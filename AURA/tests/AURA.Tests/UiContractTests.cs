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

    [Fact]
    public void Upload_and_verify_scroll_to_the_live_results_table_when_processing_starts()
    {
        var dashboard = File.ReadAllText(Path.Combine(ProjectRoot, "Views", "Home", "Index.cshtml"));

        Assert.Contains("id=\"unifiedResultsSection\"", dashboard, StringComparison.Ordinal);
        Assert.Contains("resultsSection.scrollIntoView({ behavior: 'smooth', block: 'start' })",
            dashboard, StringComparison.Ordinal);

        var uploadStart = dashboard.IndexOf("async function submitUpload()", StringComparison.Ordinal);
        var verifyStart = dashboard.IndexOf("async function startVerifyHarness()", StringComparison.Ordinal);
        var workflowFormsStart = dashboard.IndexOf("document.addEventListener('submit'", StringComparison.Ordinal);
        Assert.True(uploadStart >= 0 && verifyStart > uploadStart && workflowFormsStart > verifyStart);

        var uploadFunction = dashboard[uploadStart..verifyStart];
        var verifyFunction = dashboard[verifyStart..workflowFormsStart];
        Assert.Contains("processingPlaceholderRows = files.length;", uploadFunction, StringComparison.Ordinal);
        Assert.Contains("formData.append('receiptFile', file, file.name)", uploadFunction, StringComparison.Ordinal);
        Assert.Contains("aura.pendingReceipts", uploadFunction, StringComparison.Ordinal);
        var uploadRender = uploadFunction.IndexOf("renderResultsTable();", StringComparison.Ordinal);
        var uploadScroll = uploadFunction.IndexOf("scrollToResults();", StringComparison.Ordinal);
        Assert.True(uploadRender >= 0 && uploadScroll > uploadRender);
        Assert.Contains("processingPlaceholderRows = 5;", verifyFunction, StringComparison.Ordinal);
        var verifyRender = verifyFunction.IndexOf("renderResultsTable();", StringComparison.Ordinal);
        var verifyScroll = verifyFunction.IndexOf("scrollToResults();", StringComparison.Ordinal);
        Assert.True(verifyRender >= 0 && verifyScroll > verifyRender);
    }

    [Fact]
    public void Dashboard_and_batch_upload_are_exposed_without_a_second_processing_pipeline()
    {
        var dashboard = File.ReadAllText(Path.Combine(ProjectRoot, "Views", "Home", "Index.cshtml"));
        var layout = File.ReadAllText(Path.Combine(ProjectRoot, "Views", "Shared", "_Layout.cshtml"));
        var controller = File.ReadAllText(Path.Combine(ProjectRoot, "Controllers", "ApplicantController.cs"));

        Assert.Contains("name=\"receiptFile\" accept=\".jpg,.jpeg,.png\" multiple", dashboard,
            StringComparison.Ordinal);
        Assert.Contains("const maxBatchFiles = 10;", dashboard, StringComparison.Ordinal);
        Assert.Contains("asp-route-tab=\"dashboard\"", File.ReadAllText(Path.Combine(ProjectRoot,
            "Views", "Shared", "Components", "DashboardSummary", "Default.cshtml")), StringComparison.Ordinal);
        var dashboardSummary = File.ReadAllText(Path.Combine(ProjectRoot, "Views", "Shared", "Components",
            "DashboardSummary", "Default.cshtml"));
        Assert.Contains("if (!Model.IsAvailable)", dashboardSummary, StringComparison.Ordinal);
        Assert.Contains("Không dùng các giá trị 0 thay cho dữ liệu chưa tải được", dashboardSummary,
            StringComparison.Ordinal);
        Assert.Contains("không phải tổng đã phê duyệt", dashboardSummary, StringComparison.Ordinal);
        Assert.Contains("Bản demo có giám sát", dashboard, StringComparison.Ordinal);
        Assert.Contains("mỗi ảnh tạo một hồ sơ độc lập", dashboard, StringComparison.Ordinal);
        Assert.Contains("btn-tab-dashboard", layout, StringComparison.Ordinal);
        Assert.Contains("decimal? claimedAmount", controller, StringComparison.Ordinal);
        Assert.DoesNotContain("UploadReceiptBatch", controller, StringComparison.Ordinal);
    }

    [Fact]
    public void Evidence_overlay_prototype_is_hash_bound_normalized_and_fail_closed()
    {
        var prototypeRoot = Path.Combine(ProjectRoot, "tools", "evidence_overlay_prototype");
        var html = File.ReadAllText(Path.Combine(prototypeRoot, "index.html"));
        var imagePath = Path.Combine(prototypeRoot, "synthetic-receipt.svg");
        var imageBytes = File.ReadAllBytes(imagePath);
        var imageHash = Convert.ToHexString(System.Security.Cryptography.SHA256.HashData(imageBytes));

        const string openingTag = "<script type=\"application/json\" id=\"evidence-data\">";
        const string closingTag = "</script>";
        var jsonStart = html.IndexOf(openingTag, StringComparison.Ordinal);
        Assert.True(jsonStart >= 0);
        jsonStart += openingTag.Length;
        var jsonEnd = html.IndexOf(closingTag, jsonStart, StringComparison.Ordinal);
        Assert.True(jsonEnd > jsonStart);

        using var document = System.Text.Json.JsonDocument.Parse(html[jsonStart..jsonEnd]);
        var root = document.RootElement;
        Assert.Equal(imageHash, root.GetProperty("imageSha256").GetString());
        Assert.Equal(1000, root.GetProperty("coordinateSpace").GetProperty("width").GetInt32());
        Assert.Equal(1000, root.GetProperty("coordinateSpace").GetProperty("height").GetInt32());
        Assert.True(root.GetProperty("coordinateSpace").GetProperty("orientationApplied").GetBoolean());

        var totalRegions = root.GetProperty("regions").EnumerateArray()
            .Where(region => region.GetProperty("field").GetString() == "totalAmount")
            .ToArray();
        var totalRegion = Assert.Single(totalRegions);
        Assert.Contains("58.000 VND", totalRegion.GetProperty("text").GetString(), StringComparison.Ordinal);
        Assert.DoesNotContain("74.000", totalRegion.GetProperty("text").GetString(), StringComparison.Ordinal);

        foreach (var region in root.GetProperty("regions").EnumerateArray())
        {
            var box = region.GetProperty("box");
            var x = box.GetProperty("x").GetDouble();
            var y = box.GetProperty("y").GetDouble();
            var width = box.GetProperty("width").GetDouble();
            var height = box.GetProperty("height").GetDouble();
            Assert.InRange(x, 0, 999);
            Assert.InRange(y, 0, 999);
            Assert.True(width > 0 && height > 0 && x + width <= 1000 && y + height <= 1000);
        }

        Assert.Contains("hệ thống không vẽ hộp suy đoán", html, StringComparison.Ordinal);
        Assert.Contains("overlay đã fail-closed", html, StringComparison.Ordinal);
        Assert.Contains("overlay.toggleAttribute('hidden', shouldHide)", html, StringComparison.Ordinal);
        Assert.Contains("TAM TINH", File.ReadAllText(imagePath), StringComparison.Ordinal);
        Assert.Contains("TONG THANH TOAN", File.ReadAllText(imagePath), StringComparison.Ordinal);
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
