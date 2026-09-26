using AURA.Models;

namespace AURA.Services;

/// <summary>
/// Validates meaning across otherwise schema-valid receipt fields. Structured output
/// guarantees JSON shape, but it cannot guarantee that Vietnamese monetary punctuation
/// or the document category was interpreted correctly.
/// </summary>
public static class ReceiptSemanticValidator
{
    private static readonly HashSet<string> SupportedDocumentTypes = new(StringComparer.Ordinal)
    {
        "VAT_INVOICE", "RETAIL_RECEIPT", "RESTAURANT_BILL", "RIDE_HAILING", "ECOMMERCE", "OTHER"
    };

    public static IReadOnlyList<string> Validate(ReceiptExtractionDto facts)
    {
        var issues = new List<string>();
        var documentType = facts.DocumentType?.Trim().ToUpperInvariant();

        if (!string.IsNullOrWhiteSpace(documentType) && !SupportedDocumentTypes.Contains(documentType))
            issues.Add($"documentType '{facts.DocumentType}' không thuộc tập giá trị được hỗ trợ");

        if (string.Equals(facts.Currency, "VND", StringComparison.OrdinalIgnoreCase))
        {
            AddFractionalVndIssue(issues, "subtotal", facts.Subtotal);
            AddFractionalVndIssue(issues, "tax", facts.Tax);
            AddFractionalVndIssue(issues, "totalAmount", facts.TotalAmount);

            for (var index = 0; index < facts.LineItems.Count; index++)
            {
                AddFractionalVndIssue(issues, $"lineItems[{index}].unitPrice", facts.LineItems[index].UnitPrice);
                AddFractionalVndIssue(issues, $"lineItems[{index}].amount", facts.LineItems[index].Amount);
            }
        }

        if (string.Equals(documentType, "RIDE_HAILING", StringComparison.Ordinal) &&
            (!string.IsNullOrWhiteSpace(facts.ShippingTrackingCode) ||
             !string.IsNullOrWhiteSpace(facts.ShippingProvider)))
        {
            issues.Add("documentType RIDE_HAILING mâu thuẫn với dữ kiện vận chuyển hàng hóa");
        }

        var isNumberedPaperDocument = IsNumberedPaperDocument(documentType);
        if (isNumberedPaperDocument &&
            (!string.IsNullOrWhiteSpace(facts.OrderId) ||
             !string.IsNullOrWhiteSpace(facts.BookingId) ||
             !string.IsNullOrWhiteSpace(facts.ShippingTrackingCode)))
        {
            issues.Add(string.IsNullOrWhiteSpace(facts.InvoiceNumber)
                ? "chứng từ giấy thiếu invoiceNumber nhưng mã nhìn thấy đang bị gán sang trường định danh số khác"
                : "chứng từ giấy còn chứa mã thừa trong orderId, bookingId hoặc shippingTrackingCode");
        }

        if (!string.IsNullOrWhiteSpace(facts.OrderId) &&
            !string.IsNullOrWhiteSpace(facts.ShippingTrackingCode) &&
            string.Equals(facts.OrderId.Trim(), facts.ShippingTrackingCode.Trim(), StringComparison.OrdinalIgnoreCase))
        {
            issues.Add("cùng một mã đang được gán đồng thời cho orderId và shippingTrackingCode");
        }

        AddLineTotalIssueWhenUnambiguous(issues, facts);
        return issues.Distinct(StringComparer.OrdinalIgnoreCase).ToArray();
    }

    public static string BuildRepairInstruction(IReadOnlyList<string> issues)
    {
        var details = string.Join("\n", issues.Select(issue => $"- {issue}"));
        return $"""
            Đọc lại ảnh và sửa JSON vì kết quả trước có các mâu thuẫn semantic sau:
            {details}

            Quy tắc bắt buộc khi sửa:
            - Với VND, dấu chấm hoặc dấu phẩy giữa các nhóm ba chữ số là dấu phân cách hàng nghìn.
              Ví dụ: 295.199 đ phải là JSON number 295199; 3.000 đ phải là 3000; không trả 295.199 hoặc 3.0.
            - Màn hình đơn hàng có sản phẩm, trạng thái giao hàng và mã vận chuyển SPX/GHN/GHTK/J&T là
              ECOMMERCE, không phải RIDE_HAILING. Mã vận chuyển chỉ đi vào shippingTrackingCode.
            - RIDE_HAILING chỉ dùng cho chuyến đi/dịch vụ vận chuyển hành khách có trip/booking/receipt ID.
            - Với VAT_INVOICE, RETAIL_RECEIPT hoặc RESTAURANT_BILL, nhãn "Số hóa đơn/biên nhận"
              phải đi vào invoiceNumber, không đi vào orderId, bookingId hay shippingTrackingCode.
            - Không suy đoán từ tên file, kết quả mong đợi hoặc số tiền người dùng khai báo.
            - Chỉ trả về một JSON object đúng schema, không thêm Markdown hay giải thích.
            """;
    }

    public static ReceiptExtractionDto MarkUnresolved(ReceiptExtractionDto facts,
        IReadOnlyList<string> issues)
    {
        facts.ValidationIssues = issues.Distinct(StringComparer.OrdinalIgnoreCase).ToList();
        return facts;
    }

    public static ReceiptExtractionDto NormalizeCanonicalFields(ReceiptExtractionDto facts)
    {
        var documentType = facts.DocumentType?.Trim().ToUpperInvariant();
        if (!IsNumberedPaperDocument(documentType) || string.IsNullOrWhiteSpace(facts.InvoiceNumber))
            return facts;

        // A correction response may keep the old misplaced value after also filling
        // invoiceNumber. Removing only exact duplicates is lossless and never invents evidence.
        if (SameIdentifier(facts.OrderId, facts.InvoiceNumber)) facts.OrderId = null;
        if (SameIdentifier(facts.BookingId, facts.InvoiceNumber)) facts.BookingId = null;
        if (SameIdentifier(facts.ShippingTrackingCode, facts.InvoiceNumber)) facts.ShippingTrackingCode = null;
        return facts;
    }

    private static void AddFractionalVndIssue(List<string> issues, string field, decimal? value)
    {
        if (value.HasValue && value.Value != decimal.Truncate(value.Value))
            issues.Add($"{field}={value.Value} có phần thập phân dù tiền tệ là VND");
    }

    private static void AddLineTotalIssueWhenUnambiguous(List<string> issues, ReceiptExtractionDto facts)
    {
        if (!facts.TotalAmount.HasValue || facts.LineItems.Count == 0 ||
            facts.LineItems.Any(item => !item.Amount.HasValue))
            return;

        // Discounts/vouchers legitimately make the item sum differ from the final amount.
        // Only flag arithmetic when the model did not report such an adjustment.
        if (facts.Warnings.Any(warning => ContainsAny(warning, "discount", "voucher", "giảm giá", "khuyến mãi")))
            return;

        var lineTotal = facts.LineItems.Sum(item => item.Amount!.Value);
        var total = facts.TotalAmount.Value;
        var subtotalMatchesLines = facts.Subtotal.HasValue && AreEqual(lineTotal, facts.Subtotal.Value);
        var subtotalAndTaxMatchTotal = subtotalMatchesLines &&
            (!facts.Tax.HasValue || AreEqual(facts.Subtotal!.Value + facts.Tax.Value, total));

        if (!AreEqual(lineTotal, total) && !subtotalAndTaxMatchTotal)
            issues.Add($"tổng lineItems={lineTotal} không đối chiếu được với totalAmount={total}");
    }

    private static bool AreEqual(decimal left, decimal right) => Math.Abs(left - right) <= 0.01m;

    private static bool IsNumberedPaperDocument(string? documentType) =>
        documentType is "VAT_INVOICE" or "RETAIL_RECEIPT" or "RESTAURANT_BILL";

    private static bool SameIdentifier(string? left, string? right) =>
        !string.IsNullOrWhiteSpace(left) && !string.IsNullOrWhiteSpace(right) &&
        string.Equals(left.Trim(), right.Trim(), StringComparison.OrdinalIgnoreCase);

    private static bool ContainsAny(string? source, params string[] terms) =>
        !string.IsNullOrWhiteSpace(source) && terms.Any(term =>
            source.Contains(term, StringComparison.OrdinalIgnoreCase));
}
