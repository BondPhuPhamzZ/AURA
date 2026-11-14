using System.ComponentModel.DataAnnotations;
using AURA.Models;
using AURA.ViewModels;
using Xunit;

namespace AURA.Tests;

public sealed class ReceiptSubmitterInputTests
{
    private static readonly string ProjectRoot = FindProjectRoot();

    [Fact]
    public void Demo_defaults_satisfy_the_declared_rules()
    {
        Assert.Empty(Validate(ReceiptSubmitterInput.CreateDemo()));
    }

    [Theory]
    [InlineData(null, "Vui lòng nhập Mã nhân viên.")]
    [InlineData("", "Vui lòng nhập Mã nhân viên.")]
    [InlineData("   ", "Vui lòng nhập Mã nhân viên.")]
    public void Missing_submitter_code_is_rejected_with_a_vietnamese_message(string? value, string expected)
    {
        var input = ReceiptSubmitterInput.CreateDemo();
        input.SubmitterCode = value;

        var error = Assert.Single(Validate(input));
        Assert.Equal(expected, error.ErrorMessage);
    }

    [Fact]
    public void Lengths_match_the_sql_columns()
    {
        var input = ReceiptSubmitterInput.CreateDemo();
        input.SubmitterCode = new string('A', ReimbursementRequest.SubmitterCodeMaxLength);
        input.SubmitterDisplayName = new string('B', ReimbursementRequest.SubmitterDisplayNameMaxLength);
        input.SubmitterDepartment = new string('C', ReimbursementRequest.SubmitterDepartmentMaxLength);
        Assert.Empty(Validate(input));

        input.SubmitterDepartment += "C";
        var error = Assert.Single(Validate(input));
        Assert.Equal("Bộ phận không được vượt quá 200 ký tự.", error.ErrorMessage);
    }

    [Fact]
    public void Upload_form_takes_its_rules_from_the_model_instead_of_hand_written_html()
    {
        var dashboard = File.ReadAllText(Path.Combine(ProjectRoot, "Views", "Home", "Index.cshtml"));
        var fields = File.ReadAllText(Path.Combine(ProjectRoot, "Views", "Home", "_SubmitterContextFields.cshtml"));
        var controller = File.ReadAllText(Path.Combine(ProjectRoot, "Controllers", "ApplicantController.cs"));

        Assert.Contains("<partial name=\"_SubmitterContextFields\"", dashboard, StringComparison.Ordinal);
        Assert.Contains("@model AURA.ViewModels.ReceiptSubmitterInput", fields, StringComparison.Ordinal);
        foreach (var property in new[] { "SubmitterCode", "SubmitterDisplayName", "SubmitterDepartment" })
        {
            Assert.Contains($"<label asp-for=\"{property}\"", fields, StringComparison.Ordinal);
            Assert.Contains($"<input asp-for=\"{property}\"", fields, StringComparison.Ordinal);
        }

        Assert.DoesNotContain("maxlength=", fields, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain(" required", fields, StringComparison.Ordinal);
        Assert.DoesNotContain("name=\"submitter", dashboard, StringComparison.Ordinal);
        Assert.Contains("[data-val-required]", dashboard, StringComparison.Ordinal);

        Assert.Contains("ReceiptSubmitterInput submitter", controller, StringComparison.Ordinal);
        var action = controller[controller.IndexOf("UploadReceipt(", StringComparison.Ordinal)..];
        var modelStateCheck = action.IndexOf("if (!ModelState.IsValid)", StringComparison.Ordinal);
        var firstFileRead = action.IndexOf("ReadExactlyAsync", StringComparison.Ordinal);
        Assert.True(modelStateCheck >= 0 && firstFileRead > modelStateCheck,
            "ModelState must be checked before the upload is read or queued.");
    }

    private static List<ValidationResult> Validate(object model)
    {
        var results = new List<ValidationResult>();
        Validator.TryValidateObject(model, new ValidationContext(model), results, validateAllProperties: true);
        return results;
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
