namespace AURA.Services;

/// <summary>
/// Prevents overlapping workflow mutations inside one application instance.
/// Optimistic database concurrency remains the final guard when multiple
/// application processes use the same database.
/// </summary>
public sealed class WorkflowOperationGate
{
    private readonly SemaphoreSlim semaphore = new(1, 1);
    private string? currentOperation;

    public string? CurrentOperation => Volatile.Read(ref currentOperation);

    public bool TryEnter(string operation, out IDisposable? lease)
    {
        if (!semaphore.Wait(0))
        {
            lease = null;
            return false;
        }

        Volatile.Write(ref currentOperation, operation);
        lease = new GateLease(this);
        return true;
    }

    private void Exit()
    {
        Volatile.Write(ref currentOperation, null);
        semaphore.Release();
    }

    private sealed class GateLease(WorkflowOperationGate owner) : IDisposable
    {
        private WorkflowOperationGate? owner = owner;

        public void Dispose()
        {
            Interlocked.Exchange(ref owner, null)?.Exit();
        }
    }
}
