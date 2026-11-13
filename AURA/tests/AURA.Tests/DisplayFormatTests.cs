using System.Globalization;
using AURA.Services;
using Xunit;

namespace AURA.Tests;

public sealed class DisplayFormatTests
{
    private static readonly string ProjectRoot = FindProjectRoot();

    [Theory]
    [InlineData("en-US")]
    [InlineData("de-DE")]
    [InlineData("fr-FR")]
    [InlineData("vi-VN")]
    public void Amounts_and_dates_ignore_the_host_culture(string hostCulture)
    {
        var original = CultureInfo.CurrentCulture;
        try
        {
            CultureInfo.CurrentCulture = CultureInfo.GetCultureInfo(hostCulture);

            // Matches Intl.NumberFormat('vi-VN') used by the browser-rendered rows.
            Assert.Equal("2.999.039 VND", DisplayFormat.Vnd(2_999_039m));
            Assert.Equal("0 VND", DisplayFormat.Vnd(0m));
            Assert.Equal("12.345", DisplayFormat.Number(12_345L));
            var timestamp = new DateTime(2026, 10, 27, 15, 22, 36);
            Assert.Equal("27/10/2026", DisplayFormat.Date(timestamp));
            Assert.Equal("27/10/2026", DisplayFormat.Date(DateOnly.FromDateTime(timestamp)));
            Assert.Equal("15:22:36", DisplayFormat.Time(timestamp));
            Assert.Equal("27/10/2026 15:22:36", DisplayFormat.DateTime(timestamp));
        }
        finally
        {
            CultureInfo.CurrentCulture = original;
        }
    }

    [Fact]
    public void Views_do_not_format_amounts_or_dates_with_the_host_culture()
    {
        var views = Directory.EnumerateFiles(Path.Combine(ProjectRoot, "Views"), "*.cshtml",
            SearchOption.AllDirectories);
        foreach (var view in views)
        {
            var source = File.ReadAllText(view);
            Assert.DoesNotContain(".ToString(\"N0\")", source, StringComparison.Ordinal);
            Assert.DoesNotContain(":N0}", source, StringComparison.Ordinal);
            Assert.DoesNotContain(".ToString(\"dd/MM/yyyy", source, StringComparison.Ordinal);
            Assert.DoesNotContain(".ToString(\"HH:mm", source, StringComparison.Ordinal);
        }
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
