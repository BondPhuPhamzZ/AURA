using System.Text.Json;
using AURA.Models;
using AURA.Services;
using Xunit;

namespace AURA.Tests;

public sealed class TestKitIntegrityTests
{
    private static readonly string ProjectRoot = FindProjectRoot();

    [Fact]
    public void Verify_manifest_has_exactly_three_auto_and_two_escalation_cases_with_images()
    {
        var manifestPath = Path.Combine(ProjectRoot, "wwwroot", "test_data", "expected-results.json");
        var cases = JsonSerializer.Deserialize<List<ExpectedResult>>(File.ReadAllText(manifestPath),
            new JsonSerializerOptions { PropertyNameCaseInsensitive = true });

        Assert.NotNull(cases);
        Assert.Equal(5, cases.Count);
        Assert.Equal(3, cases.Count(x => x.ExpectedStatus == "AUTO_APPROVE"));
        Assert.Equal(2, cases.Count(x => x.ExpectedStatus.StartsWith("ESCALATE_", StringComparison.Ordinal)));
        Assert.All(cases, testCase =>
        {
            Assert.True(testCase.ClaimedAmount > 0);
            Assert.True(File.Exists(Path.Combine(ProjectRoot, "wwwroot", "test_data", "images", testCase.ImageName)));
        });
    }

    [Fact]
    public void Benchmark_manifest_has_thirty_unique_bounded_images()
    {
        var manifestPath = Path.Combine(ProjectRoot, "test_kit", "manifest.json");
        using var document = JsonDocument.Parse(File.ReadAllText(manifestPath));
        var cases = document.RootElement.GetProperty("cases").EnumerateArray().ToList();

        Assert.Equal(30, cases.Count);
        Assert.Equal(30, cases.Select(x => x.GetProperty("id").GetString()).Distinct().Count());
        Assert.Equal(30, cases.Select(x => x.GetProperty("file_name").GetString()).Distinct().Count());
        Assert.All(cases, testCase =>
        {
            var fileName = testCase.GetProperty("file_name").GetString();
            Assert.False(string.IsNullOrWhiteSpace(fileName));
            var file = new FileInfo(Path.Combine(ProjectRoot, "test_kit", "images", fileName!));
            Assert.True(file.Exists);
            Assert.InRange(file.Length, 1, 5 * 1024L * 1024L);
        });
    }

    [Fact]
    public void Judge_manifest_has_exactly_fifteen_curated_cases_with_images_and_source_entries()
    {
        using var judgeDocument = JsonDocument.Parse(File.ReadAllText(
            Path.Combine(ProjectRoot, "test_kit", "judge-manifest.json")));
        using var sourceDocument = JsonDocument.Parse(File.ReadAllText(
            Path.Combine(ProjectRoot, "test_kit", "manifest.json")));

        var judgeCases = judgeDocument.RootElement.GetProperty("cases").EnumerateArray().ToList();
        var sourceCases = sourceDocument.RootElement.GetProperty("cases").EnumerateArray()
            .ToDictionary(x => x.GetProperty("id").GetString()!, StringComparer.Ordinal);

        Assert.Equal(15, judgeCases.Count);
        Assert.Equal(15, judgeCases.Select(x => x.GetProperty("id").GetString()).Distinct().Count());
        Assert.Equal(15, judgeCases.Select(x => x.GetProperty("fileName").GetString()).Distinct().Count());
        Assert.Equal(5, judgeCases.Count(x => x.GetProperty("expectedStatus").GetString() == "AUTO_APPROVE"));
        Assert.Equal(4, judgeCases.Count(x => x.GetProperty("expectedStatus").GetString() == "ESCALATE_FACT"));
        Assert.Equal(3, judgeCases.Count(x => x.GetProperty("expectedStatus").GetString() == "ESCALATE_POLICY"));
        Assert.Equal(3, judgeCases.Count(x => x.GetProperty("expectedStatus").GetString() == "ESCALATE_AUTHORITY"));

        Assert.All(judgeCases, testCase =>
        {
            var id = testCase.GetProperty("id").GetString()!;
            var fileName = testCase.GetProperty("fileName").GetString()!;
            Assert.True(sourceCases.TryGetValue(id, out var sourceCase));
            Assert.Equal(fileName, sourceCase.GetProperty("file_name").GetString());
            Assert.Equal(testCase.GetProperty("expectedStatus").GetString(),
                sourceCase.GetProperty("expected_status").GetString());
            Assert.Equal(testCase.GetProperty("claimedAmount").GetInt32(),
                sourceCase.GetProperty("claimed_amount").GetInt32());

            var file = new FileInfo(Path.Combine(ProjectRoot, "test_kit", "images", fileName));
            Assert.True(file.Exists);
            Assert.InRange(file.Length, 1, 5 * 1024L * 1024L);
        });
    }

    [Fact]
    public void Regression_v3_is_separate_locked_and_semantically_explicit()
    {
        var kitRoot = Path.Combine(ProjectRoot, "test_kit", "v3");
        using var judgeDocument = JsonDocument.Parse(File.ReadAllText(
            Path.Combine(kitRoot, "judge-manifest.json")));
        using var sourceDocument = JsonDocument.Parse(File.ReadAllText(
            Path.Combine(kitRoot, "manifest.json")));

        var judgeCases = judgeDocument.RootElement.GetProperty("cases").EnumerateArray().ToList();
        var sourceCases = sourceDocument.RootElement.GetProperty("cases").EnumerateArray()
            .ToDictionary(x => x.GetProperty("id").GetString()!, StringComparer.Ordinal);

        Assert.Equal("post-holdout-regression-alternative",
            sourceDocument.RootElement.GetProperty("datasetRole").GetString());
        Assert.Equal(15, judgeCases.Count);
        Assert.Equal(5, judgeCases.Count(x => x.GetProperty("expectedStatus").GetString() == "AUTO_APPROVE"));
        Assert.Equal(4, judgeCases.Count(x => x.GetProperty("expectedStatus").GetString() == "ESCALATE_FACT"));
        Assert.Equal(3, judgeCases.Count(x => x.GetProperty("expectedStatus").GetString() == "ESCALATE_POLICY"));
        Assert.Equal(3, judgeCases.Count(x => x.GetProperty("expectedStatus").GetString() == "ESCALATE_AUTHORITY"));

        var ids = judgeCases.Select(x => x.GetProperty("id").GetString()).ToList();
        Assert.Equal(15, ids.Distinct(StringComparer.Ordinal).Count());
        Assert.All(ids, id => Assert.StartsWith("R3-", id, StringComparison.Ordinal));

        foreach (var testCase in judgeCases)
        {
            var id = testCase.GetProperty("id").GetString()!;
            var fileName = testCase.GetProperty("fileName").GetString()!;
            var expectedHash = testCase.GetProperty("sha256").GetString()!;
            Assert.Matches("^[0-9A-F]{64}$", expectedHash);
            Assert.True(sourceCases.TryGetValue(id, out var sourceCase));
            Assert.Equal(fileName, sourceCase.GetProperty("file_name").GetString());
            Assert.Equal(expectedHash, sourceCase.GetProperty("sha256").GetString());
            Assert.False(string.IsNullOrWhiteSpace(testCase.GetProperty("groundTruthRationale").GetString()));

            var imagePath = Path.Combine(kitRoot, "images", fileName);
            Assert.True(File.Exists(imagePath));
            var actualHash = Convert.ToHexString(System.Security.Cryptography.SHA256.HashData(
                File.ReadAllBytes(imagePath)));
            Assert.Equal(expectedHash, actualHash);
        }

        var ambiguousLabels = new[] { "Số hóa đơn/biên nhận", "Invoice/Receipt No" };
        Assert.All(sourceCases.Values, sourceCase =>
        {
            var label = sourceCase.GetProperty("identifier_label");
            if (label.ValueKind != JsonValueKind.Null)
                Assert.DoesNotContain(label.GetString(), ambiguousLabels);
        });

        var destroyedTotal = sourceCases["R3-06"];
        var destroyedFacts = destroyedTotal.GetProperty("expected_facts");
        Assert.Equal(JsonValueKind.Null, destroyedFacts.GetProperty("totalAmount").ValueKind);
        Assert.Equal("NOT_VISIBLE", destroyedFacts.GetProperty("totalAmountSource").GetString());

        var readableFade = sourceCases["R3-05"];
        Assert.Equal("AUTO_APPROVE", readableFade.GetProperty("expected_status").GetString());
        Assert.Equal("PRINTED_FINAL_TOTAL",
            readableFade.GetProperty("expected_facts").GetProperty("totalAmountSource").GetString());
    }

    [Fact]
    public void Regression_v31_preserves_v3_and_corrects_the_policy_fixture_precondition()
    {
        var originalRoot = Path.Combine(ProjectRoot, "test_kit", "v3");
        var revisionRoot = Path.Combine(ProjectRoot, "test_kit", "v3_1");
        using var original = JsonDocument.Parse(File.ReadAllText(Path.Combine(originalRoot, "manifest.json")));
        using var revision = JsonDocument.Parse(File.ReadAllText(Path.Combine(revisionRoot, "judge-manifest.json")));

        Assert.Equal("regression-15-v3", original.RootElement.GetProperty("version").GetString());
        Assert.Equal("judge-15-v3.1", revision.RootElement.GetProperty("version").GetString());
        var cases = revision.RootElement.GetProperty("cases").EnumerateArray().ToList();
        Assert.Equal(15, cases.Count);

        foreach (var testCase in cases)
        {
            var fileName = testCase.GetProperty("fileName").GetString()!;
            var expectedHash = testCase.GetProperty("sha256").GetString()!;
            var imagePath = Path.Combine(revisionRoot, "images", fileName);
            Assert.True(File.Exists(imagePath));
            Assert.Equal(expectedHash, Convert.ToHexString(System.Security.Cryptography.SHA256.HashData(
                File.ReadAllBytes(imagePath))));
        }

        var policyCase = cases.Single(x => x.GetProperty("id").GetString() == "R3-12");
        var expectedFacts = policyCase.GetProperty("expectedFacts");
        Assert.Equal("ESCALATE_POLICY", policyCase.GetProperty("expectedStatus").GetString());
        Assert.Equal("RCP-261005-1207", expectedFacts.GetProperty("receiptNumber").GetString());
        Assert.Equal("DV-261005-1207", expectedFacts.GetProperty("documentNumber").GetString());
    }

    [Fact]
    public void Regression_v31_entertainment_ground_truth_executes_as_policy_escalation()
    {
        using var sourceDocument = JsonDocument.Parse(File.ReadAllText(
            Path.Combine(ProjectRoot, "test_kit", "v3_1", "manifest.json")));
        var sourceCase = sourceDocument.RootElement.GetProperty("cases").EnumerateArray()
            .Single(testCase => testCase.GetProperty("id").GetString() == "R3-12");
        var expectedFacts = sourceCase.GetProperty("expected_facts");
        var facts = new ReceiptExtractionDto
        {
            EvidenceContractVersion = expectedFacts.GetProperty("evidenceContractVersion").GetInt32(),
            DocumentType = expectedFacts.GetProperty("documentType").GetString(),
            DocumentStatus = "ISSUED",
            MerchantName = expectedFacts.GetProperty("merchantName").GetString(),
            ReceiptNumber = expectedFacts.GetProperty("receiptNumber").GetString(),
            DocumentNumber = expectedFacts.GetProperty("documentNumber").GetString(),
            PosNumber = expectedFacts.GetProperty("posNumber").GetString(),
            InvoiceDate = expectedFacts.GetProperty("invoiceDate").GetString(),
            InvoiceDateEvidence = expectedFacts.GetProperty("invoiceDateEvidence").GetString(),
            Currency = expectedFacts.GetProperty("currency").GetString(),
            TotalAmount = expectedFacts.GetProperty("totalAmount").GetDecimal(),
            TotalAmountSource = expectedFacts.GetProperty("totalAmountSource").GetString(),
            TotalAmountEvidence = "TỔNG THANH TOÁN 310.000 đ",
            Confidence = 0.98,
            LineItems = sourceCase.GetProperty("items").EnumerateArray()
                .Select(item => new ReceiptLineItem
                {
                    Description = item.GetProperty("description").GetString()!,
                    Quantity = item.GetProperty("quantity").GetDecimal(),
                    UnitPrice = item.GetProperty("unitPrice").GetDecimal(),
                    Amount = item.GetProperty("amount").GetDecimal()
                }).ToList()
        };

        var decision = PolicyDecisionEngine.Evaluate(
            facts,
            sourceCase.GetProperty("claimed_amount").GetDecimal(),
            utcNow: new DateTime(2026, 10, 6, 0, 0, 0, DateTimeKind.Utc));

        Assert.Equal(sourceCase.GetProperty("expected_status").GetString(), decision.Status);
        Assert.Contains("Vé xem phim", decision.Reason, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void Extended_dataset_runner_decodes_utf8_ground_truth_explicitly()
    {
        var runner = File.ReadAllText(Path.Combine(
            ProjectRoot, "tools", "Invoke-ExtendedDatasetEvaluation.ps1"));

        Assert.Contains("function Read-Utf8Json", runner, StringComparison.Ordinal);
        Assert.Contains("[IO.File]::ReadAllText", runner, StringComparison.Ordinal);
        Assert.DoesNotContain("$manifest = Get-Content", runner, StringComparison.Ordinal);
        var measurementPath = Path.Combine(
            ProjectRoot, "tools", "Measure-ExtendedDatasetEvidence.ps1");
        Assert.True(File.Exists(measurementPath));
        var measurement = File.ReadAllText(measurementPath);
        Assert.Contains("PSObject.Properties['expectedFacts']", measurement, StringComparison.Ordinal);
        Assert.Contains("PSObject.Properties['expected_facts']", measurement, StringComparison.Ordinal);
        Assert.Contains("PSObject.Properties['sourceManifest']", measurement, StringComparison.Ordinal);
        Assert.Contains("[DateTime]::UtcNow", measurement, StringComparison.Ordinal);
    }

    [Fact]
    public void Live_verify_workflow_runner_preserves_raw_results_when_resumed()
    {
        var runnerPath = Path.Combine(ProjectRoot, "tools", "Invoke-LiveVerifyWorkflowSmoke.ps1");
        Assert.True(File.Exists(runnerPath));

        var runner = File.ReadAllText(runnerPath);
        Assert.Contains("Test-Path -LiteralPath $verifyResultsPath", runner, StringComparison.Ordinal);
        Assert.Contains("ResumedFromExistingRawResults", runner, StringComparison.Ordinal);
        Assert.Contains("if ($verifyWasResumed) { $null }", runner, StringComparison.Ordinal);
        Assert.Contains("FinalStatusAfterUndo", runner, StringComparison.Ordinal);
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
