using System.Text.Json;
using AURA.Services;

namespace AURA.Models;

public sealed class ReimbursementResultDto
{
    public required string CaseId { get; init; }
    public required string Image { get; init; }
    public string? Expected { get; init; }
    public string? Actual { get; init; }
    public bool? Pass { get; init; }
    public required string ProcessingState { get; init; }
    public string? Reason { get; init; }
    public string? Question { get; init; }
    public long? LatencyMs { get; init; }
    public DateTime Timestamp { get; init; }
    public required string ReceiptUrl { get; init; }
    public bool CanForward { get; init; }
    public string? HandoffPrompt { get; init; }
    public bool DuplicateDetected { get; init; }
    public bool DuplicatePolicyEnabled { get; init; }
    public string? PrimaryProvider { get; init; }
    public string? ServedProvider { get; init; }
    public bool FallbackUsed { get; init; }
    public string? ProviderErrorCode { get; init; }
    public ReceiptExtractionDto? Facts { get; init; }

    public static ReimbursementResultDto FromRequest(ReimbursementRequest request)
    {
        ReceiptExtractionDto? facts = null;
        if (!string.IsNullOrWhiteSpace(request.ExtractedFactsJson))
        {
            try
            {
                facts = JsonSerializer.Deserialize<ReceiptExtractionDto>(request.ExtractedFactsJson);
            }
            catch (JsonException)
            {
                // The audit trail remains available even if a legacy payload cannot be deserialized.
            }
        }

        var completed = request.ProcessingState is ProcessingStates.Completed or ProcessingStates.Failed;
        return new ReimbursementResultDto
        {
            CaseId = request.Id,
            Image = request.OriginalFileName,
            Actual = completed ? request.Status : null,
            Pass = null,
            ProcessingState = request.ProcessingState,
            Reason = completed ? request.AiReasoning : "Hồ sơ đã được ghi nhận và đang chờ AI xử lý.",
            Question = completed ? request.ManagerQuestion : null,
            LatencyMs = completed ? request.ProcessingLatencyMs : null,
            Timestamp = request.CreatedAt,
            ReceiptUrl = request.ImageUrl,
            CanForward = completed && PolicyDecisionEngine.IsEscalation(request.Status),
            HandoffPrompt = completed && PolicyDecisionEngine.IsEscalation(request.Status)
                ? EscalationWorkflow.EmployeeHandoffPrompt
                : null,
            DuplicateDetected = request.DuplicateDetected,
            DuplicatePolicyEnabled = request.DuplicatePolicyEnabled,
            PrimaryProvider = request.PrimaryProvider,
            ServedProvider = request.ServedProvider,
            FallbackUsed = request.FallbackUsed,
            ProviderErrorCode = request.ProviderErrorCode,
            Facts = facts
        };
    }
}
