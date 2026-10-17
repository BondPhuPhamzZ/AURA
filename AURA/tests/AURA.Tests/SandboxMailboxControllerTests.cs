using AURA.Controllers;
using AURA.Interfaces;
using AURA.Options;
using AURA.Services;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Options;
using Xunit;

namespace AURA.Tests;

public sealed class SandboxMailboxControllerTests
{
    [Fact]
    public async Task Delivery_endpoint_is_not_available_when_feature_flag_is_off()
    {
        var controller = CreateController(new MailboxSandboxOptions { Enabled = false });

        var result = await controller.Deliver(new SandboxMailboxDelivery(), CancellationToken.None);

        Assert.IsType<NotFoundObjectResult>(result);
    }

    [Fact]
    public async Task Delivery_rejects_a_missing_or_invalid_api_key_before_reading_attachment()
    {
        var options = EnabledOptions();
        var controller = CreateController(options);

        var result = await controller.Deliver(new SandboxMailboxDelivery(), CancellationToken.None);

        Assert.IsType<UnauthorizedObjectResult>(result);
    }

    [Fact]
    public void Status_is_explicitly_sandbox_only()
    {
        var controller = CreateController(EnabledOptions());

        var result = Assert.IsType<OkObjectResult>(controller.Status());
        var json = System.Text.Json.JsonSerializer.Serialize(result.Value);

        Assert.Contains("synthetic-sandbox-only", json, StringComparison.Ordinal);
        Assert.Contains("\"gmailConnected\":false", json, StringComparison.Ordinal);
        Assert.Contains("\"productionReady\":false", json, StringComparison.Ordinal);
    }

    private static SandboxMailboxController CreateController(MailboxSandboxOptions options)
    {
        var controller = new SandboxMailboxController(
            Microsoft.Extensions.Options.Options.Create(options), new FailingIngestionService());
        controller.ControllerContext = new ControllerContext { HttpContext = new DefaultHttpContext() };
        return controller;
    }

    private static MailboxSandboxOptions EnabledOptions() => new()
    {
        Enabled = true,
        ApiKey = "0123456789abcdefghijklmn",
        AllowedFolder = "AURA_INVOICE",
        AllowedSenders = ["demo.sender@example.test"]
    };

    private sealed class FailingIngestionService : IInvoiceIngestionService
    {
        public Task<InvoiceIngestionResult> IngestAsync(InvoiceIngestionCommand command,
            CancellationToken cancellationToken = default) =>
            throw new InvalidOperationException("The guard should reject before ingestion.");
    }
}
