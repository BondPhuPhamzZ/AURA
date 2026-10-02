using AURA.Models;
using AURA.Services;
using Xunit;

namespace AURA.Tests;

public sealed class ReceiptSemanticValidatorTests
{
    [Fact]
    public void Detects_the_exact_ollama_tc01_semantic_failures()
    {
        var facts = FaultyOllamaTc01();

        var issues = ReceiptSemanticValidator.Validate(facts);

        Assert.Contains(issues, issue => issue.Contains("totalAmount=295.199", StringComparison.Ordinal));
        Assert.Contains(issues, issue => issue.Contains("RIDE_HAILING", StringComparison.Ordinal));
        Assert.Contains(issues, issue => issue.Contains("orderId", StringComparison.Ordinal));
    }

    [Fact]
    public void Accepts_correctly_normalized_ecommerce_facts()
    {
        var facts = CorrectedTc01();

        Assert.Empty(ReceiptSemanticValidator.Validate(facts));
        Assert.Equal("AUTO_APPROVE",
            PolicyDecisionEngine.Evaluate(facts, 295_199m,
                utcNow: new DateTime(2026, 9, 20, 0, 0, 0, DateTimeKind.Utc)).Status);
    }

    [Fact]
    public void Does_not_multiply_a_genuine_whole_vnd_amount()
    {
        var facts = CorrectedTc01();
        facts.Subtotal = 292m;
        facts.Tax = 3m;
        facts.TotalAmount = 295m;
        facts.LineItems =
        [
            new ReceiptLineItem { Description = "Mặt hàng A", Amount = 292m },
            new ReceiptLineItem { Description = "Phí", Amount = 3m }
        ];

        Assert.Empty(ReceiptSemanticValidator.Validate(facts));
        Assert.Equal(295m, facts.TotalAmount);
    }

    [Fact]
    public void Unresolved_semantic_issue_is_a_fact_escalation()
    {
        var facts = ReceiptSemanticValidator.MarkUnresolved(FaultyOllamaTc01(),
            ReceiptSemanticValidator.Validate(FaultyOllamaTc01()));

        var result = PolicyDecisionEngine.Evaluate(facts, 295_199m,
            utcNow: new DateTime(2026, 9, 20, 0, 0, 0, DateTimeKind.Utc));

        Assert.Equal("ESCALATE_FACT", result.Status);
        Assert.Contains("mâu thuẫn", result.Reason, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void Receipts_without_line_items_are_not_auto_approved()
    {
        var ecommerceFacts = CorrectedTc01();
        ecommerceFacts.LineItems = [];

        var ecommerceResult = PolicyDecisionEngine.Evaluate(ecommerceFacts, 295_199m,
            utcNow: new DateTime(2026, 9, 20, 0, 0, 0, DateTimeKind.Utc));

        Assert.Equal("ESCALATE_FACT", ecommerceResult.Status);
        Assert.Contains("danh sách hàng hóa", ecommerceResult.Reason, StringComparison.OrdinalIgnoreCase);

        var paperFacts = new ReceiptExtractionDto
        {
            DocumentType = "RESTAURANT_BILL",
            DocumentStatus = "COMPLETED",
            MerchantName = "Phê La",
            ReceiptNumber = "020056",
            InvoiceDate = "2026-10-02",
            Currency = "VND",
            TotalAmount = 69_000m,
            Confidence = 0.95,
            LineItems = []
        };
        var semanticIssues = ReceiptSemanticValidator.Validate(paperFacts);

        Assert.Contains(semanticIssues, issue => issue.Contains("lineItems", StringComparison.Ordinal));
        ReceiptSemanticValidator.MarkUnresolved(paperFacts, semanticIssues);
        Assert.Contains("lineItems", paperFacts.MissingFields);
        Assert.Contains(paperFacts.Warnings,
            warning => warning.Contains("người kiểm tra", StringComparison.OrdinalIgnoreCase));
        Assert.Equal(0.69, paperFacts.Confidence);
        var paperResult = PolicyDecisionEngine.Evaluate(paperFacts, 69_000m,
            utcNow: new DateTime(2026, 10, 2, 0, 0, 0, DateTimeKind.Utc));

        Assert.Equal("ESCALATE_FACT", paperResult.Status);
        Assert.Contains("policy", paperResult.Reason, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void Normalizes_a_paper_transaction_date_without_inventing_new_evidence()
    {
        var facts = new ReceiptExtractionDto
        {
            DocumentType = "RETAIL_RECEIPT",
            DocumentStatus = "ISSUED",
            MerchantName = "Phở Hai Thiền",
            OrderId = "HD-260921-002",
            InvoiceNumber = null,
            InvoiceDate = null,
            TransactionDate = "2026-09-18",
            Currency = "VND",
            TotalAmount = 88_000m,
            Confidence = 0.95,
            LineItems = [new ReceiptLineItem { Description = "Phở bò tái", Amount = 88_000m }]
        };

        ReceiptSemanticValidator.NormalizeCanonicalFields(facts);
        var issues = ReceiptSemanticValidator.Validate(facts);

        Assert.Contains(issues, issue => issue.Contains("invoiceNumber", StringComparison.Ordinal));
        Assert.Equal("2026-09-18", facts.InvoiceDate);
        Assert.Null(facts.TransactionDate);
        Assert.DoesNotContain(issues, issue => issue.Contains("Date", StringComparison.Ordinal));
    }

    [Fact]
    public void Removes_only_an_exact_duplicate_paper_identifier_after_repair()
    {
        var facts = new ReceiptExtractionDto
        {
            DocumentType = "RETAIL_RECEIPT",
            InvoiceNumber = "HD-260921-002",
            OrderId = "HD-260921-002",
            ShippingTrackingCode = "DIFFERENT-CODE",
            InvoiceDate = "2026-09-18",
            TransactionDate = "2026-09-18"
        };

        ReceiptSemanticValidator.NormalizeCanonicalFields(facts);

        Assert.Null(facts.OrderId);
        Assert.Null(facts.TransactionDate);
        Assert.Equal("DIFFERENT-CODE", facts.ShippingTrackingCode);
        Assert.Contains(ReceiptSemanticValidator.Validate(facts),
            issue => issue.Contains("mã thừa", StringComparison.Ordinal));
    }

    [Fact]
    public void Removes_a_misplaced_order_id_after_transaction_reference_repair()
    {
        var facts = new ReceiptExtractionDto
        {
            DocumentType = "RESTAURANT_BILL",
            TransactionReference = "221196",
            OrderId = "221196",
            LineItems = [new ReceiptLineItem { Description = "Đồ uống", Amount = 59_000m }]
        };

        ReceiptSemanticValidator.NormalizeCanonicalFields(facts);

        Assert.Null(facts.OrderId);
        Assert.Equal("221196", facts.TransactionReference);
        Assert.Empty(ReceiptSemanticValidator.Validate(facts));
    }

    [Fact]
    public void Duplicate_values_across_paper_identifier_fields_are_rejected()
    {
        var facts = new ReceiptExtractionDto
        {
            DocumentType = "RETAIL_RECEIPT",
            ReceiptNumber = "RC-001",
            TransactionReference = "RC-001",
            InvoiceDate = "2026-09-18",
            TransactionDate = "2026-09-19"
        };

        var issues = ReceiptSemanticValidator.Validate(facts);

        Assert.Contains(issues, issue => issue.Contains("receiptNumber", StringComparison.Ordinal) &&
            issue.Contains("transactionReference", StringComparison.Ordinal));
        Assert.DoesNotContain(issues, issue => issue.Contains("Date", StringComparison.Ordinal));
    }

    [Fact]
    public void Removes_a_nonimpacting_discount_when_three_independent_totals_already_match()
    {
        var facts = new ReceiptExtractionDto
        {
            DocumentType = "RETAIL_RECEIPT",
            DocumentStatus = "ISSUED",
            MerchantName = "Highlands Coffee",
            TransactionReference = "221196",
            TransactionDate = "2026-09-29",
            InvoiceTime = "13:52",
            Currency = "VND",
            Subtotal = 59_000m,
            DiscountAmount = 1_000m,
            Tax = 0m,
            TotalAmount = 59_000m,
            Confidence = 0.95,
            LineItems = [new ReceiptLineItem { Description = "Đồ uống", Amount = 59_000m }]
        };

        ReceiptSemanticValidator.NormalizeCanonicalFields(facts);

        Assert.Equal("2026-09-29", facts.InvoiceDate);
        Assert.Null(facts.TransactionDate);
        Assert.Null(facts.DiscountAmount);
        Assert.Contains(facts.Warnings,
            warning => warning.Contains("không có giá trước giảm", StringComparison.Ordinal));
        Assert.Empty(ReceiptSemanticValidator.Validate(facts));
        Assert.Equal("AUTO_APPROVE",
            PolicyDecisionEngine.Evaluate(facts, 59_000m,
                utcNow: new DateTime(2026, 10, 1, 0, 0, 0, DateTimeKind.Utc)).Status);
    }

    [Fact]
    public void Keeps_a_reconciled_receipt_discount_and_distinct_final_payment()
    {
        var facts = VinamilkDiscountedReceipt();

        ReceiptSemanticValidator.NormalizeCanonicalFields(facts);

        Assert.Equal(2_828m, facts.DiscountAmount);
        Assert.DoesNotContain(facts.Warnings,
            warning => warning.Contains("không có giá trước giảm", StringComparison.Ordinal));
        Assert.Empty(ReceiptSemanticValidator.Validate(facts));
    }

    [Fact]
    public void Accepts_printed_receipt_discount_reconciled_to_final_payment()
    {
        var facts = VinamilkDiscountedReceipt();

        Assert.Empty(ReceiptSemanticValidator.Validate(facts));
        Assert.Equal("AUTO_APPROVE",
            PolicyDecisionEngine.Evaluate(facts, 180_286m,
                utcNow: new DateTime(2026, 10, 1, 0, 0, 0, DateTimeKind.Utc)).Status);

        facts.Subtotal = 190_000m;
        Assert.Contains(ReceiptSemanticValidator.Validate(facts),
            issue => issue.Contains("subtotal", StringComparison.Ordinal));
    }

    [Fact]
    public void Final_payment_not_pre_discount_subtotal_is_compared_with_the_claim()
    {
        var result = PolicyDecisionEngine.Evaluate(VinamilkDiscountedReceipt(), 183_114m,
            utcNow: new DateTime(2026, 10, 1, 0, 0, 0, DateTimeKind.Utc));

        Assert.Equal("ESCALATE_FACT", result.Status);
        Assert.Contains("180,286", result.Reason, StringComparison.Ordinal);
    }

    [Fact]
    public void Discount_keyword_in_warning_does_not_bypass_structured_arithmetic()
    {
        var facts = VinamilkDiscountedReceipt();
        facts.DiscountAmount = null;
        facts.Warnings.Add("Hóa đơn có dòng giảm giá");

        var issues = ReceiptSemanticValidator.Validate(facts);

        Assert.Contains(issues, issue => issue.Contains("discountAmount", StringComparison.Ordinal));
    }

    [Theory]
    [InlineData(-2828, "không âm")]
    [InlineData(200000, "vượt quá")]
    [InlineData(2800, "không đối chiếu")]
    [InlineData(2828.5, "phần thập phân")]
    public void Rejects_invalid_or_unreconciled_vnd_discount(decimal discount, string expectedIssue)
    {
        var facts = VinamilkDiscountedReceipt();
        facts.DiscountAmount = discount;

        Assert.Contains(ReceiptSemanticValidator.Validate(facts),
            issue => issue.Contains(expectedIssue, StringComparison.OrdinalIgnoreCase));
    }

    private static ReceiptExtractionDto FaultyOllamaTc01() => new()
    {
        DocumentType = "RIDE_HAILING",
        DocumentStatus = "COMPLETED",
        MerchantName = "Double Fish Việt Nam",
        PlatformName = "SPX Instant",
        OrderId = "SPX-VN2693231211394",
        ShippingTrackingCode = "SPX-VN2693231211394",
        ShippingProvider = "SPX Instant",
        OrderStatus = "COMPLETED",
        TransactionDate = "2026-09-18",
        CompletionDate = "2026-09-18",
        InvoiceTime = "09:45",
        Currency = "VND",
        Subtotal = 292.199m,
        Tax = 3.0m,
        TotalAmount = 295.199m,
        Confidence = 0.95,
        LineItems =
        [
            new ReceiptLineItem { Description = "Vợt bóng bàn Double Fish 4A+", Amount = 292.199m },
            new ReceiptLineItem { Description = "Bảo hiểm người tiêu dùng", Amount = 3.0m }
        ]
    };

    private static ReceiptExtractionDto CorrectedTc01() => new()
    {
        DocumentType = "ECOMMERCE",
        DocumentStatus = "COMPLETED",
        MerchantName = "Double Fish Việt Nam",
        PlatformName = null,
        ShippingTrackingCode = "SPX-VN2693231211394",
        ShippingProvider = "SPX Instant",
        OrderStatus = "COMPLETED",
        TransactionDate = "2026-09-18",
        CompletionDate = "2026-09-18",
        InvoiceTime = "09:45",
        Currency = "VND",
        Subtotal = 295_199m,
        Tax = 0m,
        TotalAmount = 295_199m,
        Confidence = 0.95,
        LineItems =
        [
            new ReceiptLineItem { Description = "Vợt bóng bàn Double Fish 4A+", Amount = 292_199m },
            new ReceiptLineItem { Description = "Bảo hiểm người tiêu dùng", Amount = 3_000m }
        ]
    };

    private static ReceiptExtractionDto VinamilkDiscountedReceipt() => new()
    {
        DocumentType = "RETAIL_RECEIPT",
        DocumentStatus = "ISSUED",
        MerchantName = "Vinamilk",
        ReceiptNumber = "SAL.CH40411260922000147",
        InvoiceDate = "2026-09-22",
        InvoiceTime = "17:31",
        Currency = "VND",
        Subtotal = 183_114m,
        DiscountAmount = 2_828m,
        Tax = 0m,
        TotalAmount = 180_286m,
        Confidence = 0.95,
        LineItems =
        [
            new ReceiptLineItem { Description = "Sản phẩm sữa", Amount = 183_114m }
        ]
    };
}
