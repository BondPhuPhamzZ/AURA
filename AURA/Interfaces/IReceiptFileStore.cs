namespace AURA.Interfaces;

public interface IReceiptFileStore
{
    /// <summary>Atomically stores content if the key is absent; existing content must match SHA-256.</summary>
    Task<bool> PutIfAbsentAsync(string storedFileName, ReadOnlyMemory<byte> bytes,
        string contentType, string sha256, CancellationToken cancellationToken);

    Task<Stream?> OpenReadAsync(string storedFileName, CancellationToken cancellationToken);
    Task DeleteAsync(string storedFileName, CancellationToken cancellationToken);
    Task<bool> IsAvailableAsync(CancellationToken cancellationToken);
}
