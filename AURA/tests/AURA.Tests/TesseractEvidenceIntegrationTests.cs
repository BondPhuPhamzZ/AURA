using System.Text.Json;
using AURA.Models;
using AURA.Options;
using AURA.Services;
using Xunit;
using Xunit.Abstractions;
using MsOptions = Microsoft.Extensions.Options.Options;

namespace AURA.Tests;

/// <summary>
/// Opt-in: AURA_RUN_OCR_INTEGRATION=1 and AURA_TESSDATA_PATH=&lt;folder with vie/eng traineddata&gt;.
/// Runs real Tesseract on the 15 locked synthetic v3.1 receipts with facts taken from the manifest.
/// This is a development-set smoke, not an accuracy benchmark: the images were seen while building.
/// </summary>
public sealed class TesseractEvidenceIntegrationTests(ITestOutputHelper output)
{
    private static readonly string ProjectRoot = FindProjectRoot();

    [Fact]
    public async Task Synthetic_dev_set_locates_clean_fields_and_skips_destroyed_ones()
    {
        if (Environment.GetEnvironmentVariable("AURA_RUN_OCR_INTEGRATION") != "1") return;
        var tessdata = Environment.GetEnvironmentVariable("AURA_TESSDATA_PATH");
        Assert.False(string.IsNullOrWhiteSpace(tessdata), "Set AURA_TESSDATA_PATH to the pinned tessdata folder.");

        using var engine = new TesseractReceiptOcrEngine(MsOptions.Create(new EvidenceLocalizationOptions
        {
            Enabled = true, TessdataPath = tessdata!, Languages = "vie+eng", TimeoutSeconds = 60
        }));
        var kit = Path.Combine(ProjectRoot, "test_kit", "v3_1");
        using var manifest = JsonDocument.Parse(await File.ReadAllTextAsync(Path.Combine(kit, "manifest.json")));
        var located = new Dictionary<string, HashSet<string>>();

        foreach (var testCase in manifest.RootElement.GetProperty("cases").EnumerateArray())
        {
            var id = testCase.GetProperty("id").GetString()!;
            var bytes = await File.ReadAllBytesAsync(Path.Combine(kit, "images",
                testCase.GetProperty("file_name").GetString()!));
            Assert.Equal(ImageOrientation.Upright, ImageOrientation.Read(bytes));

            var page = await engine.RecognizeAsync(bytes, CancellationToken.None);
            var (regions, unlocated) = EvidenceRegionMatcher.Match(page, FactsFor(testCase));
            located[id] = regions.Select(region => region.Field).ToHashSet();
            output.WriteLine($"{id}: located [{string.Join(", ", located[id])}]; " +
                $"unlocated [{string.Join(", ", unlocated.Select(item => $"{item.Field}: {item.Reason}"))}]");

            foreach (var region in regions)
                Assert.True(region.Box.X + region.Box.Width <= 1000 && region.Box.Y + region.Box.Height <= 1000);
            // A located total must contain the expected final amount next to the total label.
            if (regions.FirstOrDefault(region => region.Field == EvidenceRegionMatcher.TotalField) is { } total)
                Assert.Contains(testCase.GetProperty("total").GetDecimal().ToString("N0",
                    new System.Globalization.CultureInfo("vi-VN")), total.Text);
        }

        // Clean VAT invoice: every reviewer field is placed.
        Assert.Equal(new[] { "merchantName", "identifier", "documentDate", "totalAmount" }.ToHashSet(), located["R3-01"]);
        // Negative controls: a destroyed printed total / receipt number must not be drawn.
        Assert.DoesNotContain(EvidenceRegionMatcher.TotalField, located["R3-06"]);
        Assert.DoesNotContain(EvidenceRegionMatcher.IdentifierField, located["R3-08"]);
    }

    private static ReceiptExtractionDto FactsFor(JsonElement testCase)
    {
        string? Read(string name) =>
            testCase.TryGetProperty(name, out var value) && value.ValueKind == JsonValueKind.String
                ? value.GetString() : null;
        var total = testCase.GetProperty("total").GetDecimal();
        var facts = new ReceiptExtractionDto
        {
            MerchantName = Read("merchant"),
            InvoiceDateEvidence = Read("date_evidence"),
            TotalAmount = total,
            // A model reading the printed final line returns it verbatim; use the common label here.
            TotalAmountEvidence = $"TỔNG THANH TOÁN {total.ToString("N0", new System.Globalization.CultureInfo("vi-VN"))} đ"
        };
        var identifier = Read("identifier_value");
        switch (Read("identifier_field"))
        {
            case "invoiceNumber": facts.InvoiceNumber = identifier; break;
            case "receiptNumber": facts.ReceiptNumber = identifier; break;
            case "transactionReference": facts.TransactionReference = identifier; break;
        }
        return facts;
    }

    private static string FindProjectRoot()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory is not null)
        {
            if (File.Exists(Path.Combine(directory.FullName, "AURA.csproj"))) return directory.FullName;
            directory = directory.Parent;
        }

        throw new DirectoryNotFoundException("Không tìm thấy AURA.csproj từ thư mục test output.");
    }
}
