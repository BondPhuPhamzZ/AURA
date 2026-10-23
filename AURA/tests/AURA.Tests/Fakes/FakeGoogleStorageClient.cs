using Google;
using Google.Cloud.Storage.V1;
using StorageObject = Google.Apis.Storage.v1.Data.Object;

namespace AURA.Tests.Fakes;

internal sealed class FakeGoogleStorageClient : StorageClient
{
    private readonly Dictionary<string, byte[]> _objects = new(StringComparer.Ordinal);
    private const long CurrentGeneration = 17;

    public string? LastObjectName { get; private set; }
    public bool LastDownloadWasGenerationPinned { get; private set; }
    public bool DownloadCalled { get; private set; }
    public ulong? AdvertisedSizeOverride { get; set; }

    public void Seed(string name, byte[] bytes) => _objects[name] = bytes;

    public override async Task<StorageObject> UploadObjectAsync(string bucket, string objectName,
        string contentType, Stream source, UploadObjectOptions? options = null,
        CancellationToken cancellationToken = default,
        IProgress<Google.Apis.Upload.IUploadProgress>? progress = null)
    {
        LastObjectName = objectName;
        if (options?.IfGenerationMatch == 0 && _objects.ContainsKey(objectName))
            throw ApiError(System.Net.HttpStatusCode.PreconditionFailed);
        await using var content = new MemoryStream();
        await source.CopyToAsync(content, cancellationToken);
        _objects[objectName] = content.ToArray();
        return Metadata(objectName, _objects[objectName]);
    }

    public override Task<StorageObject> GetObjectAsync(string bucket, string objectName,
        GetObjectOptions? options = null, CancellationToken cancellationToken = default)
    {
        if (!_objects.TryGetValue(objectName, out var bytes))
            throw ApiError(System.Net.HttpStatusCode.NotFound);
        return Task.FromResult(Metadata(objectName, bytes));
    }

    public override async Task<StorageObject> DownloadObjectAsync(string bucket, string objectName,
        Stream destination, DownloadObjectOptions? options = null,
        CancellationToken cancellationToken = default,
        IProgress<Google.Apis.Download.IDownloadProgress>? progress = null)
    {
        DownloadCalled = true;
        LastDownloadWasGenerationPinned = options?.Generation == CurrentGeneration;
        if (!LastDownloadWasGenerationPinned)
            throw new InvalidOperationException("Expected the metadata generation to be pinned.");
        var bytes = _objects[objectName];
        await destination.WriteAsync(bytes, cancellationToken);
        return Metadata(objectName, bytes);
    }

    private StorageObject Metadata(string name, byte[] bytes) => new()
    {
        Name = name,
        Size = AdvertisedSizeOverride ?? (ulong)bytes.Length,
        Generation = CurrentGeneration
    };

    private static GoogleApiException ApiError(System.Net.HttpStatusCode status) =>
        new("AURA.Tests", "Simulated object API response.") { HttpStatusCode = status };
}
