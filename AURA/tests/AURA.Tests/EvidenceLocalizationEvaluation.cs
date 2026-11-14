using System.Text.Json;
using System.Text.Json.Serialization;
using AURA.Models;
using AURA.Services;

namespace AURA.Tests;

/// <summary>
/// Stage-3 evaluation for evidence boxes. An independent annotator draws the true box (or marks
/// a field absent/unreadable) for each receipt with tools/evidence_annotation, locks the file, and
/// this harness compares the localizer's boxes against it. Ground truth never comes from OCR.
/// </summary>
public static class EvidenceLocalizationEvaluation
{
    public const string Format = "aura-evidence-annotations-v1";
    public const double IouThreshold = 0.5;

    public static readonly string[] Fields =
    {
        EvidenceRegionMatcher.MerchantField, EvidenceRegionMatcher.IdentifierField,
        EvidenceRegionMatcher.DateField, EvidenceRegionMatcher.TotalField
    };

    public static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web) { WriteIndented = true };

    public enum Outcome
    {
        /// <summary>Box drawn and overlapping the true box (IoU ≥ threshold).</summary>
        Hit,
        /// <summary>Box drawn in the wrong place for a field that is present.</summary>
        Misplaced,
        /// <summary>No box although the field is present.</summary>
        Missed,
        /// <summary>No box for a field that is absent or unreadable — the desired abstention.</summary>
        CorrectAbstain,
        /// <summary>Box drawn for a field that is absent or unreadable.</summary>
        FalseOnAbsent
    }

    public static double IoU(EvidenceBox a, EvidenceBox b)
    {
        var width = Math.Min(a.X + a.Width, b.X + b.Width) - Math.Max(a.X, b.X);
        var height = Math.Min(a.Y + a.Height, b.Y + b.Height) - Math.Max(a.Y, b.Y);
        if (width <= 0 || height <= 0) return 0;
        var intersection = (double)width * height;
        return intersection / ((double)a.Width * a.Height + (double)b.Width * b.Height - intersection);
    }

    public static (Outcome Outcome, double? IoU) Classify(AnnotatedField truth, EvidenceBox? predicted)
    {
        if (!truth.IsPresent) return (predicted is null ? Outcome.CorrectAbstain : Outcome.FalseOnAbsent, null);
        if (predicted is null) return (Outcome.Missed, null);
        var overlap = IoU(truth.Box!, predicted);
        return (overlap >= IouThreshold ? Outcome.Hit : Outcome.Misplaced, overlap);
    }

    public static ReceiptExtractionDto ToFacts(AnnotatedFacts facts)
    {
        var dto = new ReceiptExtractionDto
        {
            MerchantName = facts.MerchantName,
            InvoiceDateEvidence = facts.DateEvidence,
            TotalAmount = facts.TotalAmount,
            TotalAmountEvidence = facts.TotalEvidence
        };
        switch (facts.IdentifierField)
        {
            case "receiptNumber": dto.ReceiptNumber = facts.Identifier; break;
            case "transactionReference": dto.TransactionReference = facts.Identifier; break;
            case "orderId": dto.OrderId = facts.Identifier; break;
            case "bookingId": dto.BookingId = facts.Identifier; break;
            case "documentNumber": dto.DocumentNumber = facts.Identifier; break;
            default: dto.InvoiceNumber = facts.Identifier; break;
        }
        return dto;
    }

    /// <summary>Checks the annotation file before any OCR runs; returns human-readable problems.</summary>
    public static IReadOnlyList<string> Validate(AnnotationFile file)
    {
        var problems = new List<string>();
        if (file.Format != Format) problems.Add($"format phải là {Format}.");
        if (file.Images.Count == 0) problems.Add("Không có ảnh nào.");
        foreach (var image in file.Images)
        {
            if (string.IsNullOrWhiteSpace(image.File) || Path.GetFileName(image.File) != image.File)
                problems.Add($"Tên file không hợp lệ: {image.File}.");
            if (image.Sha256 is not { Length: 64 } || !image.Sha256.All(Uri.IsHexDigit))
                problems.Add($"{image.File}: thiếu SHA-256.");
            foreach (var field in Fields)
            {
                if (!image.Fields.TryGetValue(field, out var truth))
                {
                    problems.Add($"{image.File}: chưa đánh dấu {field}.");
                    continue;
                }
                if (truth.Status is not ("present" or "absent")) problems.Add($"{image.File}/{field}: status sai.");
                if (truth.IsPresent && (truth.Box is not { } box || box.Width <= 0 || box.Height <= 0 ||
                    box.X < 0 || box.Y < 0 || box.X + box.Width > 1000 || box.Y + box.Height > 1000))
                    problems.Add($"{image.File}/{field}: khung không hợp lệ.");
            }
        }
        return problems;
    }

    public static EvaluationReport Evaluate(IEnumerable<ImageResult> results)
    {
        var list = results.ToList();
        var fields = Fields.Select(field =>
        {
            var outcomes = list.Where(result => !result.Skipped)
                .Select(result => result.Outcomes[field]).ToList();
            int Count(Outcome outcome) => outcomes.Count(item => item.Outcome == outcome);
            var present = Count(Outcome.Hit) + Count(Outcome.Misplaced) + Count(Outcome.Missed);
            var located = Count(Outcome.Hit) + Count(Outcome.Misplaced) + Count(Outcome.FalseOnAbsent);
            var falseHighlights = Count(Outcome.Misplaced) + Count(Outcome.FalseOnAbsent);
            var hitIous = outcomes.Where(item => item.Outcome == Outcome.Hit).Select(item => item.IoU!.Value).ToList();
            return new FieldMetrics(field, present, Count(Outcome.Hit), Count(Outcome.Misplaced), Count(Outcome.Missed),
                Count(Outcome.CorrectAbstain), Count(Outcome.FalseOnAbsent),
                present == 0 ? null : Count(Outcome.Hit) / (double)present,
                located == 0 ? null : falseHighlights / (double)located,
                hitIous.Count == 0 ? null : hitIous.Average());
        }).ToList();
        return new EvaluationReport(list.Count, list.Count(result => result.Skipped), fields, list);
    }

    /// <summary>
    /// Proposed gate from docs/EVIDENCE_LOCALIZATION_PLAN.md (owner to confirm before a locked run):
    /// no false highlight on totalAmount, false-highlight rate ≤ 2 % and recall ≥ 80 % per field,
    /// at least 30 evaluated images and at least 5 absent/unreadable negative controls.
    /// </summary>
    public static IReadOnlyList<string> GateFailures(EvaluationReport report)
    {
        var failures = new List<string>();
        var evaluated = report.Images - report.Skipped;
        if (evaluated < 30) failures.Add($"Chỉ có {evaluated} ảnh được đánh giá (cần ≥ 30).");
        var negatives = report.Fields.Sum(field => field.CorrectAbstain + field.FalseOnAbsent);
        if (negatives < 5) failures.Add($"Chỉ có {negatives} ca đối chứng âm (cần ≥ 5).");
        foreach (var field in report.Fields)
        {
            if (field.Field == EvidenceRegionMatcher.TotalField && field.Misplaced + field.FalseOnAbsent > 0)
                failures.Add($"totalAmount có {field.Misplaced + field.FalseOnAbsent} khung đặt nhầm (yêu cầu 0).");
            if (field.FalseHighlightRate > 0.02)
                failures.Add($"{field.Field}: tỉ lệ khoanh nhầm {field.FalseHighlightRate:P1} (> 2%).");
            if (field.Recall is { } recall && recall < 0.8)
                failures.Add($"{field.Field}: recall {recall:P1} (< 80%).");
        }
        return failures;
    }

    public static string ToMarkdown(EvaluationReport report, string annotationSha256, string localizer,
        IReadOnlyList<string> gateFailures)
    {
        var lines = new List<string>
        {
            "# Đánh giá khung bằng chứng (evidence localization)",
            "",
            $"- Annotation SHA-256: `{annotationSha256}`",
            $"- Localizer: `{localizer}` · IoU ≥ {IouThreshold}",
            $"- Ảnh: {report.Images} (bỏ qua {report.Skipped})",
            $"- Gate đề xuất: **{(gateFailures.Count == 0 ? "ĐẠT" : "CHƯA ĐẠT")}**",
            "",
            "| Trường | Có trên ảnh | Đúng | Đặt nhầm | Bỏ sót | Bỏ qua đúng | Vẽ khi không có | Recall | Tỉ lệ khoanh nhầm | IoU TB |",
            "| --- | ---: | ---: | ---: | ---: | ---: | ---: | ---: | ---: | ---: |"
        };
        static string Percent(double? value) => value is null ? "—" : $"{value:P1}";
        lines.AddRange(report.Fields.Select(field =>
            $"| {field.Field} | {field.Present} | {field.Hits} | {field.Misplaced} | {field.Missed} | " +
            $"{field.CorrectAbstain} | {field.FalseOnAbsent} | {Percent(field.Recall)} | " +
            $"{Percent(field.FalseHighlightRate)} | {(field.MeanIoU is null ? "—" : field.MeanIoU.Value.ToString("0.00"))} |"));
        if (gateFailures.Count > 0)
        {
            lines.Add("");
            lines.Add("## Lý do chưa đạt gate");
            lines.AddRange(gateFailures.Select(failure => "- " + failure));
        }
        lines.Add("");
        lines.Add("Số liệu chỉ có giá trị khi annotation được khóa trước khi chạy và do người khác thực hiện.");
        return string.Join(Environment.NewLine, lines) + Environment.NewLine;
    }

    public sealed class AnnotationFile
    {
        public string Format { get; set; } = string.Empty;
        public string? Annotator { get; set; }
        public string? CreatedAt { get; set; }
        public string? Note { get; set; }
        public List<AnnotatedImage> Images { get; set; } = new();
    }

    public sealed class AnnotatedImage
    {
        public string File { get; set; } = string.Empty;
        public string Sha256 { get; set; } = string.Empty;
        public int Width { get; set; }
        public int Height { get; set; }
        public AnnotatedFacts Facts { get; set; } = new();
        public Dictionary<string, AnnotatedField> Fields { get; set; } = new();
    }

    public sealed class AnnotatedFacts
    {
        public string? MerchantName { get; set; }
        public string? Identifier { get; set; }
        public string? IdentifierField { get; set; }
        public string? DateEvidence { get; set; }
        public decimal? TotalAmount { get; set; }
        public string? TotalEvidence { get; set; }
    }

    public sealed class AnnotatedField
    {
        public string Status { get; set; } = string.Empty;
        public EvidenceBox? Box { get; set; }

        [JsonIgnore]
        public bool IsPresent => Status == "present";
    }

    public sealed record FieldResult(Outcome Outcome, double? IoU, EvidenceBox? Predicted);

    public sealed record ImageResult(string File, bool Skipped, string? SkipReason,
        IReadOnlyDictionary<string, FieldResult> Outcomes);

    public sealed record FieldMetrics(string Field, int Present, int Hits, int Misplaced, int Missed,
        int CorrectAbstain, int FalseOnAbsent, double? Recall, double? FalseHighlightRate, double? MeanIoU);

    public sealed record EvaluationReport(int Images, int Skipped, IReadOnlyList<FieldMetrics> Fields,
        IReadOnlyList<ImageResult> Results);
}
