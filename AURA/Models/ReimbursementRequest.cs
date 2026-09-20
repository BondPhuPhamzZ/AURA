using System;
using System.ComponentModel.DataAnnotations;

namespace AURA.Models
{
    public class ReimbursementRequest
    {
        [Key]
        public string Id { get; set; } = Guid.NewGuid().ToString();
        [Required]
        public string EmployeeId { get; set; } = string.Empty;
        [Required]
        public string EmployeeName { get; set; } = string.Empty;
        public decimal ClaimedAmount { get; set; }
        public string ImageUrl { get; set; } = string.Empty;
        public string Status { get; set; } = "PENDING";
        public string? AiReasoning { get; set; }
        public string? ManagerQuestion { get; set; }
        public long ProcessingLatencyMs { get; set; }
        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    }
}
