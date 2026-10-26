using AURA.Controllers;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace AURA.Tests;

public sealed class HomeControllerTests
{
    [Theory]
    [InlineData(null, "applicant")]
    [InlineData("", "applicant")]
    [InlineData("unknown", "applicant")]
    [InlineData("reviewer", "reviewer")]
    [InlineData(" REVIEWER ", "reviewer")]
    [InlineData("audit", "audit")]
    [InlineData("dashboard", "dashboard")]
    [InlineData(" DASHBOARD ", "dashboard")]
    public void IndexSelectsSafeInitialTab(string? requestedTab, string expectedTab)
    {
        var controller = new HomeController(NullLogger<HomeController>.Instance);

        var result = Assert.IsType<ViewResult>(controller.Index(requestedTab));

        Assert.Null(result.ViewName);
        Assert.Equal(expectedTab, controller.ViewData["InitialTab"]);
    }

    [Fact]
    public void IndexAcceptsAValidDashboardPeriod()
    {
        var controller = new HomeController(NullLogger<HomeController>.Instance);

        _ = controller.Index("dashboard", "2026-09");

        Assert.Equal(2026, controller.ViewData["DashboardYear"]);
        Assert.Equal(9, controller.ViewData["DashboardMonth"]);
    }

    [Theory]
    [InlineData("2026-00")]
    [InlineData("2026-13")]
    [InlineData("not-a-period")]
    public void IndexIgnoresAnInvalidDashboardPeriod(string period)
    {
        var controller = new HomeController(NullLogger<HomeController>.Instance);

        _ = controller.Index("dashboard", period);

        Assert.False(controller.ViewData.ContainsKey("DashboardYear"));
        Assert.False(controller.ViewData.ContainsKey("DashboardMonth"));
    }

    [Theory]
    [InlineData("2026-09", 2026, 9)]
    [InlineData("2000-01", 2000, 1)]
    [InlineData("2100-12", 2100, 12)]
    [InlineData("2026-13", null, null)]
    [InlineData("1999-12", null, null)]
    [InlineData(null, null, null)]
    public void DashboardFragmentUsesTheSameValidatedPeriodAsTheFullPage(string? value, int? year, int? month)
    {
        var controller = new HomeController(NullLogger<HomeController>.Instance);
        var result = Assert.IsType<ViewComponentResult>(controller.DashboardSummary(value));

        Assert.Equal("DashboardSummary", result.ViewComponentName);
        var args = new Microsoft.AspNetCore.Routing.RouteValueDictionary(result.Arguments);
        Assert.Equal(year, args["year"]);
        Assert.Equal(month, args["month"]);
    }
}
