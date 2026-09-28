using System.Threading.Channels;

namespace AURA.Services;

public sealed class ReceiptProcessingQueue
{
    private readonly Channel<string> _signals = Channel.CreateBounded<string>(new BoundedChannelOptions(256)
    {
        FullMode = BoundedChannelFullMode.DropOldest,
        SingleReader = true,
        SingleWriter = false
    });

    public void Signal(string requestId) => _signals.Writer.TryWrite(requestId);

    public async Task WaitForSignalOrTimeoutAsync(TimeSpan timeout, CancellationToken cancellationToken)
    {
        using var timeoutSource = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        timeoutSource.CancelAfter(timeout);
        try
        {
            await _signals.Reader.WaitToReadAsync(timeoutSource.Token);
            while (_signals.Reader.TryRead(out _)) { }
        }
        catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
        {
            // A timeout is intentional: the database is polled so pending jobs survive restarts.
        }
    }
}
