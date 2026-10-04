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
    public void IndexSelectsSafeInitialTab(string? requestedTab, string expectedTab)
    {
        var controller = new HomeController(NullLogger<HomeController>.Instance);

        var result = Assert.IsType<ViewResult>(controller.Index(requestedTab));

        Assert.Null(result.ViewName);
        Assert.Equal(expectedTab, controller.ViewData["InitialTab"]);
    }
}
