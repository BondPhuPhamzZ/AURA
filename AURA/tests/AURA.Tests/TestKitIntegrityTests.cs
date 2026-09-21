using System.Text.Json;
using AURA.Models;
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
