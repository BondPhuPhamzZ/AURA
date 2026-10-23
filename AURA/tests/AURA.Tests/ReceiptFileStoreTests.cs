using System.Security.Cryptography;
using AURA.Options;
using AURA.Services;
using AURA.Tests.Fakes;
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

    [Fact]
    public async Task Gcs_store_uses_create_only_and_reuses_only_matching_content()
    {
        var client = new FakeGoogleStorageClient();
        var store = new GoogleCloudReceiptFileStore(client, "test-bucket", "private/receipts", 1024);
        byte[] bytes = [9, 8, 7, 6];
        var hash = Convert.ToHexString(SHA256.HashData(bytes));

        Assert.True(await store.PutIfAbsentAsync("case.jpg", bytes, "image/jpeg", hash,
            CancellationToken.None));
        Assert.False(await store.PutIfAbsentAsync("case.jpg", bytes, "image/jpeg", hash,
            CancellationToken.None));
        await using var stored = await store.OpenReadAsync("case.jpg", CancellationToken.None);
        Assert.NotNull(stored);
        Assert.Equal(bytes, await ReadAllAsync(stored!));
        Assert.Equal("private/receipts/case.jpg", client.LastObjectName);
        Assert.True(client.LastDownloadWasGenerationPinned);

        byte[] different = [1, 1, 1, 1];
        await Assert.ThrowsAsync<InvalidOperationException>(() => store.PutIfAbsentAsync(
            "case.jpg", different, "image/jpeg",
            Convert.ToHexString(SHA256.HashData(different)), CancellationToken.None));
    }

    [Fact]
    public async Task Gcs_store_rejects_oversized_metadata_before_downloading_bytes()
    {
        var client = new FakeGoogleStorageClient();
        client.Seed("private/receipts/large.jpg", new byte[1025]);
        var store = new GoogleCloudReceiptFileStore(client, "test-bucket", "private/receipts", 1024);

        await Assert.ThrowsAsync<InvalidDataException>(() => store.OpenReadAsync("large.jpg",
            CancellationToken.None));
        Assert.False(client.DownloadCalled);
    }

    [Fact]
    public async Task Gcs_store_stops_download_if_object_grows_after_metadata_check()
    {
        var client = new FakeGoogleStorageClient();
        client.Seed("private/receipts/raced.jpg", new byte[1025]);
        client.AdvertisedSizeOverride = 1024;
        var store = new GoogleCloudReceiptFileStore(client, "test-bucket", "private/receipts", 1024);

        await Assert.ThrowsAsync<InvalidDataException>(() => store.OpenReadAsync("raced.jpg",
            CancellationToken.None));
        Assert.True(client.DownloadCalled);
    }

    [Fact]
    public void Gcs_configuration_accepts_explicit_ADC_without_a_key_file()
    {
        var options = new GoogleCloudReceiptStorageOptions
        {
            Bucket = "aura-receipts-test",
            Prefix = "aura/receipts",
            CredentialMode = GoogleCloudReceiptStorageOptions.ApplicationDefault
        };

        Assert.True(GoogleCloudReceiptStorageConfiguration.IsValid(options,
            Path.GetTempPath(), isProviderEnabled: true));
        Assert.False(GoogleCloudReceiptStorageConfiguration.IsValid(new GoogleCloudReceiptStorageOptions
        {
            Bucket = options.Bucket,
            Prefix = options.Prefix,
            CredentialMode = options.CredentialMode,
            ServiceAccountJsonPath = "unexpected.json"
        }, Path.GetTempPath(), isProviderEnabled: true));
    }

    [Fact]
    public void Gcs_ADC_environment_credential_path_must_be_external_to_the_app_root()
    {
        var root = NewRoot();
        try
        {
            var appRoot = Path.Combine(root, "app");
            var externalKey = Path.Combine(root, "private", "adc-credential.json");
            var inAppKey = Path.Combine(appRoot, "adc-credential.json");
            Directory.CreateDirectory(appRoot);
            Directory.CreateDirectory(Path.GetDirectoryName(externalKey)!);
            File.WriteAllText(externalKey, "external test fixture");
            File.WriteAllText(inAppKey, "in-app test fixture");
            var options = new GoogleCloudReceiptStorageOptions
            {
                Bucket = "aura-receipts-test",
                Prefix = "aura/receipts",
                CredentialMode = GoogleCloudReceiptStorageOptions.ApplicationDefault
            };

            Assert.True(GoogleCloudReceiptStorageConfiguration.IsValid(options, appRoot,
                isProviderEnabled: true, applicationCredentialsPath: externalKey));
            Assert.False(GoogleCloudReceiptStorageConfiguration.IsValid(options, appRoot,
                isProviderEnabled: true, applicationCredentialsPath: inAppKey));
            Assert.False(GoogleCloudReceiptStorageConfiguration.IsValid(options, appRoot,
                isProviderEnabled: true, applicationCredentialsPath: Path.Combine(root, "missing.json")));
        }
        finally
        {
            DeleteRoot(root);
        }
    }

    [Fact]
    public void Gcs_configuration_does_not_require_credentials_when_cloud_storage_is_not_selected()
    {
        Assert.True(GoogleCloudReceiptStorageConfiguration.IsValid(
            new GoogleCloudReceiptStorageOptions(), Path.GetTempPath(), isProviderEnabled: false));
    }

    [Fact]
    public void Gcs_configuration_requires_JSON_key_to_exist_outside_the_app_root()
    {
        var root = NewRoot();
        try
        {
            var appRoot = Path.Combine(root, "app");
            var externalKey = Path.Combine(root, "private", "service-account.json");
            Directory.CreateDirectory(appRoot);
            Directory.CreateDirectory(Path.GetDirectoryName(externalKey)!);
            File.WriteAllText(externalKey, "test fixture, not a credential");
            var options = new GoogleCloudReceiptStorageOptions
            {
                Bucket = "aura-receipts-test",
                Prefix = "aura/receipts",
                CredentialMode = GoogleCloudReceiptStorageOptions.JsonServiceAccount,
                ServiceAccountJsonPath = externalKey
            };

            Assert.True(GoogleCloudReceiptStorageConfiguration.IsValid(options, appRoot,
                isProviderEnabled: true));
            Assert.False(GoogleCloudReceiptStorageConfiguration.IsValid(new GoogleCloudReceiptStorageOptions
            {
                Bucket = options.Bucket,
                Prefix = options.Prefix,
                CredentialMode = options.CredentialMode,
                ServiceAccountJsonPath = Path.Combine(appRoot, "service-account.json")
            }, appRoot, isProviderEnabled: true));
            Assert.False(GoogleCloudReceiptStorageConfiguration.IsValid(new GoogleCloudReceiptStorageOptions
            {
                Bucket = options.Bucket,
                Prefix = options.Prefix,
                CredentialMode = options.CredentialMode,
                ServiceAccountJsonPath = Path.Combine(root, "missing.json")
            }, appRoot, isProviderEnabled: true));
        }
        finally
        {
            DeleteRoot(root);
        }
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
