using System;
using System.Collections.Generic;

namespace AURA.Models
{
    public class ReceiptLineItem
    {
        public string Description { get; set; }
        public decimal? Quantity { get; set; }
        public decimal? UnitPrice { get; set; }
        public decimal? Amount { get; set; }
    }

    public class ReceiptExtractionDto
    {
        public string MerchantName { get; set; }
        public string InvoiceNumber { get; set; }
        public string InvoiceDate { get; set; }
        public string Currency { get; set; }
        public decimal? Subtotal { get; set; }
        public decimal? Tax { get; set; }
        public decimal? TotalAmount { get; set; }
        public List<ReceiptLineItem> LineItems { get; set; } = new List<ReceiptLineItem>();
        public List<string> MissingFields { get; set; } = new List<string>();
        public List<string> Warnings { get; set; } = new List<string>();
        public double Confidence { get; set; }
    }
}
