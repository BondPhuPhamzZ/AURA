using AURA.Models;

namespace AURA.Services;

public static class ReceiptProcessingRetryPolicy
{
    public static bool TryRescheduleRateLimit(ReimbursementRequest request,
        VisionExtractionException failure, int maxAttempts)
    {
        if (failure.Code != "AI_RATE_LIMIT" ||
            request.ProcessingAttemptCount >= maxAttempts)
            return false;

        request.Status = ProcessingStates.Pending;
        request.ProcessingState = ProcessingStates.Pending;
        request.AiReasoning = null;
        request.ManagerQuestion = null;
        request.ProviderErrorCode = failure.Code;
        return true;
    }
}
