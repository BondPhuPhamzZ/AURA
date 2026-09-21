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
