using System;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace AURA.Models
{
    public class ReimbursementRequest
    {
        [Key]
        [DatabaseGenerated(DatabaseGeneratedOption.Identity)]
        public string Id { get; set; } = Guid.NewGuid().ToString();

        [Required(ErrorMessage = "Employee ID is required")]
        [StringLength(50)]
        public string EmployeeId { get; set; } = string.Empty;

        [Required(ErrorMessage = "Tên nhân viên là bắt buộc")]
        [StringLength(100)]
        public string EmployeeName { get; set; } = string.Empty;

        [Required]
        [Range(1, 1000000000, ErrorMessage = "Số tiền phải lớn hơn 0")]
        public decimal ClaimedAmount { get; set; }
        
        [Required]
        [MaxLength(2048)]
        public string ImageUrl { get; set; } = string.Empty;

        [StringLength(50)]
        public string Status { get; set; } = "PENDING"; // PENDING, AUTO_APPROVE, ESCALATE
        
        public string? AiReasoning { get; set; }
        
        public string? ManagerQuestion { get; set; }
        
        public long ProcessingLatencyMs { get; set; }
        
        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    }
}
