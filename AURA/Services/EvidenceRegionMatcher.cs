using System.Globalization;
using System.Text;
using System.Text.RegularExpressions;
using AURA.Models;

namespace AURA.Services;

/// <summary>
/// Deterministically places extracted facts on OCR word boxes. It fails closed: a field is drawn
/// only when its value is found exactly once (or a printed label makes one hit unambiguous).
/// It never edits facts and never guesses a box.
/// </summary>
public static partial class EvidenceRegionMatcher
{
    public const string Version = "match-v1";

    public const string MerchantField = "merchantName";
    public const string IdentifierField = "identifier";
    public const string DateField = "documentDate";
    public const string TotalField = "totalAmount";

    private const double TokenSimilarity = 0.75;
    private const double LabelCoverage = 0.6;
    private const double MerchantSimilarity = 0.85;
    private const int MaxWindowWords = 3;
    private const int PaddingPixels = 4;

    public static (IReadOnlyList<EvidenceRegion> Regions, IReadOnlyList<UnlocatedEvidence> Unlocated) Match(
        OcrPage page, ReceiptExtractionDto facts)
    {
        ArgumentNullException.ThrowIfNull(page);
        ArgumentNullException.ThrowIfNull(facts);
        var lines = BuildLines(page.Words);
        var rows = BuildRows(lines);
        var regions = new List<EvidenceRegion>();
        var unlocated = new List<UnlocatedEvidence>();

        void Add(string field, string label, Outcome outcome)
        {
            if (outcome.Words is { Count: > 0 } words)
                regions.Add(new EvidenceRegion(field, label, string.Join(' ', words.Select(word => word.Text)),
                    ToBox(words, page.Width, page.Height)));
            else
                unlocated.Add(new UnlocatedEvidence(field, label, outcome.Reason ?? "Không định vị được."));
        }

        Add(MerchantField, "Đơn vị bán", LocateMerchant(lines, facts.MerchantName));
        var (identifierLabel, identifier) = CanonicalIdentifier(facts);
        Add(IdentifierField, identifierLabel, LocateIdentifier(lines, identifier));
        Add(DateField, "Ngày chứng từ / giao dịch", LocateDate(lines,
            FirstNonEmpty(facts.InvoiceDateEvidence, facts.TransactionDateEvidence)));
        Add(TotalField, "Tổng thanh toán", LocateTotal(lines, rows, facts.TotalAmount, facts.TotalAmountEvidence));
        return (regions, unlocated);
    }

    private sealed record Outcome(IReadOnlyList<OcrWord>? Words, string? Reason)
    {
        public static Outcome Found(IReadOnlyList<OcrWord> words) => new(words, null);
        public static Outcome Missing(string reason) => new(null, reason);
    }

    private sealed record Line(int Id, IReadOnlyList<OcrWord> Words)
    {
        public int Top => Words.Min(word => word.Y1);
        public int Bottom => Words.Max(word => word.Y2);
        public int Left => Words.Min(word => word.X1);
        public double CenterX => (Left + Words.Max(word => word.X2)) / 2.0;
        public double CenterY => (Top + Bottom) / 2.0;
        public int Height => Math.Max(1, Bottom - Top);
    }

    private sealed record Hit(Line Line, int Start, int End)
    {
        public IReadOnlyList<OcrWord> Words => Line.Words.Skip(Start).Take(End - Start + 1).ToList();
    }

    private static List<Line> BuildLines(IReadOnlyList<OcrWord> words) =>
        words.Where(word => !string.IsNullOrWhiteSpace(word.Text) && word.X2 > word.X1 && word.Y2 > word.Y1)
            .GroupBy(word => word.LineId)
            .Select(group => new Line(group.Key, group.OrderBy(word => word.X1).ToList()))
            .OrderBy(line => line.Top).ThenBy(line => line.Left)
            .ToList();

    // Receipts put labels and amounts in separate columns; OCR may return them as separate
    // lines. Lines whose vertical centres are close belong to one visual row. A photographed
    // receipt is often slightly tilted, which moves a far-right amount up or down relative to its
    // label, so centres are first corrected by the page's estimated skew.
    private static List<List<Line>> BuildRows(List<Line> lines)
    {
        var skew = EstimateSkew(lines);
        double Level(Line line) => line.CenterY - skew * line.CenterX;
        var rows = new List<List<Line>>();
        foreach (var line in lines.OrderBy(Level))
        {
            var row = rows.LastOrDefault(candidate =>
                Math.Abs(candidate.Average(Level) - Level(line)) <=
                0.6 * Math.Max(line.Height, candidate.Max(item => item.Height)));
            if (row is null) rows.Add(new List<Line> { line });
            else row.Add(line);
        }
        return rows;
    }

    /// <summary>Median least-squares slope (dy/dx) of wide multi-word lines, limited to about ±5.7°.</summary>
    private static double EstimateSkew(List<Line> lines)
    {
        var slopes = new List<double>();
        foreach (var line in lines.Where(line => line.Words.Count >= 3))
        {
            var points = line.Words.Select(word => ((word.X1 + word.X2) / 2.0, (word.Y1 + word.Y2) / 2.0)).ToList();
            if (points.Max(point => point.Item1) - points.Min(point => point.Item1) < 100) continue;
            var meanX = points.Average(point => point.Item1);
            var meanY = points.Average(point => point.Item2);
            var denominator = points.Sum(point => (point.Item1 - meanX) * (point.Item1 - meanX));
            if (denominator <= 0) continue;
            slopes.Add(points.Sum(point => (point.Item1 - meanX) * (point.Item2 - meanY)) / denominator);
        }
        if (slopes.Count == 0) return 0;
        slopes.Sort();
        return Math.Clamp(slopes[slopes.Count / 2], -0.1, 0.1);
    }

    private static Outcome LocateMerchant(List<Line> lines, string? merchantName)
    {
        var target = Fold(merchantName);
        if (target.Length < 3) return Outcome.Missing("AI không trả tên đơn vị bán.");

        // A name may wrap onto the next line, so consider single lines and adjacent pairs.
        var candidates = new List<(IReadOnlyList<OcrWord> Words, double Score)>();
        for (var index = 0; index < lines.Count; index++)
        {
            candidates.Add((lines[index].Words, Similarity(Fold(Text(lines[index].Words)), target)));
            if (index + 1 < lines.Count)
            {
                var pair = lines[index].Words.Concat(lines[index + 1].Words).ToList();
                candidates.Add((pair, Similarity(Fold(Text(pair)), target)));
            }
        }

        var ranked = candidates.OrderByDescending(candidate => candidate.Score).ToList();
        if (ranked.Count == 0 || ranked[0].Score < MerchantSimilarity)
            return Outcome.Missing("Không thấy dòng chữ khớp tên đơn vị bán.");
        var best = ranked[0];
        var rival = ranked.Skip(1).FirstOrDefault(candidate =>
            candidate.Score >= best.Score - 0.05 && !candidate.Words.Intersect(best.Words).Any());
        return rival.Words is null
            ? Outcome.Found(best.Words)
            : Outcome.Missing("Tên đơn vị bán khớp nhiều chỗ trên ảnh.");
    }

    private static (string Label, string? Value) CanonicalIdentifier(ReceiptExtractionDto facts)
    {
        foreach (var (label, value) in new[]
                 {
                     ("Số hóa đơn", facts.InvoiceNumber), ("Số biên nhận / Bill No", facts.ReceiptNumber),
                     ("Mã giao dịch", facts.TransactionReference), ("Mã đơn hàng", facts.OrderId),
                     ("Mã chuyến / đặt chỗ", facts.BookingId), ("Số chứng từ", facts.DocumentNumber)
                 })
        {
            if (!string.IsNullOrWhiteSpace(value)) return (label, value);
        }
        return ("Định danh giao dịch", null);
    }

    private static Outcome LocateIdentifier(List<Line> lines, string? value)
    {
        var target = AlphaNumeric(value);
        if (target.Length < 3) return Outcome.Missing("Không có mã định danh đủ dài để định vị chắc chắn.");
        var hits = FindWindows(lines, words => AlphaNumeric(Text(words)) == target, maxWords: 2);
        return hits.Count switch
        {
            0 => Outcome.Missing("Không thấy mã định danh trên ảnh."),
            1 => Outcome.Found(WithLeadingLabel(hits[0])),
            _ => Outcome.Missing("Mã định danh xuất hiện ở nhiều chỗ.")
        };
    }

    private static Outcome LocateDate(List<Line> lines, string? evidence)
    {
        var printed = evidence is null ? null : DatePattern().Match(evidence);
        if (printed is not { Success: true }) return Outcome.Missing("Thiếu chuỗi ngày in nguyên văn.");
        var target = DateDigits(printed);
        var hits = FindWindows(lines, words =>
        {
            // OCR sometimes splits a printed date inside a number ("05/1 0/2026"); join digit runs only.
            var text = SplitDigits().Replace(Text(words), string.Empty).Trim();
            var match = DatePattern().Match(text);
            return match.Success && DateDigits(match) == target && match.Length >= text.Length - 1;
        }, MaxWindowWords);
        return hits.Count switch
        {
            0 => Outcome.Missing("Không thấy ngày in trên ảnh."),
            1 => Outcome.Found(WithLeadingLabel(hits[0])),
            _ => Outcome.Missing("Ngày xuất hiện ở nhiều chỗ.")
        };
    }

    private static Outcome LocateTotal(List<Line> lines, List<List<Line>> rows, decimal? total, string? evidence)
    {
        if (total is not { } amount || amount <= 0) return Outcome.Missing("AI không trả tổng thanh toán.");
        if (amount != decimal.Truncate(amount)) return Outcome.Missing("Tổng có phần lẻ; không định vị tự động.");
        var target = ((long)amount).ToString(CultureInfo.InvariantCulture);
        var hits = FindWindows(lines, words => AmountDigits(Text(words)) == target, MaxWindowWords);
        if (hits.Count == 0) return Outcome.Missing("Không thấy số tổng trên ảnh.");

        var labelTokens = Fold(LettersOnly(evidence)).Split(' ', StringSplitOptions.RemoveEmptyEntries)
            .Where(token => token.Length >= 2 && token is not ("vnd" or "d")).ToList();
        var scored = hits.Select(hit =>
        {
            var row = rows.First(candidate => candidate.Contains(hit.Line));
            var labelWords = row.SelectMany(line => line.Words).Except(hit.Words)
                .Where(word => labelTokens.Count > 0 && Fold(word.Text).Split(' ', StringSplitOptions.RemoveEmptyEntries)
                    .Any(token => labelTokens.Any(label => Similarity(token, label) >= TokenSimilarity)))
                .ToList();
            var covered = labelTokens.Count(label => labelWords.Any(word =>
                Fold(word.Text).Split(' ', StringSplitOptions.RemoveEmptyEntries)
                    .Any(token => Similarity(token, label) >= TokenSimilarity)));
            var coverage = labelTokens.Count == 0 ? 0 : covered / (double)labelTokens.Count;
            return (Hit: hit, LabelWords: labelWords, Coverage: coverage);
        }).ToList();

        // The number alone is not enough: if the printed total line is destroyed, the same value may
        // survive only as a subtotal. Draw only where the number shares a row with the verbatim
        // total label, and only when exactly one such row exists.
        if (labelTokens.Count == 0)
            return Outcome.Missing("Thiếu dòng tổng in nguyên văn; không đoán vị trí số tổng.");
        var labelled = scored.Where(item => item.Coverage >= LabelCoverage).ToList();
        if (labelled.Count == 0)
            return Outcome.Missing("Không thấy số tổng nằm cùng hàng với nhãn tổng thanh toán.");
        if (labelled.Count > 1)
            return Outcome.Missing("Số tổng xuất hiện ở nhiều chỗ; không chọn thay người duyệt.");
        // Label first so the region reads like the receipt: "TỔNG THANH TOÁN 241.920 đ".
        var chosen = labelled[0];
        return Outcome.Found(chosen.LabelWords.OrderBy(word => word.X1).Concat(chosen.Hit.Words).ToList());
    }

    private static List<Hit> FindWindows(List<Line> lines, Func<IReadOnlyList<OcrWord>, bool> matches, int maxWords)
    {
        var hits = new List<Hit>();
        foreach (var line in lines)
        {
            for (var start = 0; start < line.Words.Count; start++)
            {
                for (var length = 1; length <= maxWords && start + length <= line.Words.Count; length++)
                {
                    var window = line.Words.Skip(start).Take(length).ToList();
                    if (!matches(window)) continue;
                    // Overlapping windows on one line (e.g. "241.920" and "241.920 đ") are one hit.
                    var end = start + length - 1;
                    var overlap = hits.FirstOrDefault(hit => hit.Line == line && hit.Start <= end && start <= hit.End);
                    if (overlap is null) hits.Add(new Hit(line, start, end));
                    else hits[hits.IndexOf(overlap)] = overlap with
                    {
                        Start = Math.Min(overlap.Start, start), End = Math.Max(overlap.End, end)
                    };
                }
            }
        }
        return hits;
    }

    // Include the printed label just before a value ("Số hóa đơn: 000142"), stopping at numbers.
    private static IReadOnlyList<OcrWord> WithLeadingLabel(Hit hit)
    {
        var start = hit.Start;
        while (start > 0 && hit.Start - start < 5 && !hit.Line.Words[start - 1].Text.Any(char.IsDigit))
            start--;
        return hit.Line.Words.Skip(start).Take(hit.End - start + 1).ToList();
    }

    private static EvidenceBox ToBox(IReadOnlyList<OcrWord> words, int width, int height)
    {
        var x1 = Math.Max(0, words.Min(word => word.X1) - PaddingPixels);
        var y1 = Math.Max(0, words.Min(word => word.Y1) - PaddingPixels);
        var x2 = Math.Min(width, words.Max(word => word.X2) + PaddingPixels);
        var y2 = Math.Min(height, words.Max(word => word.Y2) + PaddingPixels);
        const int space = EvidenceLocalizationResult.CoordinateSpace;
        var x = Math.Clamp(x1 * space / width, 0, space - 1);
        var y = Math.Clamp(y1 * space / height, 0, space - 1);
        var right = Math.Clamp((int)Math.Ceiling(x2 * (double)space / width), x + 1, space);
        var bottom = Math.Clamp((int)Math.Ceiling(y2 * (double)space / height), y + 1, space);
        return new EvidenceBox(x, y, right - x, bottom - y);
    }

    private static string Text(IEnumerable<OcrWord> words) => string.Join(' ', words.Select(word => word.Text));

    private static string? FirstNonEmpty(params string?[] values) =>
        values.FirstOrDefault(value => !string.IsNullOrWhiteSpace(value));

    private static string DateDigits(Match match) =>
        match.Groups[1].Value + match.Groups[2].Value + match.Groups[3].Value;

    /// <summary>Digits of a printed money value; null when the text holds anything but a number and a VND unit.</summary>
    internal static string? AmountDigits(string text)
    {
        var match = AmountPattern().Match(text.Trim());
        if (!match.Success) return null;
        var digits = new string(match.Groups["number"].Value.Where(char.IsDigit).ToArray()).TrimStart('0');
        return digits.Length == 0 ? null : digits;
    }

    internal static string AlphaNumeric(string? value) =>
        Fold(value).Replace(" ", string.Empty, StringComparison.Ordinal);

    private static string LettersOnly(string? value) =>
        new((value ?? string.Empty).Select(character => char.IsLetter(character) ? character : ' ').ToArray());

    /// <summary>Lowercase, strip Vietnamese diacritics (đ → d) and collapse punctuation to spaces.</summary>
    internal static string Fold(string? value)
    {
        if (string.IsNullOrWhiteSpace(value)) return string.Empty;
        var builder = new StringBuilder(value.Length);
        foreach (var character in value.Normalize(NormalizationForm.FormD))
        {
            if (CharUnicodeInfo.GetUnicodeCategory(character) == UnicodeCategory.NonSpacingMark) continue;
            var lower = character is 'đ' or 'Đ' ? 'd' : char.ToLowerInvariant(character);
            builder.Append(char.IsLetterOrDigit(lower) ? lower : ' ');
        }
        return string.Join(' ', builder.ToString().Split(' ', StringSplitOptions.RemoveEmptyEntries));
    }

    /// <summary>1 − normalised Levenshtein distance.</summary>
    internal static double Similarity(string left, string right)
    {
        if (left.Length == 0 && right.Length == 0) return 1;
        if (left.Length == 0 || right.Length == 0) return 0;
        var previous = Enumerable.Range(0, right.Length + 1).ToArray();
        var current = new int[right.Length + 1];
        for (var i = 1; i <= left.Length; i++)
        {
            current[0] = i;
            for (var j = 1; j <= right.Length; j++)
            {
                var cost = left[i - 1] == right[j - 1] ? 0 : 1;
                current[j] = Math.Min(Math.Min(current[j - 1] + 1, previous[j] + 1), previous[j - 1] + cost);
            }
            (previous, current) = (current, previous);
        }
        return 1 - previous[right.Length] / (double)Math.Max(left.Length, right.Length);
    }

    [GeneratedRegex(@"(\d{1,4})\s*[/\-.]\s*(\d{1,2})\s*[/\-.]\s*(\d{1,4})")]
    private static partial Regex DatePattern();

    [GeneratedRegex(@"(?<=\d)\s+(?=\d)")]
    private static partial Regex SplitDigits();

    [GeneratedRegex(@"^(?<number>\d{1,3}(?:[.,\s]\d{3})+|\d+)\s*(?:đ|Đ|₫|d|D|vnd|VND|vnđ|VNĐ)?$")]
    private static partial Regex AmountPattern();
}
