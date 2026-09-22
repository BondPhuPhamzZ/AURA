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
}
