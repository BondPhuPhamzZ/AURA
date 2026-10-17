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
    public void Normalizes_a_visible_chinese_invoice_date_and_removes_a_summary_pseudo_item()
    {
        var facts = CorrectedTc01();
        facts.DocumentType = "RETAIL_RECEIPT";
        facts.ReceiptNumber = "CN-20261017-01";
        facts.InvoiceDate = "2026-01-01";
        facts.InvoiceDateEvidence = "开票日期：2026年10月17日";
        facts.TransactionDate = null;
        facts.TransactionDateEvidence = null;
        facts.Currency = "RMB";
        facts.Subtotal = 88m;
        facts.TotalAmount = 88m;
        facts.TotalAmountEvidence = "价税合计 ¥88.00";
        facts.LineItems =
        [
            new ReceiptLineItem { Description = "办公用品", Amount = 88m },
            new ReceiptLineItem { Description = "价税合计", Amount = 88m }
        ];

        ReceiptSemanticValidator.NormalizeCanonicalFields(facts);

        Assert.Equal("2026-10-17", facts.InvoiceDate);
        Assert.Equal("CNY", facts.Currency);
        Assert.Single(facts.LineItems);
        Assert.Equal("办公用品", facts.LineItems[0].Description);
        Assert.DoesNotContain(ReceiptSemanticValidator.Validate(facts),
            issue => issue.Contains("totalAmountEvidence", StringComparison.Ordinal));

        facts.TotalAmount = 89m;
        Assert.Contains(ReceiptSemanticValidator.Validate(facts),
            issue => issue.Contains("totalAmountEvidence", StringComparison.Ordinal));
    }

    [Fact]
    public void Does_not_multiply_a_genuine_whole_vnd_amount()
    {
        var facts = CorrectedTc01();
        facts.Subtotal = 292m;
        facts.Tax = 3m;
        facts.TotalAmount = 295m;
        facts.TotalAmountEvidence = "Total 295 VND";
        facts.LineItems =
        [
            new ReceiptLineItem { Description = "Mặt hàng A", Amount = 292m },
            new ReceiptLineItem { Description = "Phí", Amount = 3m }
        ];

        Assert.Empty(ReceiptSemanticValidator.Validate(facts));
        Assert.Equal(295m, facts.TotalAmount);
    }

    [Fact]
    public void Rejects_a_circle_k_style_inferred_total_when_the_final_value_is_torn_off()
    {
        var facts = new ReceiptExtractionDto
        {
            EvidenceContractVersion = ReceiptExtractionDto.CurrentEvidenceContractVersion,
            DocumentType = "RETAIL_RECEIPT",
            DocumentStatus = "COMPLETED",
            MerchantName = "Circle K",
            ReceiptNumber = "QQR",
            TaxAuthorityCode = "M1-26-CF3SK-11222129209",
            InvoiceDate = "2026-10-06",
            InvoiceDateEvidence = "Tue 06 Oct 2026",
            Currency = "VND",
            Subtotal = 21_000m,
            TotalAmount = 21_000m,
            TotalAmountSource = "INFERRED",
            TotalAmountEvidence = null,
            Confidence = 0.95,
            LineItems =
            [
                new ReceiptLineItem { Description = "Khăn giấy ướt", Amount = 14_000m },
                new ReceiptLineItem { Description = "Nước suối", Amount = 7_000m }
            ]
        };

        var issues = ReceiptSemanticValidator.Validate(facts);
        ReceiptSemanticValidator.MarkUnresolved(facts, issues);
        var decision = PolicyDecisionEngine.Evaluate(facts, 21_000m,
            utcNow: new DateTime(2026, 10, 6, 0, 0, 0, DateTimeKind.Utc));

        Assert.Contains(issues, issue => issue.Contains("suy ra", StringComparison.OrdinalIgnoreCase));
        Assert.Contains("totalAmount", facts.MissingFields);
        Assert.Equal(0.69, facts.Confidence);
        Assert.Equal("ESCALATE_FACT", decision.Status);
    }

    [Fact]
    public void Accepts_only_a_visible_final_row_whose_vnd_value_matches_total_amount()
    {
        var facts = CorrectedTc01();

        Assert.Empty(ReceiptSemanticValidator.Validate(facts));

        facts.TotalAmountEvidence = "Tạm tính 295.199 đ";
        var wrongLabelIssues = ReceiptSemanticValidator.Validate(facts);
        Assert.Contains(wrongLabelIssues,
            issue => issue.Contains("nhãn tổng phải trả", StringComparison.OrdinalIgnoreCase));

        facts.TotalAmountEvidence = "Tổng thanh toán 295.198 đ";
        var wrongValueIssues = ReceiptSemanticValidator.Validate(facts);
        Assert.Contains(wrongValueIssues,
            issue => issue.Contains("không chứa đúng", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public void Corrects_vietnamese_day_first_date_and_twelve_hour_time_from_raw_evidence()
    {
        var facts = VinamilkDiscountedReceipt();
        facts.InvoiceDate = "2026-04-10";
        facts.InvoiceDateEvidence = "04-10-26";
        facts.InvoiceTime = "2:17 PM";

        ReceiptSemanticValidator.NormalizeCanonicalFields(facts);

        Assert.Equal("2026-10-04", facts.InvoiceDate);
        Assert.Equal("14:17", facts.InvoiceTime);
        Assert.Contains(facts.Warnings,
            warning => warning.Contains("2026-10-04", StringComparison.Ordinal));
        Assert.Empty(ReceiptSemanticValidator.Validate(facts));
    }

    [Fact]
    public void Accepts_fractional_included_vat_without_promoting_it_to_the_final_total()
    {
        var facts = VinamilkDiscountedReceipt();
        facts.Subtotal = 60_000m;
        facts.DiscountAmount = null;
        facts.Tax = 4_444.44m;
        facts.TotalAmount = 60_000m;
        facts.TotalAmountEvidence = "TỔNG CỘNG 60.000";
        facts.LineItems =
        [
            new ReceiptLineItem { Description = "Sữa", Amount = 38_000m },
            new ReceiptLineItem { Description = "Kem", Amount = 22_000m }
        ];

        Assert.Empty(ReceiptSemanticValidator.Validate(facts));
    }

    [Fact]
    public void Supporting_identifiers_cannot_be_duplicated_into_transaction_fields()
    {
        var facts = VinamilkDiscountedReceipt();
        facts.ReceiptNumber = "622002261001104";
        facts.TransactionReference = null;
        facts.TaxAuthorityCode = "622002261001104";

        Assert.Contains(ReceiptSemanticValidator.Validate(facts), issue =>
            issue.Contains("receiptNumber", StringComparison.Ordinal) &&
            issue.Contains("taxAuthorityCode", StringComparison.Ordinal));
    }

    [Fact]
    public void Ptt_receipt_number_and_distinct_tax_authority_code_are_kept_separate()
    {
        var facts = VinamilkDiscountedReceipt();
        facts.ReceiptNumber = "622002261001104";
        facts.TransactionReference = null;
        facts.TaxAuthorityCode = "M1-26-MX4H6-10112701104";

        Assert.Empty(ReceiptSemanticValidator.Validate(facts));
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
    public void Canonicalizes_exact_duplicate_retail_identifiers_without_losing_traceability()
    {
        var facts = VinamilkDiscountedReceipt();
        facts.InvoiceNumber = "622002261001104";
        facts.ReceiptNumber = "622002261001104";
        facts.TransactionReference = "622002261001104";
        facts.TaxAuthorityCode = "M1-26-MX4H6-10112701104";

        ReceiptSemanticValidator.NormalizeCanonicalFields(facts);

        Assert.Null(facts.InvoiceNumber);
        Assert.Equal("622002261001104", facts.ReceiptNumber);
        Assert.Null(facts.TransactionReference);
        Assert.Equal("M1-26-MX4H6-10112701104", facts.TaxAuthorityCode);
        Assert.Contains(facts.Warnings,
            warning => warning.Contains("bị lặp", StringComparison.OrdinalIgnoreCase));
        Assert.Empty(ReceiptSemanticValidator.Validate(facts));
    }

    [Fact]
    public void Keeps_distinct_retail_identifiers_unchanged()
    {
        var facts = VinamilkDiscountedReceipt();
        facts.InvoiceNumber = "INV-001";
        facts.ReceiptNumber = "RC-001";
        facts.TransactionReference = "RRN-001";

        ReceiptSemanticValidator.NormalizeCanonicalFields(facts);

        Assert.Equal("INV-001", facts.InvoiceNumber);
        Assert.Equal("RC-001", facts.ReceiptNumber);
        Assert.Equal("RRN-001", facts.TransactionReference);
    }

    [Fact]
    public void Removes_a_total_summary_row_but_keeps_real_line_items()
    {
        var facts = VinamilkDiscountedReceipt();
        facts.Subtotal = 12_000m;
        facts.DiscountAmount = null;
        facts.Tax = null;
        facts.TotalAmount = 12_000m;
        facts.TotalAmountEvidence = "Tổng số/Tổng cộng: 12,000";
        facts.LineItems =
        [
            new ReceiptLineItem { Description = "T3.Trà Ô long Tea plus 45", Amount = 12_000m },
            new ReceiptLineItem { Description = "Tổng số/Tổng cộng:", Amount = 12_000m }
        ];

        ReceiptSemanticValidator.NormalizeCanonicalFields(facts);

        var item = Assert.Single(facts.LineItems);
        Assert.Equal("T3.Trà Ô long Tea plus 45", item.Description);
        Assert.Contains(facts.Warnings,
            warning => warning.Contains("lặp vào lineItems", StringComparison.OrdinalIgnoreCase));
        Assert.Empty(ReceiptSemanticValidator.Validate(facts));
    }

    [Fact]
    public void Does_not_remove_a_real_product_merely_containing_the_word_total()
    {
        var facts = VinamilkDiscountedReceipt();
        facts.Subtotal = 120_000m;
        facts.DiscountAmount = null;
        facts.Tax = 0;
        facts.TotalAmount = 120_000m;
        facts.TotalAmountEvidence = "Tổng cộng 120.000";
        facts.LineItems =
        [
            new ReceiptLineItem { Description = "Total Care Shampoo", Amount = 120_000m }
        ];

        ReceiptSemanticValidator.NormalizeCanonicalFields(facts);

        Assert.Single(facts.LineItems);
        Assert.Equal("Total Care Shampoo", facts.LineItems[0].Description);
    }

    [Fact]
    public void Corrects_a_wrong_total_only_when_printed_row_subtotal_and_line_items_corroborate()
    {
        var facts = VinamilkDiscountedReceipt();
        facts.MerchantName = "CO.OPSMILE - 85 TUE TINH";
        facts.ReceiptNumber = "M1-26-AJWGW-21180136552";
        facts.InvoiceDate = "2026-10-05";
        facts.InvoiceDateEvidence = "05/10/2026";
        facts.InvoiceTime = "08:29:02";
        facts.Subtotal = 60_000m;
        facts.DiscountAmount = 440_000m;
        facts.Tax = 4_444.44m;
        facts.TotalAmount = 4_444.44m;
        facts.TotalAmountEvidence = "TONG CONG 60.000\nBao gom thue GTGT 8%\n4444.44";
        facts.MissingFields.Add("totalAmount");
        facts.LineItems =
        [
            new ReceiptLineItem { Description = "Cream NSPN 380g", Amount = 22_000m },
            new ReceiptLineItem { Description = "VNM ít đường 100% 1L", Amount = 38_000m }
        ];

        ReceiptSemanticValidator.NormalizeCanonicalFields(facts);

        Assert.Equal(60_000m, facts.TotalAmount);
        Assert.Null(facts.DiscountAmount);
        Assert.DoesNotContain(facts.MissingFields,
            field => string.Equals(field, "totalAmount", StringComparison.OrdinalIgnoreCase));
        Assert.Empty(ReceiptSemanticValidator.Validate(facts));
        Assert.Equal("AUTO_APPROVE",
            PolicyDecisionEngine.Evaluate(facts, 60_000m,
                utcNow: new DateTime(2026, 10, 6, 0, 0, 0, DateTimeKind.Utc)).Status);
    }

    [Fact]
    public void Does_not_reconstruct_total_when_the_labeled_value_lacks_independent_corroboration()
    {
        var facts = VinamilkDiscountedReceipt();
        facts.Subtotal = 60_000m;
        facts.DiscountAmount = null;
        facts.Tax = 0;
        facts.TotalAmount = 4_444.44m;
        facts.TotalAmountEvidence = "TỔNG CỘNG 59.000";
        facts.LineItems =
        [
            new ReceiptLineItem { Description = "Sữa", Amount = 38_000m },
            new ReceiptLineItem { Description = "Kem", Amount = 22_000m }
        ];

        ReceiptSemanticValidator.NormalizeCanonicalFields(facts);

        Assert.Equal(4_444.44m, facts.TotalAmount);
        Assert.NotEmpty(ReceiptSemanticValidator.Validate(facts));
    }

    [Fact]
    public void Does_not_treat_merchandise_total_as_the_final_payment_row()
    {
        var facts = VinamilkDiscountedReceipt();
        facts.Subtotal = 60_000m;
        facts.DiscountAmount = 5_000m;
        facts.Tax = 0;
        facts.TotalAmount = 4_444.44m;
        facts.TotalAmountEvidence = "Tổng tiền hàng 60.000\nTổng cộng 55.000";
        facts.LineItems =
        [
            new ReceiptLineItem { Description = "Sữa", Amount = 38_000m },
            new ReceiptLineItem { Description = "Kem", Amount = 22_000m }
        ];

        ReceiptSemanticValidator.NormalizeCanonicalFields(facts);

        Assert.Equal(4_444.44m, facts.TotalAmount);
        Assert.Equal(5_000m, facts.DiscountAmount);
        Assert.NotEmpty(ReceiptSemanticValidator.Validate(facts));
    }

    [Fact]
    public void Damaged_short_alphabetic_receipt_prefix_is_not_traceable()
    {
        var facts = new ReceiptExtractionDto
        {
            DocumentType = "RESTAURANT_BILL",
            ReceiptNumber = "RCF",
            InvoiceDate = "2026-10-05",
            InvoiceDateEvidence = "05/10/2026",
            Currency = "VND",
            TotalAmount = 315_000m,
            TotalAmountSource = "PRINTED_FINAL_TOTAL",
            TotalAmountEvidence = "TỔNG THANH TOÁN 315.000 đ",
            Confidence = 0.95,
            LineItems = [new ReceiptLineItem { Description = "Suất ăn làm việc", Amount = 315_000m }]
        };

        var issues = ReceiptSemanticValidator.Validate(facts);

        Assert.Contains(issues, issue => issue.Contains("receiptNumber='RCF'", StringComparison.Ordinal));
        facts.ValidationIssues = issues.ToList();
        Assert.Equal("ESCALATE_FACT",
            PolicyDecisionEngine.Evaluate(facts, 315_000m,
                utcNow: new DateTime(2026, 10, 6, 0, 0, 0, DateTimeKind.Utc)).Status);
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
        EvidenceContractVersion = ReceiptExtractionDto.CurrentEvidenceContractVersion,
        DocumentType = "ECOMMERCE",
        DocumentStatus = "COMPLETED",
        MerchantName = "Double Fish Việt Nam",
        PlatformName = null,
        ShippingTrackingCode = "SPX-VN2693231211394",
        ShippingProvider = "SPX Instant",
        OrderStatus = "COMPLETED",
        TransactionDate = "2026-09-18",
        TransactionDateEvidence = "18/09/2026",
        CompletionDate = "2026-09-18",
        InvoiceTime = "09:45",
        Currency = "VND",
        Subtotal = 295_199m,
        Tax = 0m,
        TotalAmount = 295_199m,
        TotalAmountSource = "PRINTED_FINAL_TOTAL",
        TotalAmountEvidence = "Thành tiền 295.199 đ",
        Confidence = 0.95,
        LineItems =
        [
            new ReceiptLineItem { Description = "Vợt bóng bàn Double Fish 4A+", Amount = 292_199m },
            new ReceiptLineItem { Description = "Bảo hiểm người tiêu dùng", Amount = 3_000m }
        ]
    };

    private static ReceiptExtractionDto VinamilkDiscountedReceipt() => new()
    {
        EvidenceContractVersion = ReceiptExtractionDto.CurrentEvidenceContractVersion,
        DocumentType = "RETAIL_RECEIPT",
        DocumentStatus = "ISSUED",
        MerchantName = "Vinamilk",
        ReceiptNumber = "SAL.CH40411260922000147",
        InvoiceDate = "2026-09-22",
        InvoiceDateEvidence = "22/09/2026",
        InvoiceTime = "17:31",
        Currency = "VND",
        Subtotal = 183_114m,
        DiscountAmount = 2_828m,
        Tax = 0m,
        TotalAmount = 180_286m,
        TotalAmountSource = "PRINTED_FINAL_TOTAL",
        TotalAmountEvidence = "Tổng thanh toán 180.286 VND",
        Confidence = 0.95,
        LineItems =
        [
            new ReceiptLineItem { Description = "Sản phẩm sữa", Amount = 183_114m }
        ]
    };
}
