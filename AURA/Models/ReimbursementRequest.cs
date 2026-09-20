using System;
using System.ComponentModel.DataAnnotations;

namespace AURA.Models
{
    public class ReimbursementRequest
    {
        [Key]
        public string Id { get; set; } = Guid.NewGuid().ToString();
        
        public decimal ClaimedAmount { get; set; }
        
        public string ImageUrl { get; set; }
        
        public string Status { get; set; } = "PENDING"; // PENDING, ESCALATE, AUTO_APPROVE, REJECTED
        
        public string? AiReasoning { get; set; }
        
        public string? ManagerQuestion { get; set; }
        
        public long ProcessingLatencyMs { get; set; }
        
        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    }
}
