using System;
using System.ComponentModel.DataAnnotations;

namespace AURA.ViewModels
{
    public class ReimbursementViewModel
    {
        public string EmployeeName { get; set; } = string.Empty;
        public decimal ClaimedAmount { get; set; }
        public string ImageUrl { get; set; } = string.Empty;
        public string Status { get; set; } = string.Empty;
        public string AiReasoning { get; set; } = string.Empty;
        public string ManagerQuestion { get; set; } = string.Empty;
        public long ProcessingLatencyMs { get; set; }
    }
}
