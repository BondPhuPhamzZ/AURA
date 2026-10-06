using Xunit;

namespace AURA.Tests;

public sealed class UiContractTests
{
    private static readonly string ProjectRoot = FindProjectRoot();

    [Fact]
    public void Verify_results_keep_each_cases_facts_selectable()
    {
        var controller = File.ReadAllText(Path.Combine(ProjectRoot, "Controllers", "VerifyController.cs"));
        var dashboard = File.ReadAllText(Path.Combine(ProjectRoot, "Views", "Home", "Index.cshtml"));
        var auditView = File.ReadAllText(Path.Combine(ProjectRoot, "Views", "Shared", "Components",
            "AuditLogQueue", "Default.cshtml"));

        Assert.Contains("facts = extractedFacts", controller, StringComparison.Ordinal);
        Assert.Contains("facts-view-button", dashboard, StringComparison.Ordinal);
        Assert.Contains("showAnalysisFacts", dashboard, StringComparison.Ordinal);
        Assert.Contains("getAnalysisFactsLabel", dashboard, StringComparison.Ordinal);
        Assert.Contains("Nội dung AI —", dashboard, StringComparison.Ordinal);
        Assert.Contains("VietnamTime.FromUtc", auditView, StringComparison.Ordinal);
        Assert.DoesNotContain(".ToLocalTime()", auditView, StringComparison.Ordinal);
    }

    [Fact]
    public void Mobile_manager_actions_stay_in_the_content_column()
    {
        var queueView = File.ReadAllText(Path.Combine(ProjectRoot, "Views", "Shared", "Components",
            "EscalationQueue", "Default.cshtml"));
        var stylesheet = File.ReadAllText(Path.Combine(ProjectRoot, "wwwroot", "css", "site.css"));

        Assert.Contains(".manager-action-column > form", stylesheet, StringComparison.Ordinal);
        Assert.Contains(".manager-action-column > .decision-help", stylesheet, StringComparison.Ordinal);
        Assert.Contains("grid-column: 2", stylesheet, StringComparison.Ordinal);
        Assert.DoesNotContain("class=\"d-inline js-workflow-form\" style=\"margin-left: 8px;\"",
            queueView, StringComparison.Ordinal);
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
