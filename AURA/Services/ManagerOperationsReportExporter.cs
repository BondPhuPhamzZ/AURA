using System.Globalization;
using System.Text;
using System.Text.Json;
using AURA.Models;

namespace AURA.Services;

public static class ManagerOperationsReportExporter
{
    private static readonly string[] Headers =
    [
        "Mã hồ sơ",
        "Thời gian tiếp nhận",
        "Tên tệp",
        "Số tiền VND",
        "Nguồn số tiền",
        "Trạng thái xử lý",
        "Route quyết định",
        "Privacy review",
        "Nhóm dữ liệu cá nhân",
        "Đã chuyển quản lý",
        "Quyết định quản lý",
        "Độ trễ ms"
    ];

    public static ManagerOperationsReport ExportCurrentMonth(
        IEnumerable<ReimbursementRequest> requests,
        DateTime utcNow)
    {
        var localNow = VietnamTime.FromUtc(utcNow);
        return ExportMonth(requests, localNow.Year, localNow.Month);
    }

    public static ManagerOperationsReport ExportMonth(
        IEnumerable<ReimbursementRequest> requests,
        int year,
        int month)
    {
        if (year is < 2000 or > 2100) throw new ArgumentOutOfRangeException(nameof(year));
        if (month is < 1 or > 12) throw new ArgumentOutOfRangeException(nameof(month));

        var selected = requests
            .Select(request => new ProjectedRequest(request, VietnamTime.FromUtc(request.CreatedAt),
                ReadFacts(request)))
            .Where(item => item.LocalCreatedAt.Year == year && item.LocalCreatedAt.Month == month)
            .OrderBy(item => item.LocalCreatedAt)
            .ThenBy(item => item.Request.Id, StringComparer.Ordinal)
            .ToArray();

        var csv = new StringBuilder();
        csv.AppendLine(string.Join(',', Headers.Select(EscapeCsv)));

        foreach (var item in selected)
        {
            var request = item.Request;
            var facts = item.Facts;
            var privacyReview = request.Status == "ESCALATE_PRIVACY" ||
                                facts.RequiresPrivacyReview ||
                                facts.Categories.Count > 0;
            var managerDecision = request.ManagerAnswer switch
            {
                true => "YES",
                false => "NO",
                null => "PENDING"
            };

            var cells = new[]
            {
                request.Id,
                item.LocalCreatedAt.ToString("dd/MM/yyyy HH:mm:ss", CultureInfo.InvariantCulture),
                request.OriginalFileName,
                Math.Max(0, request.ClaimedAmount).ToString("0.##", CultureInfo.InvariantCulture),
                facts.AmountSource,
                request.ProcessingState,
                request.Status,
                privacyReview ? "YES" : "NO",
                string.Join('|', facts.Categories),
                request.IsForwardedToManager ? "YES" : "NO",
                managerDecision,
                Math.Max(0, request.ProcessingLatencyMs).ToString(CultureInfo.InvariantCulture)
            };

            csv.AppendLine(string.Join(',', cells.Select(EscapeCsv)));
        }

        var body = Encoding.UTF8.GetBytes(csv.ToString());
        var preamble = Encoding.UTF8.GetPreamble();
        var content = new byte[preamble.Length + body.Length];
        Buffer.BlockCopy(preamble, 0, content, 0, preamble.Length);
        Buffer.BlockCopy(body, 0, content, preamble.Length, body.Length);

        return new ManagerOperationsReport(
            $"AURA_Operations_{year:0000}-{month:00}.csv",
            content,
            selected.Length);
    }

    private static ExportFacts ReadFacts(ReimbursementRequest request)
    {
        if (string.IsNullOrWhiteSpace(request.ExtractedFactsJson))
            return ExportFacts.Empty;

        try
        {
            var facts = JsonSerializer.Deserialize<ReceiptExtractionDto>(request.ExtractedFactsJson);
            if (facts is null) return ExportFacts.Empty;

            var categories = (facts.PersonalDataCategories ?? [])
                .Where(category => !string.IsNullOrWhiteSpace(category))
                .Select(category => category.Trim().ToUpperInvariant())
                .Distinct(StringComparer.Ordinal)
                .OrderBy(category => category, StringComparer.Ordinal)
                .ToArray();

            return new ExportFacts(
                string.IsNullOrWhiteSpace(facts.AmountSource) ? AmountSources.Unknown : facts.AmountSource,
                facts.RequiresPrivacyReview,
                categories);
        }
        catch (JsonException)
        {
            return ExportFacts.Empty;
        }
    }

    private static string EscapeCsv(string? value)
    {
        var normalized = (value ?? string.Empty)
            .Replace('\r', ' ')
            .Replace('\n', ' ');

        // Excel treats these leading characters as formulas. Prefixing an apostrophe
        // preserves the displayed value while preventing CSV formula execution.
        if (normalized.Length > 0 && normalized[0] is '=' or '+' or '-' or '@' or '\t')
            normalized = "'" + normalized;

        return '"' + normalized.Replace("\"", "\"\"") + '"';
    }

    private sealed record ProjectedRequest(
        ReimbursementRequest Request,
        DateTime LocalCreatedAt,
        ExportFacts Facts);

    private sealed record ExportFacts(
        string AmountSource,
        bool RequiresPrivacyReview,
        IReadOnlyList<string> Categories)
    {
        public static readonly ExportFacts Empty =
            new(AmountSources.Unknown, false, Array.Empty<string>());
    }
}

public sealed record ManagerOperationsReport(
    string FileName,
    byte[] Content,
    int RecordCount);
