using System.Text;
using System.Text.Json;
using AURA.Models;
using AURA.Services;
using Xunit;

namespace AURA.Tests;

public sealed class ManagerOperationsReportExporterTests
{
    [Fact]
    public void Exports_only_current_vietnam_month_in_stable_order()
    {
        var requests = new[]
        {
            Request("second", new DateTime(2026, 10, 17, 2, 0, 0, DateTimeKind.Utc)),
            Request("first", new DateTime(2026, 10, 16, 17, 30, 0, DateTimeKind.Utc)),
            Request("old", new DateTime(2026, 9, 30, 1, 0, 0, DateTimeKind.Utc))
        };

        var report = ManagerOperationsReportExporter.ExportCurrentMonth(
            requests, new DateTime(2026, 10, 17, 3, 0, 0, DateTimeKind.Utc));
        var csv = Decode(report.Content);

        Assert.Equal("AURA_Operations_2026-10.csv", report.FileName);
        Assert.Equal(2, report.RecordCount);
        Assert.True(csv.IndexOf("first", StringComparison.Ordinal) <
                    csv.IndexOf("second", StringComparison.Ordinal));
        Assert.DoesNotContain("old", csv, StringComparison.Ordinal);
    }

    [Fact]
    public void Prevents_formula_injection_and_excludes_raw_extracted_values()
    {
        var request = Request("safe", new DateTime(2026, 10, 17, 1, 0, 0, DateTimeKind.Utc));
        request.OriginalFileName = "=HYPERLINK(\"https://invalid.example\")";
        request.ExtractedFactsJson = JsonSerializer.Serialize(new ReceiptExtractionDto
        {
            MerchantName = "DO_NOT_EXPORT_RAW_FACTS",
            AmountSource = AmountSources.AiExtracted,
            RequiresPrivacyReview = true,
            PersonalDataCategories = ["phone"]
        });

        var report = ManagerOperationsReportExporter.ExportCurrentMonth(
            [request], new DateTime(2026, 10, 17, 3, 0, 0, DateTimeKind.Utc));
        var csv = Decode(report.Content);

        Assert.Contains("'=HYPERLINK", csv, StringComparison.Ordinal);
        Assert.DoesNotContain("DO_NOT_EXPORT_RAW_FACTS", csv, StringComparison.Ordinal);
        Assert.Contains("AI_EXTRACTED", csv, StringComparison.Ordinal);
        Assert.Contains("PHONE", csv, StringComparison.Ordinal);
    }

    [Fact]
    public void Uses_utf8_bom_for_excel_compatibility()
    {
        var report = ManagerOperationsReportExporter.ExportCurrentMonth(
            Array.Empty<ReimbursementRequest>(),
            new DateTime(2026, 10, 17, 3, 0, 0, DateTimeKind.Utc));
        var preamble = Encoding.UTF8.GetPreamble();

        Assert.True(report.Content.AsSpan().StartsWith(preamble));
        Assert.Contains("Mã hồ sơ", Decode(report.Content), StringComparison.Ordinal);
    }

    private static string Decode(byte[] content) =>
        Encoding.UTF8.GetString(content.AsSpan(Encoding.UTF8.GetPreamble().Length));

    private static ReimbursementRequest Request(string id, DateTime createdAt) => new()
    {
        Id = id,
        OriginalFileName = id + ".jpg",
        StoredFileName = id + ".jpg",
        ContentType = "image/jpeg",
        FileSha256 = new string('A', 64),
        ImageUrl = "/receipt/" + id,
        CreatedAt = createdAt,
        Status = "AUTO_APPROVE",
        ProcessingState = ProcessingStates.Completed,
        ClaimedAmount = 120_000m,
        ProcessingLatencyMs = 1_200
    };
}
