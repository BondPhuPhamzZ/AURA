using AURA.Models;
using AURA.Services;
using Xunit;

namespace AURA.Tests;

public sealed class PolicyDecisionEngineTests
{
    private static readonly DateTime Now = new(2026, 9, 20, 0, 0, 0, DateTimeKind.Utc);

    [Fact]
    public void Ordinary_valid_receipt_is_auto_approved()
    {
        Assert.Equal("AUTO_APPROVE", Decide(ValidFacts()).Status);
    }

    [Fact]
    public void Null_extraction_is_fact_escalation()
    {
        Assert.Equal("ESCALATE_FACT", PolicyDecisionEngine.Evaluate(null, 150_000, utcNow: Now).Status);
    }

    [Theory]
    [InlineData("low-confidence")]
    [InlineData("missing-total")]
    [InlineData("amount-mismatch")]
    [InlineData("missing-merchant")]
    [InlineData("missing-invoice-number")]
    [InlineData("missing-tax-id")]
    [InlineData("unsupported-currency")]
    [InlineData("invalid-date")]
    [InlineData("future-date")]
    [InlineData("stale-date")]
    [InlineData("weekend")]
    [InlineData("late-night")]
    [InlineData("blurred")]
    public void Uncertain_or_unverifiable_evidence_is_fact_escalation(string scenario)
    {
        var facts = ValidFacts();
        var claimedAmount = 150_000m;
        switch (scenario)
        {
            case "low-confidence": facts.Confidence = 0.5; break;
            case "missing-total": facts.TotalAmount = null; break;
            case "amount-mismatch": claimedAmount = 151_000; break;
            case "missing-merchant": facts.MerchantName = null; break;
            case "missing-invoice-number": facts.InvoiceNumber = null; break;
            case "missing-tax-id": facts.TaxId = null; break;
            case "unsupported-currency": facts.Currency = "USD"; break;
            case "invalid-date": facts.InvoiceDate = "18/09/26"; break;
            case "future-date": facts.InvoiceDate = "2026-09-25"; break;
            case "stale-date": facts.InvoiceDate = "2026-01-02"; break;
            case "weekend": facts.InvoiceDate = "2026-09-19"; break;
            case "late-night": facts.InvoiceTime = "23:30"; break;
            case "blurred": facts.Warnings.Add("blurry total"); break;
        }

        Assert.Equal("ESCALATE_FACT", Decide(facts, claimedAmount).Status);
    }

    [Fact]
    public void Duplicate_image_is_fact_escalation()
    {
        Assert.Equal("ESCALATE_FACT", PolicyDecisionEngine.Evaluate(ValidFacts(), 150_000, true, Now).Status);
    }

    [Fact]
    public void Impossible_arithmetic_signal_is_fact_escalation()
    {
        var facts = ValidFacts();
        facts.SuspiciousSignals.Add("impossible arithmetic");

        var result = PolicyDecisionEngine.Evaluate(facts, 150_000m, utcNow: Now);

        Assert.Equal("ESCALATE_FACT", result.Status);
        Assert.Contains("impossible arithmetic", result.Reason, StringComparison.OrdinalIgnoreCase);
    }

    [Theory]
    [InlineData("DRAFT")]
    [InlineData("CANCELLED")]
    [InlineData("chưa phát hành")]
    public void Draft_or_cancelled_document_is_fact_escalation(string documentStatus)
    {
        var facts = ValidFacts();
        facts.DocumentStatus = documentStatus;

        Assert.Equal("ESCALATE_FACT", Decide(facts).Status);
    }

    [Fact]
    public void Completed_ecommerce_order_can_use_order_id_without_tax_or_invoice_number()
    {
        var facts = ValidEcommerceFacts();
        facts.OrderId = "SHOPEE-260918-001";

        Assert.Equal("AUTO_APPROVE", Decide(facts, 295_199).Status);
    }

    [Theory]
    [InlineData("RETAIL_RECEIPT")]
    [InlineData("RESTAURANT_BILL")]
    public void Numbered_non_vat_receipt_does_not_require_seller_tax_id(string documentType)
    {
        var facts = ValidFacts();
        facts.DocumentType = documentType;
        facts.TaxId = null;

        Assert.Equal("AUTO_APPROVE", Decide(facts).Status);
    }

    [Fact]
    public void Completed_ecommerce_order_can_use_shipping_tracking_code_as_traceable_identifier()
    {
        var facts = ValidEcommerceFacts();
        facts.ShippingTrackingCode = "VN2693231211394";
        facts.ShippingProvider = "SPX Instant";

        Assert.Equal("AUTO_APPROVE", Decide(facts, 295_199).Status);
    }

    [Fact]
    public void Ecommerce_order_without_any_traceable_identifier_is_fact_escalation()
    {
        Assert.Equal("ESCALATE_FACT", Decide(ValidEcommerceFacts(), 295_199).Status);
    }

    [Fact]
    public void Delivery_date_does_not_replace_missing_ecommerce_transaction_date()
    {
        var facts = ValidEcommerceFacts();
        facts.ShippingTrackingCode = "VN2693231211394";
        facts.TransactionDate = null;
        facts.CompletionDate = "2026-09-18";

        Assert.Equal("ESCALATE_FACT", Decide(facts, 295_199).Status);
    }

    [Fact]
    public void Refunded_ecommerce_order_is_fact_escalation_even_with_shipping_code()
    {
        var facts = ValidEcommerceFacts();
        facts.ShippingTrackingCode = "VN2693231211394";
        facts.OrderStatus = "COMPLETED - REFUNDED";

        Assert.Equal("ESCALATE_FACT", Decide(facts, 295_199).Status);
    }

    [Theory]
    [InlineData("Tiger Beer")]
    [InlineData("Thuốc lá")]
    [InlineData("Karaoke client event")]
    [InlineData("Personal item")]
    public void Prohibited_item_is_policy_escalation(string item)
    {
        var facts = ValidFacts();
        facts.LineItems = [new ReceiptLineItem { Description = item, Amount = 150_000 }];

        Assert.Equal("ESCALATE_POLICY", Decide(facts).Status);
    }

    [Fact]
    public void Reliable_receipt_over_limit_is_authority_escalation()
    {
        var facts = ValidFacts();
        facts.TotalAmount = 1_000_001;

        Assert.Equal("ESCALATE_AUTHORITY", Decide(facts, 1_000_001).Status);
    }

    [Fact]
    public void Fact_uncertainty_has_priority_over_policy_and_authority()
    {
        var facts = ValidFacts();
        facts.Confidence = 0.4;
        facts.TotalAmount = 2_000_000;
        facts.LineItems = [new ReceiptLineItem { Description = "Tiger Beer", Amount = 2_000_000 }];

        Assert.Equal("ESCALATE_FACT", Decide(facts, 2_000_000).Status);
    }

    [Theory]
    [InlineData("ESCALATE_FACT", true, "MANUAL_REVIEW_ACCEPTED")]
    [InlineData("ESCALATE_FACT", false, "RETURNED_FOR_MORE_EVIDENCE")]
    [InlineData("ESCALATE_POLICY", true, "APPROVED_POLICY_EXCEPTION")]
    [InlineData("ESCALATE_POLICY", false, "REJECTED_POLICY_EXCEPTION")]
    [InlineData("ESCALATE_AUTHORITY", true, "FORWARDED_TO_AUTHORITY")]
    [InlineData("ESCALATE_AUTHORITY", false, "RETURNED_BY_MANAGER")]
    [InlineData("ESCALATE_SYSTEM_ERROR", true, "MANUAL_REVIEW_ACCEPTED")]
    [InlineData("ESCALATE_SYSTEM_ERROR", false, "RETURNED_FOR_RETRY")]
    public void Manager_yes_no_has_explicit_outcome(string status, bool answer, string expected)
    {
        Assert.Equal(expected, EscalationWorkflow.Answer(status, answer).Status);
    }

    [Fact]
    public void Employee_handoff_only_confirms_transfer_not_the_manager_decision()
    {
        Assert.Contains("chuyển tiếp", EscalationWorkflow.EmployeeHandoffPrompt, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("[CÓ", EscalationWorkflow.EmployeeHandoffPrompt, StringComparison.OrdinalIgnoreCase);
    }

    [Theory]
    [InlineData("ESCALATE_FACT")]
    [InlineData("ESCALATE_POLICY")]
    [InlineData("ESCALATE_AUTHORITY")]
    public void Manager_choices_use_explicit_approve_and_reject_labels(string status)
    {
        var choices = EscalationWorkflow.ExplainChoices(status);

        Assert.StartsWith("Đồng ý", choices.Yes, StringComparison.Ordinal);
        Assert.StartsWith("Từ chối", choices.No, StringComparison.Ordinal);
    }

    [Fact]
    public void System_error_choices_do_not_claim_the_receipt_was_verified()
    {
        var choices = EscalationWorkflow.ExplainChoices("ESCALATE_SYSTEM_ERROR");

        Assert.Equal("Đủ tiêu chuẩn", choices.Yes);
        Assert.Equal("Không đủ tiêu chuẩn", choices.No);
    }

    [Fact]
    public void Escalation_question_is_addressed_to_manager()
    {
        var facts = ValidFacts();
        facts.Confidence = 0.4;
        var decision = Decide(facts);

        Assert.Contains("Quản lý", decision.ManagerQuestion, StringComparison.Ordinal);
    }

    private static (string Status, string Reason, string ManagerQuestion) Decide(
        ReceiptExtractionDto facts, decimal claimedAmount = 150_000) =>
        PolicyDecisionEngine.Evaluate(facts, claimedAmount, utcNow: Now);

    private static ReceiptExtractionDto ValidFacts() => new()
    {
        DocumentType = "VAT_INVOICE",
        MerchantName = "AURA Taxi",
        TaxId = "0312345678",
        InvoiceNumber = "AA/26E-000001",
        InvoiceDate = "2026-09-18",
        InvoiceTime = "09:00",
        Currency = "VND",
        TotalAmount = 150_000,
        Confidence = 0.98,
        LineItems = [new ReceiptLineItem { Description = "Business taxi trip", Amount = 150_000 }]
    };

    private static ReceiptExtractionDto ValidEcommerceFacts() => new()
    {
        DocumentType = "ECOMMERCE",
        DocumentStatus = "COMPLETED",
        PlatformName = "Shopee",
        MerchantName = "Double Fish Việt Nam",
        OrderStatus = "Đơn hàng đã hoàn thành",
        TransactionDate = "2026-09-18",
        CompletionDate = "2026-09-18",
        Currency = "VND",
        TotalAmount = 295_199,
        Confidence = 0.98,
        LineItems = [new ReceiptLineItem { Description = "Vợt bóng bàn", Quantity = 1, Amount = 295_199 }]
    };
}
