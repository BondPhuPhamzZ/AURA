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
        public const int CurrentEvidenceContractVersion = 2;

        // Versioned evidence contract. Version 2 adds explicit provenance for the
        // policy-critical date and final payable amount. Legacy persisted facts have
        // the default value 0 and remain readable, while every new model response must
        // explicitly return the current version.
        public int EvidenceContractVersion { get; set; }
        // Backend provenance: only set when every other required field was present
        // and the provider omitted the schema version envelope.
        public bool EvidenceContractVersionInferred { get; set; }
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
        public string? ReceiptNumber { get; set; }
        public string? TransactionReference { get; set; }
        public string? TaxAuthorityCode { get; set; }
        public string? InvoiceSerial { get; set; }
        public string? DocumentNumber { get; set; }
        public string? PosNumber { get; set; }
        public string? InvoiceDate { get; set; }
        public string? InvoiceDateEvidence { get; set; }
        public string? TransactionDate { get; set; }
        public string? TransactionDateEvidence { get; set; }
        public string? CompletionDate { get; set; }
        public string? InvoiceTime { get; set; }
        public string? Currency { get; set; }
        public decimal? Subtotal { get; set; }
        public decimal? DiscountAmount { get; set; }
        public decimal? Tax { get; set; }
        public decimal? TotalAmount { get; set; }
        // PRINTED_FINAL_TOTAL is the only source eligible for automated approval.
        // NOT_VISIBLE, AMBIGUOUS and INFERRED keep the reason explicit and fail safe.
        public string? TotalAmountSource { get; set; }
        // Exact visible final-payable row, including its printed label and amount.
        // The semantic validator independently verifies that it contains TotalAmount.
        public string? TotalAmountEvidence { get; set; }
        public List<ReceiptLineItem> LineItems { get; set; } = new List<ReceiptLineItem>();
        public List<string> MissingFields { get; set; } = new List<string>();
        public List<string> Warnings { get; set; } = new List<string>();
        public List<string> SuspiciousSignals { get; set; } = new List<string>();
        // Backend privacy triage. The model reports only normalized markers such as
        // `personal_data:PHONE`; it must never echo the raw personal value here.
        public List<string> PersonalDataCategories { get; set; } = new List<string>();
        public string PrivacyRisk { get; set; } = PrivacyRiskLevels.None;
        public bool RequiresPrivacyReview { get; set; }
        // Backend-only provenance for the amount used by PolicyDecisionEngine.
        // This field is persisted in ExtractedFactsJson but is not model-generated.
        public string AmountSource { get; set; } = AmountSources.Unknown;
        // Added by the backend after structured output parsing. The model never supplies
        // this field; it records unresolved cross-field contradictions for the policy engine.
        public List<string> ValidationIssues { get; set; } = new List<string>();
        // Backend-only observability. These fields are not part of the model response schema;
        // they make a successful or failed one-shot semantic repair visible in status JSON,
        // persisted facts and audit evidence.
        public bool SemanticRepairApplied { get; set; }
        public List<string> SemanticRepairIssues { get; set; } = new List<string>();
        public double Confidence { get; set; }
    }

    public static class PrivacyRiskLevels
    {
        public const string None = "NONE";
        public const string Review = "REVIEW";
        public const string High = "HIGH";
    }

    public static class AmountSources
    {
        public const string Unknown = "UNKNOWN";
        public const string UserProvided = "USER_PROVIDED";
        public const string AiExtracted = "AI_EXTRACTED";
        public const string Unavailable = "UNAVAILABLE";
    }
}
