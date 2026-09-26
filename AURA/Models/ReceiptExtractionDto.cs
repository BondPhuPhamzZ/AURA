using System;
using System.Collections.Generic;

namespace AURA.Models
{
    public class ReceiptLineItem
    {
        public string Description { get; set; } = string.Empty;
        public decimal? Quantity { get; set; }
        public decimal? UnitPrice { get; set; }
        public decimal? Amount { get; set; }
    }

    public class ReceiptExtractionDto
    {
        public string? DocumentType { get; set; }
        public string? DocumentStatus { get; set; }
        public string? MerchantName { get; set; }
        public string? TaxId { get; set; }
        public string? MerchantId { get; set; }
        public string? TerminalId { get; set; }
        public string? PlatformName { get; set; }
        public string? OrderId { get; set; }
        public string? BookingId { get; set; }
        public string? ShippingTrackingCode { get; set; }
        public string? ShippingProvider { get; set; }
        public string? OrderStatus { get; set; }
        public string? InvoiceNumber { get; set; }
        public string? InvoiceDate { get; set; }
        public string? TransactionDate { get; set; }
        public string? CompletionDate { get; set; }
        public string? InvoiceTime { get; set; }
        public string? Currency { get; set; }
        public decimal? Subtotal { get; set; }
        public decimal? Tax { get; set; }
        public decimal? TotalAmount { get; set; }
        public List<ReceiptLineItem> LineItems { get; set; } = new List<ReceiptLineItem>();
        public List<string> MissingFields { get; set; } = new List<string>();
        public List<string> Warnings { get; set; } = new List<string>();
        public List<string> SuspiciousSignals { get; set; } = new List<string>();
        // Added by the backend after structured output parsing. The model never supplies
        // this field; it records unresolved cross-field contradictions for the policy engine.
        public List<string> ValidationIssues { get; set; } = new List<string>();
        public double Confidence { get; set; }
    }
}
