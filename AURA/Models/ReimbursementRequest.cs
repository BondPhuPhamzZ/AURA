using System;
using System.ComponentModel.DataAnnotations;

namespace AURA.Models
{
    public class ReimbursementRequest
    {
        [Key]
        public string Id { get; set; } = Guid.NewGuid().ToString();
        
        public decimal ClaimedAmount { get; set; }
        
        [Required, StringLength(500)]
        public string ImageUrl { get; set; } = string.Empty;

        [Required, StringLength(255)]
        public string OriginalFileName { get; set; } = string.Empty;

        [Required, StringLength(255)]
        public string StoredFileName { get; set; } = string.Empty;

        [Required, StringLength(100)]
        public string ContentType { get; set; } = string.Empty;

        public long FileSizeBytes { get; set; }

        [Required, StringLength(64)]
        public string FileSha256 { get; set; } = string.Empty;

        public string? ExtractedFactsJson { get; set; }

        [Required, StringLength(32)]
        public string ProcessingState { get; set; } = ProcessingStates.Pending;

        public DateTime QueuedAt { get; set; } = DateTime.UtcNow;

        public DateTime? ProcessingStartedAt { get; set; }

        public DateTime? ProcessingCompletedAt { get; set; }

        public DateTime? ProcessingLeaseUntil { get; set; }

        public int ProcessingAttemptCount { get; set; }

        [StringLength(50)]
        public string? PrimaryProvider { get; set; }

        [StringLength(50)]
        public string? ServedProvider { get; set; }

        [StringLength(100)]
        public string? ProviderErrorCode { get; set; }

        public bool FallbackUsed { get; set; }

        public bool DuplicateDetected { get; set; }

        public bool DuplicatePolicyEnabled { get; set; }

        [Required, StringLength(100)]
        public string SubmitterCode { get; set; } = "DEMO-EMPLOYEE";

        [Required, StringLength(200)]
        public string SubmitterDisplayName { get; set; } = "Nhân viên demo";

        [Required, StringLength(200)]
        public string SubmitterDepartment { get; set; } = "Demo";
        
        public string Status { get; set; } = "PENDING"; // PENDING, ESCALATE, AUTO_APPROVE, REJECTED
        
        public string? AiReasoning { get; set; }
        
        public string? ManagerQuestion { get; set; }

        public bool IsForwardedToManager { get; set; }

        public DateTime? ForwardedAt { get; set; }

        public bool? ManagerAnswer { get; set; }

        public DateTime? ManagerDecisionAt { get; set; }
        
        public long ProcessingLatencyMs { get; set; }

        [Timestamp]
        public byte[] RowVersion { get; set; } = [];
        
        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    }

    public static class ProcessingStates
    {
        public const string Pending = "PENDING";
        public const string Processing = "PROCESSING";
        public const string Completed = "COMPLETED";
        public const string Failed = "FAILED";
    }
}
