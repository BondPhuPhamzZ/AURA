using AURA.Models;
using AURA.Services;
using Xunit;

namespace AURA.Tests;

public sealed class ReceiptPrivacyClassifierTests
{
    [Fact]
    public void Normalizes_model_markers_without_copying_raw_personal_values()
    {
        var facts = new ReceiptExtractionDto
        {
            SuspiciousSignals = ["personal_data:phone", "personal_data:ADDRESS"]
        };

        ReceiptPrivacyClassifier.Apply(facts);

        Assert.Equal(["ADDRESS", "PHONE"], facts.PersonalDataCategories);
        Assert.Equal(PrivacyRiskLevels.Review, facts.PrivacyRisk);
        Assert.True(facts.RequiresPrivacyReview);
        Assert.DoesNotContain(facts.Warnings, warning => warning.Contains("090", StringComparison.Ordinal));
    }

    [Fact]
    public void Treats_identity_and_financial_categories_as_high_risk()
    {
        var facts = new ReceiptExtractionDto
        {
            SuspiciousSignals = ["personal_data:PERSONAL_ID", "personal_data:BANK_ACCOUNT"]
        };

        ReceiptPrivacyClassifier.Apply(facts);

        Assert.Equal(PrivacyRiskLevels.High, facts.PrivacyRisk);
        Assert.True(facts.RequiresPrivacyReview);
    }

    [Fact]
    public void Detects_email_already_present_in_business_fields_as_a_fallback()
    {
        var facts = new ReceiptExtractionDto { MerchantName = "Contact user@example.com" };

        ReceiptPrivacyClassifier.Apply(facts);

        Assert.Contains("EMAIL", facts.PersonalDataCategories);
        Assert.DoesNotContain("user@example.com", string.Join(" ", facts.Warnings), StringComparison.Ordinal);
    }

    [Fact]
    public void Leaves_clean_receipts_out_of_privacy_review()
    {
        var facts = new ReceiptExtractionDto { MerchantName = "Cửa hàng AURA" };

        ReceiptPrivacyClassifier.Apply(facts);

        Assert.Empty(facts.PersonalDataCategories);
        Assert.Equal(PrivacyRiskLevels.None, facts.PrivacyRisk);
        Assert.False(facts.RequiresPrivacyReview);
    }

    [Fact]
    public void Policy_routes_a_complete_privacy_flagged_receipt_to_human_review()
    {
        var facts = new ReceiptExtractionDto
        {
            EvidenceContractVersion = ReceiptExtractionDto.CurrentEvidenceContractVersion,
            DocumentType = "RETAIL_RECEIPT",
            DocumentStatus = "ISSUED",
            MerchantName = "Cửa hàng AURA",
            ReceiptNumber = "RC-001",
            InvoiceDate = "2026-10-16",
            InvoiceDateEvidence = "16/10/2026",
            Currency = "VND",
            TotalAmount = 100_000m,
            TotalAmountSource = "PRINTED_FINAL_TOTAL",
            TotalAmountEvidence = "TỔNG THANH TOÁN 100.000 VND",
            LineItems = [new ReceiptLineItem { Description = "Văn phòng phẩm", Amount = 100_000m }],
            Confidence = 0.95,
            PersonalDataCategories = ["PHONE"],
            PrivacyRisk = PrivacyRiskLevels.Review,
            RequiresPrivacyReview = true
        };

        var decision = PolicyDecisionEngine.Evaluate(facts, 100_000m,
            utcNow: new DateTime(2026, 10, 17, 0, 0, 0, DateTimeKind.Utc));

        Assert.Equal("ESCALATE_PRIVACY", decision.Status);
        Assert.Contains("PHONE", decision.Reason, StringComparison.Ordinal);
    }
}
