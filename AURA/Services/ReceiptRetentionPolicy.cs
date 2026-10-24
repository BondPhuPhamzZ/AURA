using AURA.Models;

namespace AURA.Services;

public static class ReceiptRetentionPolicy
{
    public const int RetentionDays = 7;

    private static readonly HashSet<string> FinalManagerDecisionStatuses =
    [
        "MANUAL_REVIEW_ACCEPTED",
        "RETURNED_FOR_MORE_EVIDENCE",
        "APPROVED_POLICY_EXCEPTION",
        "REJECTED_POLICY_EXCEPTION",
        "FORWARDED_TO_AUTHORITY",
        "RETURNED_BY_MANAGER",
        "RETURNED_FOR_RETRY"
    ];

    public static DateTime? ForAutomatedDecision(ReimbursementRequest request)
    {
        if (request.ProcessingState != ProcessingStates.Completed ||
            request.Status != "AUTO_APPROVE" || request.ProcessingCompletedAt is null)
            return null;

        return AsUtc(request.ProcessingCompletedAt.Value).AddDays(RetentionDays);
    }

    public static DateTime? ForManagerDecision(ReimbursementRequest request)
    {
        if (request.ManagerAnswer is null || request.ManagerDecisionAt is null ||
            !FinalManagerDecisionStatuses.Contains(request.Status))
            return null;

        return AsUtc(request.ManagerDecisionAt.Value).AddDays(RetentionDays);
    }

    public static bool IsFinalized(ReimbursementRequest request) =>
        ForAutomatedDecision(request) is not null || ForManagerDecision(request) is not null;

    public static bool IsExpired(DateTime? expiresAtUtc, DateTime utcNow) =>
        expiresAtUtc is not null && AsUtc(expiresAtUtc.Value) <= AsUtc(utcNow);

    private static DateTime AsUtc(DateTime value) => value.Kind switch
    {
        DateTimeKind.Utc => value,
        DateTimeKind.Local => value.ToUniversalTime(),
        _ => DateTime.SpecifyKind(value, DateTimeKind.Utc)
    };
}
