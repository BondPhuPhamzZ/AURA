using System.Security.Cryptography;
using AURA.Interfaces;
using AURA.Options;
using Microsoft.Extensions.Options;

namespace AURA.Services;

public sealed class LocalReceiptFileStore : IReceiptFileStore
{
    private readonly string _root;

    public LocalReceiptFileStore(IWebHostEnvironment environment, IOptions<ReceiptStorageOptions> options)
    {
        _root = ReceiptStoragePath.ResolveRoot(environment, options.Value.Directory);
        Directory.CreateDirectory(_root);
    }

    public async Task<bool> PutIfAbsentAsync(string storedFileName, ReadOnlyMemory<byte> bytes,
        string contentType, string sha256, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        ReceiptStoredFileIntegrity.ValidateContentHash(bytes.Span, sha256);
        var destination = Resolve(storedFileName);
        var staged = Resolve($".{storedFileName}.{Guid.NewGuid():N}.tmp");
        try
        {
            await using (var output = new FileStream(staged, FileMode.CreateNew, FileAccess.Write,
                FileShare.None, 64 * 1024, FileOptions.Asynchronous | FileOptions.SequentialScan))
            {
                await output.WriteAsync(bytes, cancellationToken);
                await output.FlushAsync(cancellationToken);
            }

            try
            {
                File.Move(staged, destination);
                return true;
            }
            catch (IOException) when (File.Exists(destination))
            {
                await VerifyExistingAsync(destination, bytes.Length, sha256, cancellationToken);
                return false;
            }
        }
        finally
        {
            if (File.Exists(staged)) File.Delete(staged);
        }
    }

    public Task<Stream?> OpenReadAsync(string storedFileName, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        var path = Resolve(storedFileName);
        if (!File.Exists(path)) return Task.FromResult<Stream?>(null);
        Stream stream = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.Read,
            64 * 1024, FileOptions.Asynchronous | FileOptions.SequentialScan);
        return Task.FromResult<Stream?>(stream);
    }

    public Task DeleteAsync(string storedFileName, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        var path = Resolve(storedFileName);
        if (File.Exists(path)) File.Delete(path);
        return Task.CompletedTask;
    }

    public Task<bool> IsAvailableAsync(CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        return Task.FromResult(Directory.Exists(_root));
    }

    private string Resolve(string storedFileName) =>
        ReceiptStoragePath.ResolveFile(_root, storedFileName);

    private static async Task VerifyExistingAsync(string path, int size, string sha256,
        CancellationToken cancellationToken)
    {
        await using var file = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.Read,
            64 * 1024, FileOptions.Asynchronous | FileOptions.SequentialScan);
        if (file.Length != size || !string.Equals(
                Convert.ToHexString(await SHA256.HashDataAsync(file, cancellationToken)), sha256,
                StringComparison.OrdinalIgnoreCase))
            throw new InvalidOperationException("Existing receipt object has conflicting content.");
    }
}
