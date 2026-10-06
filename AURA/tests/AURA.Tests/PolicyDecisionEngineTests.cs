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
    public void Paper_receipt_can_use_a_dedicated_receipt_number()
    {
        var facts = ValidFacts();
        facts.DocumentType = "RESTAURANT_BILL";
        facts.TaxId = null;
        facts.InvoiceNumber = null;
        facts.ReceiptNumber = "RC-221196";

        Assert.Equal("AUTO_APPROVE", Decide(facts).Status);
    }

    [Fact]
    public void Paper_receipt_can_use_a_per_purchase_transaction_reference()
    {
        var facts = ValidFacts();
        facts.DocumentType = "RETAIL_RECEIPT";
        facts.TaxId = null;
        facts.InvoiceNumber = null;
        facts.TransactionReference = "CHECK-221196";

        Assert.Equal("AUTO_APPROVE", Decide(facts).Status);
    }

    [Fact]
    public void Shop_and_terminal_ids_do_not_replace_a_paper_transaction_reference()
    {
        var facts = ValidFacts();
        facts.DocumentType = "RETAIL_RECEIPT";
        facts.TaxId = null;
        facts.InvoiceNumber = null;
        facts.MerchantId = "SHOP-30496";
        facts.TerminalId = "POS01";

        var result = Decide(facts);

        Assert.Equal("ESCALATE_FACT", result.Status);
        Assert.Contains("mã giao dịch riêng", result.Reason, StringComparison.OrdinalIgnoreCase);
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
    [InlineData("Bia lon")]
    [InlineData("Thuốc lá")]
    [InlineData("Karaoke client event")]
    [InlineData("Personal item")]
    [InlineData("Kẹp tóc Basic Marble Pattern")]
    [InlineData("Khăn Ướt Chăm Sóc Da Fressi Care Face")]
    [InlineData("Vé xem phim")]
    [InlineData("Ve xem phim")]
    [InlineData("Vé xem phim IMAX")]
    [InlineData("Rạp chiếu phim - suất tối")]
    [InlineData("Movie ticket")]
    [InlineData("Cinema ticket")]
    public void Prohibited_item_is_policy_escalation(string item)
    {
        var facts = ValidFacts();
        facts.LineItems = [new ReceiptLineItem { Description = item, Amount = 150_000 }];

        Assert.Equal("ESCALATE_POLICY", Decide(facts).Status);
    }

    [Theory]
    [InlineData("Bìa hồ sơ")]
    [InlineData("Bìa còng lưu tài liệu")]
    [InlineData("Bia hồ sơ")]
    [InlineData("Bia ho so")]
    [InlineData("Bia còng lưu tài liệu")]
    public void Stationery_bia_even_when_ocr_loses_diacritic_is_not_alcohol(string item)
    {
        var facts = ValidFacts();
        facts.LineItems = [new ReceiptLineItem { Description = item, Amount = 150_000 }];

        Assert.Equal("AUTO_APPROVE", Decide(facts).Status);
    }

    [Fact]
    public void Short_alphabetic_receipt_fragment_cannot_auto_approve()
    {
        var facts = ValidFacts();
        facts.InvoiceNumber = null;
        facts.ReceiptNumber = "RCF";

        var result = Decide(facts);

        Assert.Equal("ESCALATE_FACT", result.Status);
        Assert.Contains("thiếu số hóa đơn", result.Reason, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void Printed_seconds_are_losslessly_normalized_before_policy()
    {
        var facts = ValidFacts();
        facts.DocumentType = "RETAIL_RECEIPT";
        facts.TaxId = null;
        facts.InvoiceNumber = null;
        facts.ReceiptNumber = "20261006.2.52594";
        facts.InvoiceDate = "2026-10-06";
        facts.InvoiceDateEvidence = "06/10/2026";
        facts.InvoiceTime = "09:46:21";
        facts.TotalAmount = 16_000m;
        facts.TotalAmountEvidence = "TỔNG CỘNG 16.000";
        facts.LineItems = [new ReceiptLineItem { Description = "Bia Larue Special", Amount = 16_000m }];

        ReceiptSemanticValidator.NormalizeCanonicalFields(facts);
        var result = PolicyDecisionEngine.Evaluate(facts, 16_000m,
            utcNow: new DateTime(2026, 10, 6, 0, 0, 0, DateTimeKind.Utc));

        Assert.Equal("09:46", facts.InvoiceTime);
        Assert.Equal("ESCALATE_POLICY", result.Status);
        Assert.DoesNotContain("giờ", result.Reason, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void Version_two_facts_without_printed_final_total_provenance_fail_safe()
    {
        var facts = ValidFacts();
        facts.TotalAmountSource = "INFERRED";
        facts.TotalAmountEvidence = null;

        var result = Decide(facts);

        Assert.Equal("ESCALATE_FACT", result.Status);
        Assert.Contains("dòng tổng thanh toán cuối", result.Reason, StringComparison.OrdinalIgnoreCase);
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

        var result = Decide(facts, 2_000_000);

        Assert.Equal("ESCALATE_FACT", result.Status);
        Assert.Contains("hạng mục cần kiểm tra policy", result.Reason, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("Tiger Beer", result.Reason, StringComparison.OrdinalIgnoreCase);
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
        EvidenceContractVersion = ReceiptExtractionDto.CurrentEvidenceContractVersion,
        DocumentType = "VAT_INVOICE",
        MerchantName = "AURA Taxi",
        TaxId = "0312345678",
        InvoiceNumber = "AA/26E-000001",
        InvoiceDate = "2026-09-18",
        InvoiceDateEvidence = "18/09/2026",
        InvoiceTime = "09:00",
        Currency = "VND",
        TotalAmount = 150_000,
        TotalAmountSource = "PRINTED_FINAL_TOTAL",
        TotalAmountEvidence = "TỔNG THANH TOÁN 150.000 VND",
        Confidence = 0.98,
        LineItems = [new ReceiptLineItem { Description = "Business taxi trip", Amount = 150_000 }]
    };

    private static ReceiptExtractionDto ValidEcommerceFacts() => new()
    {
        EvidenceContractVersion = ReceiptExtractionDto.CurrentEvidenceContractVersion,
        DocumentType = "ECOMMERCE",
        DocumentStatus = "COMPLETED",
        PlatformName = "Shopee",
        MerchantName = "Double Fish Việt Nam",
        OrderStatus = "Đơn hàng đã hoàn thành",
        TransactionDate = "2026-09-18",
        TransactionDateEvidence = "18/09/2026",
        CompletionDate = "2026-09-18",
        Currency = "VND",
        TotalAmount = 295_199,
        TotalAmountSource = "PRINTED_FINAL_TOTAL",
        TotalAmountEvidence = "Thành tiền 295.199 đ",
        Confidence = 0.98,
        LineItems = [new ReceiptLineItem { Description = "Vợt bóng bàn", Quantity = 1, Amount = 295_199 }]
    };
}
