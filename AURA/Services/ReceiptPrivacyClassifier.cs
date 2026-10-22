using System.Text.RegularExpressions;
using AURA.Models;

namespace AURA.Services;

/// <summary>
/// Converts model markers and conservative backend pattern checks into an auditable
/// privacy-review signal. It supports triage only and never claims legal compliance.
/// </summary>
public static partial class ReceiptPrivacyClassifier
{
    private static readonly HashSet<string> AllowedCategories = new(StringComparer.Ordinal)
    {
        "PERSON_NAME", "PHONE", "EMAIL", "ADDRESS", "PERSONAL_ID",
        "PERSONAL_TAX_ID", "BANK_ACCOUNT", "PAYMENT_CARD", "HEALTH",
        "BIOMETRIC", "OTHER"
    };

    private static readonly HashSet<string> HighRiskCategories = new(StringComparer.Ordinal)
    {
        "PERSONAL_ID", "BANK_ACCOUNT", "PAYMENT_CARD", "HEALTH", "BIOMETRIC"
    };

    public static ReceiptExtractionDto Apply(ReceiptExtractionDto facts)
    {
        var categories = new HashSet<string>(StringComparer.Ordinal);
        foreach (var signal in facts.SuspiciousSignals)
        {
            const string prefix = "personal_data:";
            if (!signal.StartsWith(prefix, StringComparison.OrdinalIgnoreCase)) continue;
            var category = NormalizeCategory(signal[prefix.Length..]);
            if (AllowedCategories.Contains(category)) categories.Add(category);
        }

        // Email syntax is distinctive enough to check existing business text, while
        // receipt identifiers (notably a 10-digit Vietnamese tax ID) can resemble
        // phone numbers. Only free-text fields participate in the phone fallback;
        // model personal_data markers still cover other locations on the image.
        foreach (var value in EnumerateExistingText(facts))
        {
            if (EmailPattern().IsMatch(value)) categories.Add("EMAIL");
        }

        foreach (var value in EnumerateFreeText(facts))
        {
            if (PhonePattern().IsMatch(value)) categories.Add("PHONE");
        }

        facts.PersonalDataCategories = categories.OrderBy(value => value, StringComparer.Ordinal).ToList();
        facts.RequiresPrivacyReview = facts.PersonalDataCategories.Count > 0;
        facts.PrivacyRisk = facts.PersonalDataCategories.Any(HighRiskCategories.Contains)
            ? PrivacyRiskLevels.High
            : facts.RequiresPrivacyReview
                ? PrivacyRiskLevels.Review
                : PrivacyRiskLevels.None;

        if (facts.RequiresPrivacyReview)
        {
            AddDistinct(facts.Warnings,
                $"Phát hiện tín hiệu dữ liệu cá nhân ({string.Join(", ", facts.PersonalDataCategories)}); " +
                "không hiển thị giá trị gốc và yêu cầu người có thẩm quyền kiểm tra.");
        }

        return facts;
    }

    private static IEnumerable<string> EnumerateExistingText(ReceiptExtractionDto facts)
    {
        var values = new[]
        {
            facts.MerchantName, facts.TaxId, facts.MerchantId, facts.TerminalId,
            facts.PlatformName, facts.OrderId, facts.BookingId, facts.ShippingTrackingCode,
            facts.InvoiceNumber, facts.ReceiptNumber, facts.TransactionReference,
            facts.TotalAmountEvidence
        };

        return values.Where(value => !string.IsNullOrWhiteSpace(value)).Select(value => value!);
    }

    private static IEnumerable<string> EnumerateFreeText(ReceiptExtractionDto facts)
    {
        var values = new[] { facts.MerchantName, facts.PlatformName, facts.TotalAmountEvidence };
        return values.Where(value => !string.IsNullOrWhiteSpace(value)).Select(value => value!);
    }

    private static string NormalizeCategory(string value) =>
        value.Trim().Replace('-', '_').Replace(' ', '_').ToUpperInvariant();

    private static void AddDistinct(ICollection<string> values, string value)
    {
        if (!values.Contains(value, StringComparer.OrdinalIgnoreCase)) values.Add(value);
    }

    [GeneratedRegex(@"\b[A-Z0-9._%+-]+@[A-Z0-9.-]+\.[A-Z]{2,}\b", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant)]
    private static partial Regex EmailPattern();

    [GeneratedRegex(@"(?<!\d)(?:\+?84|0)(?:[ .-]?\d){9,10}(?!\d)", RegexOptions.CultureInvariant)]
    private static partial Regex PhonePattern();

}
