using AURA.Models;
using AURA.Services;
using Xunit;

namespace AURA.Tests;

public sealed class ReceiptProcessingRetryPolicyTests
{
    [Theory]
    [InlineData(1, true)]
    [InlineData(2, true)]
    [InlineData(3, false)]
    public void Rate_limit_requeues_only_before_attempt_limit(int attempt, bool shouldRetry)
    {
        var request = new ReimbursementRequest
        {
            ProcessingAttemptCount = attempt,
            ProcessingState = ProcessingStates.Processing,
            Status = ProcessingStates.Processing,
            AiReasoning = "stale",
            ManagerQuestion = "stale"
        };
        var failure = new VisionExtractionException("AI_RATE_LIMIT", "Temporary limit");

        var retried = ReceiptProcessingRetryPolicy.TryRescheduleRateLimit(request, failure, 3);

        Assert.Equal(shouldRetry, retried);
        if (shouldRetry)
        {
            Assert.Equal(ProcessingStates.Pending, request.ProcessingState);
            Assert.Equal(ProcessingStates.Pending, request.Status);
            Assert.Null(request.AiReasoning);
            Assert.Null(request.ManagerQuestion);
            Assert.Equal("AI_RATE_LIMIT", request.ProviderErrorCode);
        }
        else
        {
            Assert.Equal(ProcessingStates.Processing, request.ProcessingState);
        }
    }

    [Fact]
    public void Schema_failure_is_never_scheduled_as_rate_limit_retry()
    {
        var request = new ReimbursementRequest
        {
            ProcessingAttemptCount = 1,
            ProcessingState = ProcessingStates.Processing
        };

        Assert.False(ReceiptProcessingRetryPolicy.TryRescheduleRateLimit(request,
            new VisionExtractionException("AI_SCHEMA_MISMATCH", "Invalid JSON"), 3));
        Assert.Equal(ProcessingStates.Processing, request.ProcessingState);
    }
}
