using System.Security.Cryptography;
using AURA.Options;
using AURA.Services;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.FileProviders;
using Microsoft.Extensions.Options;
using Xunit;

namespace AURA.Tests;

public sealed class ReceiptFileStoreTests
{
    [Fact]
    public async Task Local_store_put_is_idempotent_and_never_overwrites_conflicting_bytes()
    {
        var root = NewRoot();
        try
        {
            var store = CreateStore(root);
            byte[] first = [1, 2, 3, 4];
            var hash = Convert.ToHexString(SHA256.HashData(first));

            Assert.True(await store.PutIfAbsentAsync("receipt.jpg", first, "image/jpeg", hash,
                CancellationToken.None));
            Assert.False(await store.PutIfAbsentAsync("receipt.jpg", first, "image/jpeg", hash,
                CancellationToken.None));
            await using var stream = await store.OpenReadAsync("receipt.jpg", CancellationToken.None);
            Assert.NotNull(stream);
            Assert.Equal(first, await ReadAllAsync(stream!));
            await Assert.ThrowsAsync<InvalidOperationException>(() => store.PutIfAbsentAsync(
                "receipt.jpg", new byte[] { 4, 3, 2, 1 }, "image/jpeg",
                Convert.ToHexString(SHA256.HashData(new byte[] { 4, 3, 2, 1 })), CancellationToken.None));
            Assert.True(await store.IsAvailableAsync(CancellationToken.None));
        }
        finally
        {
            DeleteRoot(root);
        }
    }

    [Fact]
    public async Task Integrity_reader_rejects_data_larger_than_the_database_size_before_buffering_it()
    {
        await using var source = new MemoryStream(new byte[] { 1, 2, 3, 4 });
        var expectedHash = Convert.ToHexString(SHA256.HashData(new byte[] { 1, 2, 3 }));

        await Assert.ThrowsAsync<VisionExtractionException>(() => ReceiptStoredFileIntegrity.ReadVerifiedAsync(
            source, 3, expectedHash, 5, CancellationToken.None));
    }

    private static LocalReceiptFileStore CreateStore(string root)
    {
        var options = Microsoft.Extensions.Options.Options.Create(
            new ReceiptStorageOptions { Directory = "private-receipts" });
        return new LocalReceiptFileStore(new TestEnvironment(root), options);
    }

    private static async Task<byte[]> ReadAllAsync(Stream stream)
    {
        await using var result = new MemoryStream();
        await stream.CopyToAsync(result);
        return result.ToArray();
    }

    private static string NewRoot()
    {
        var root = Path.Combine(Path.GetTempPath(), "aura-receipt-store-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(root);
        return root;
    }

    private static void DeleteRoot(string root)
    {
        if (Path.GetDirectoryName(root)?.TrimEnd(Path.DirectorySeparatorChar) !=
                Path.GetTempPath().TrimEnd(Path.DirectorySeparatorChar) ||
            !Path.GetFileName(root).StartsWith("aura-receipt-store-", StringComparison.Ordinal))
            throw new InvalidOperationException("Refusing to delete outside the dedicated test temp root.");
        Directory.Delete(root, recursive: true);
    }

    private sealed class TestEnvironment(string root) : IWebHostEnvironment
    {
        public string ApplicationName { get; set; } = "AURA.Tests";
        public IFileProvider WebRootFileProvider { get; set; } = new NullFileProvider();
        public string WebRootPath { get; set; } = Path.Combine(root, "wwwroot");
        public string EnvironmentName { get; set; } = "Development";
        public string ContentRootPath { get; set; } = root;
        public IFileProvider ContentRootFileProvider { get; set; } = new NullFileProvider();
    }
}
