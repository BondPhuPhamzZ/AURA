using System.Net;
using System.Security.Cryptography;
using AURA.Interfaces;
using AURA.Options;
using Google;
using Google.Apis.Auth.OAuth2;
using Google.Cloud.Storage.V1;
using Microsoft.Extensions.Options;

namespace AURA.Services;

public sealed class GoogleCloudReceiptFileStore : IReceiptFileStore
{
    private readonly StorageClient _client;
    private readonly string _bucket;
    private readonly string _prefix;
    private readonly long _maxBytes;

    public GoogleCloudReceiptFileStore(IOptions<GoogleCloudReceiptStorageOptions> options,
        IOptions<ReceiptStorageOptions> storageOptions)
        : this(CreateClient(options.Value), options.Value.Bucket, options.Value.Prefix,
            storageOptions.Value.MaxFileSizeMb * 1024L * 1024L)
    {
    }

    internal GoogleCloudReceiptFileStore(StorageClient client, string bucket, string prefix, long maxBytes)
    {
        _bucket = bucket;
        _prefix = prefix.Trim('/');
        _maxBytes = maxBytes;
        _client = client;
    }

    private static StorageClient CreateClient(GoogleCloudReceiptStorageOptions options)
    {
        var serviceAccount = CredentialFactory.FromFile<ServiceAccountCredential>(options.ServiceAccountJsonPath);
        var credential = GoogleCredential.FromServiceAccountCredential(serviceAccount)
            .CreateScoped("https://www.googleapis.com/auth/devstorage.read_write");
        return StorageClient.Create(credential);
    }

    public async Task<bool> PutIfAbsentAsync(string storedFileName, ReadOnlyMemory<byte> bytes,
        string contentType, string sha256, CancellationToken cancellationToken)
    {
        var objectName = Resolve(storedFileName);
        try
        {
            await using var input = new MemoryStream(bytes.ToArray(), writable: false);
            await _client.UploadObjectAsync(_bucket, objectName, contentType, input,
                new UploadObjectOptions { IfGenerationMatch = 0 }, cancellationToken);
            return true;
        }
        catch (GoogleApiException exception) when (exception.HttpStatusCode == HttpStatusCode.PreconditionFailed)
        {
            var existing = await DownloadAsync(objectName, cancellationToken, bytes.Length);
            if (existing.LongLength != bytes.Length || !string.Equals(
                    Convert.ToHexString(SHA256.HashData(existing)), sha256, StringComparison.OrdinalIgnoreCase))
                throw new InvalidOperationException("Existing receipt object has conflicting content.", exception);
            return false;
        }
    }

    public async Task<Stream?> OpenReadAsync(string storedFileName, CancellationToken cancellationToken)
    {
        var objectName = Resolve(storedFileName);
        try
        {
            var bytes = await DownloadAsync(objectName, cancellationToken, _maxBytes);
            return new MemoryStream(bytes, writable: false);
        }
        catch (GoogleApiException exception) when (exception.HttpStatusCode == HttpStatusCode.NotFound)
        {
            return null;
        }
    }

    public async Task DeleteAsync(string storedFileName, CancellationToken cancellationToken)
    {
        try
        {
            await _client.DeleteObjectAsync(_bucket, Resolve(storedFileName),
                cancellationToken: cancellationToken);
        }
        catch (GoogleApiException exception) when (exception.HttpStatusCode == HttpStatusCode.NotFound)
        {
            // Idempotent cleanup.
        }
    }

    public async Task<bool> IsAvailableAsync(CancellationToken cancellationToken)
    {
        try
        {
            await foreach (var _ in _client.ListObjectsAsync(_bucket, _prefix,
                               new ListObjectsOptions { PageSize = 1 })
                           .WithCancellation(cancellationToken))
                break;
            return true;
        }
        catch (Exception exception) when (exception is GoogleApiException or HttpRequestException or TimeoutException)
        {
            return false;
        }
    }

    private async Task<byte[]> DownloadAsync(string objectName, CancellationToken cancellationToken,
        long maximumBytes)
    {
        var metadata = await _client.GetObjectAsync(_bucket, objectName,
            cancellationToken: cancellationToken);
        var objectSize = metadata.Size ?? 0UL;
        var generation = metadata.Generation ?? 0L;
        if (objectSize == 0 || objectSize > (ulong)maximumBytes || objectSize > int.MaxValue || generation <= 0)
            throw new InvalidDataException("Receipt object metadata is invalid or exceeds the configured size limit.");
        await using var output = new BoundedMemoryStream(checked((int)objectSize));
        await _client.DownloadObjectAsync(_bucket, objectName, output,
            new DownloadObjectOptions { Generation = generation },
            cancellationToken);
        if ((ulong)output.Length != objectSize)
            throw new InvalidDataException("Receipt object changed while it was being read.");
        return output.ToArray();
    }

    private string Resolve(string storedFileName)
    {
        _ = ReceiptStoragePath.ResolveFile(Path.GetTempPath(), storedFileName);
        return string.IsNullOrEmpty(_prefix) ? storedFileName : $"{_prefix}/{storedFileName}";
    }

    private sealed class BoundedMemoryStream : MemoryStream
    {
        private readonly int _maximumLength;

        public BoundedMemoryStream(int maximumLength) : base(maximumLength) =>
            _maximumLength = maximumLength;

        public override void Write(byte[] buffer, int offset, int count)
        {
            EnsureCapacityFor(count);
            base.Write(buffer, offset, count);
        }

        public override void Write(ReadOnlySpan<byte> buffer)
        {
            EnsureCapacityFor(buffer.Length);
            base.Write(buffer);
        }

        public override Task WriteAsync(byte[] buffer, int offset, int count,
            CancellationToken cancellationToken)
        {
            EnsureCapacityFor(count);
            return base.WriteAsync(buffer, offset, count, cancellationToken);
        }

        public override ValueTask WriteAsync(ReadOnlyMemory<byte> buffer,
            CancellationToken cancellationToken = default)
        {
            EnsureCapacityFor(buffer.Length);
            return base.WriteAsync(buffer, cancellationToken);
        }

        private void EnsureCapacityFor(int count)
        {
            if (count < 0 || Length + count > _maximumLength)
                throw new InvalidDataException("Receipt object exceeds its advertised size.");
        }
    }
}
