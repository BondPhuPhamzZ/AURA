using AURA.Services;
using Xunit;

namespace AURA.Tests;

public sealed class WorkflowOperationGateTests
{
    [Fact]
    public void Rejects_overlapping_operation_and_allows_next_after_release()
    {
        var gate = new WorkflowOperationGate();

        Assert.True(gate.TryEnter("first", out var firstLease));
        Assert.Equal("first", gate.CurrentOperation);
        Assert.False(gate.TryEnter("overlap", out var rejectedLease));
        Assert.Null(rejectedLease);

        firstLease!.Dispose();

        Assert.True(gate.TryEnter("next", out var nextLease));
        nextLease!.Dispose();
        Assert.Null(gate.CurrentOperation);
    }
}
