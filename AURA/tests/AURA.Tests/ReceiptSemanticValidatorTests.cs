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
    public void Ecommerce_without_line_items_is_not_auto_approved()
    {
        var facts = CorrectedTc01();
        facts.LineItems = [];

        var result = PolicyDecisionEngine.Evaluate(facts, 295_199m,
            utcNow: new DateTime(2026, 9, 20, 0, 0, 0, DateTimeKind.Utc));

        Assert.Equal("ESCALATE_FACT", result.Status);
        Assert.Contains("danh sách hàng hóa", result.Reason, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void Detects_paper_receipt_identifier_assigned_to_order_id()
    {
        var facts = new ReceiptExtractionDto
        {
            DocumentType = "RETAIL_RECEIPT",
            DocumentStatus = "ISSUED",
            MerchantName = "Phở Hai Thiền",
            OrderId = "HD-260921-002",
            InvoiceNumber = null,
            InvoiceDate = "2026-09-18",
            Currency = "VND",
            TotalAmount = 88_000m,
            Confidence = 0.95,
            LineItems = [new ReceiptLineItem { Description = "Phở bò tái", Amount = 88_000m }]
        };

        var issues = ReceiptSemanticValidator.Validate(facts);

        Assert.Contains(issues, issue => issue.Contains("invoiceNumber", StringComparison.Ordinal));
    }

    [Fact]
    public void Removes_only_an_exact_duplicate_paper_identifier_after_repair()
    {
        var facts = new ReceiptExtractionDto
        {
            DocumentType = "RETAIL_RECEIPT",
            InvoiceNumber = "HD-260921-002",
            OrderId = "HD-260921-002",
            ShippingTrackingCode = "DIFFERENT-CODE"
        };

        ReceiptSemanticValidator.NormalizeCanonicalFields(facts);

        Assert.Null(facts.OrderId);
        Assert.Equal("DIFFERENT-CODE", facts.ShippingTrackingCode);
        Assert.Contains(ReceiptSemanticValidator.Validate(facts),
            issue => issue.Contains("mã thừa", StringComparison.Ordinal));
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
}
