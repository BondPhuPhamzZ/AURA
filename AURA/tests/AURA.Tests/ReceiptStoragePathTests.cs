using AURA.Services;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.FileProviders;
using Xunit;

namespace AURA.Tests;

public sealed class ReceiptStoragePathTests
{
    [Fact]
    public void Default_private_storage_is_outside_webroot()
    {
        var environment = NewEnvironment();
        Assert.Equal(Path.GetFullPath(Path.Combine(environment.ContentRootPath, "App_Data", "receipts")),
            ReceiptStoragePath.ResolveRoot(environment, "App_Data/receipts"));
    }

    [Theory]
    [InlineData("wwwroot")]
    [InlineData("wwwroot/receipts")]
    [InlineData("wwwroot/../wwwroot/receipts")]
    public void Static_file_storage_configuration_is_rejected(string directory)
    {
        var environment = NewEnvironment();
        Assert.Throws<InvalidOperationException>(() =>
            ReceiptStoragePath.ResolveRoot(environment, directory));
    }

    [Fact]
    public void Sibling_with_similar_prefix_is_not_rejected()
    {
        var environment = NewEnvironment();
        Assert.Equal(Path.GetFullPath(Path.Combine(environment.ContentRootPath, "wwwroot-private")),
            ReceiptStoragePath.ResolveRoot(environment, "wwwroot-private"));
    }

    [Theory]
    [InlineData("../receipt.jpg")]
    [InlineData("sub/receipt.jpg")]
    [InlineData("sub\\receipt.jpg")]
    [InlineData("receipt.jpg:stream")]
    [InlineData("..")]
    public void Stored_file_name_must_be_a_single_plain_component(string storedFileName)
    {
        Assert.Throws<InvalidOperationException>(() =>
            ReceiptStoragePath.ResolveFile(Path.GetTempPath(), storedFileName));
    }

    [Fact]
    public void Legacy_guid_and_current_hex_file_names_remain_valid()
    {
        var root = Path.GetTempPath();
        Assert.Equal(Path.Combine(root, "d85b4431-d123-41c7-bc8f-1337c31f8af1.jpg"),
            ReceiptStoragePath.ResolveFile(root, "d85b4431-d123-41c7-bc8f-1337c31f8af1.jpg"));
        Assert.Equal(Path.Combine(root, "d85b4431d12341c7bc8f1337c31f8af1.png"),
            ReceiptStoragePath.ResolveFile(root, "d85b4431d12341c7bc8f1337c31f8af1.png"));
    }

    private static TestEnvironment NewEnvironment()
    {
        var root = Path.GetFullPath(Path.Combine(Path.GetTempPath(), "aura-storage-boundary-test"));
        return new TestEnvironment
        {
            ContentRootPath = root,
            WebRootPath = Path.Combine(root, "wwwroot")
        };
    }

    private sealed class TestEnvironment : IWebHostEnvironment
    {
        public string ApplicationName { get; set; } = "AURA.Tests";
        public IFileProvider WebRootFileProvider { get; set; } = new NullFileProvider();
        public string WebRootPath { get; set; } = string.Empty;
        public string EnvironmentName { get; set; } = "Development";
        public string ContentRootPath { get; set; } = string.Empty;
        public IFileProvider ContentRootFileProvider { get; set; } = new NullFileProvider();
    }
}
