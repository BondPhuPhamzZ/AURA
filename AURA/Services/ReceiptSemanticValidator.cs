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
            AddFractionalVndIssue(issues, "discountAmount", facts.DiscountAmount);
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

        var isPaperDocument = IsPaperDocument(documentType);
        if (isPaperDocument && facts.LineItems.Count == 0)
        {
            issues.Add("chứng từ giấy thiếu lineItems nên chưa thể kiểm tra hàng hóa/dịch vụ theo policy");
        }

        AddDatePlacementIssues(issues, facts, isPaperDocument);
        if (isPaperDocument &&
            (!string.IsNullOrWhiteSpace(facts.OrderId) ||
             !string.IsNullOrWhiteSpace(facts.BookingId) ||
             !string.IsNullOrWhiteSpace(facts.ShippingTrackingCode)))
        {
            issues.Add(!HasPaperTraceableIdentifier(facts)
                ? "chứng từ giấy thiếu invoiceNumber, receiptNumber hoặc transactionReference nhưng mã nhìn thấy đang bị gán sang trường định danh số khác"
                : "chứng từ giấy còn chứa mã thừa trong orderId, bookingId hoặc shippingTrackingCode");
        }

        AddDuplicateIdentifierIssue(issues, "invoiceNumber", facts.InvoiceNumber,
            "receiptNumber", facts.ReceiptNumber);
        AddDuplicateIdentifierIssue(issues, "invoiceNumber", facts.InvoiceNumber,
            "transactionReference", facts.TransactionReference);
        AddDuplicateIdentifierIssue(issues, "receiptNumber", facts.ReceiptNumber,
            "transactionReference", facts.TransactionReference);

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
            - Với VAT_INVOICE, nhãn "Số hóa đơn"/"Invoice No" phải đi vào invoiceNumber.
              Với RETAIL_RECEIPT hoặc RESTAURANT_BILL, "Số biên nhận"/"Receipt No"/"Bill No"
              phải đi vào receiptNumber. Mã giao dịch riêng cho lần mua như "Check", "Transaction No",
              "Trace", "RRN" hoặc "Mã giao dịch" phải đi vào transactionReference.
            - Với VAT_INVOICE, RETAIL_RECEIPT hoặc RESTAURANT_BILL, ngày có nhãn hóa đơn/biên nhận
              đi vào invoiceDate. Nếu chứng từ giấy chỉ in ngày mua/giao dịch/thanh toán thì có thể
              đặt ngày đó vào transactionDate; backend sẽ chuẩn hóa thành ngày chứng từ để kiểm policy.
              Nếu ảnh chỉ cho thấy ngày hoàn tất/giao hàng thì giữ completionDate riêng, không
              sao chép ngày đó sang invoiceDate hoặc transactionDate.
            - Shop/store ID, POS/register ID, terminal ID, merchant ID, pager number, tax ID,
              serial hóa đơn và mẫu số không được dùng làm transactionReference.
            - Không đặt các định danh chứng từ giấy vào orderId, bookingId hay shippingTrackingCode.
            - Chứng từ giấy chỉ đủ dữ kiện để xét policy khi đọc được ít nhất một hàng hóa/dịch vụ
              đã mua. Nếu vùng chi tiết món hàng bị che, mờ hoặc không đọc được, không được suy đoán:
              giữ lineItems rỗng, thêm `lineItems` vào missingFields và mô tả nguyên nhân trong warnings.
            - Nếu ảnh in rõ `Giảm giá`, `Chiết khấu`, `Voucher`, `Khuyến mãi` hoặc một khoản giảm
              tương đương ở cấp toàn hóa đơn, đặt trị tuyệt đối không âm vào discountAmount.
              Ví dụ `-2.828` VND phải là discountAmount=2828. Không suy ra discountAmount chỉ từ
              chênh lệch số học. totalAmount phải là số cuối cùng thực trả và phép tính phải đối chiếu
              được theo subtotal + tax - discountAmount = totalAmount (hoặc subtotal đã gồm thuế).
              Điểm/tích lũy, số dư điểm, mã hoặc phần trăm voucher, tiền khách đưa, tiền thừa,
              số lượng và mã terminal/khách hàng không phải discountAmount. Nếu lineItems, subtotal
              và totalAmount đã bằng nhau thì không gán một số rời rạc thành discountAmount.
            - Không suy đoán từ tên file, kết quả mong đợi hoặc số tiền người dùng khai báo.
            - Chỉ trả về một JSON object đúng schema, không thêm Markdown hay giải thích.
            """;
    }

    public static ReceiptExtractionDto MarkUnresolved(ReceiptExtractionDto facts,
        IReadOnlyList<string> issues)
    {
        facts.ValidationIssues = issues.Distinct(StringComparer.OrdinalIgnoreCase).ToList();

        if (issues.Any(issue => issue.Contains("thiếu lineItems", StringComparison.OrdinalIgnoreCase)))
        {
            AddDistinct(facts.MissingFields, "lineItems");
            AddDistinct(facts.Warnings,
                "Không đọc được danh sách hàng hóa/dịch vụ; cần người kiểm tra trước khi đối chiếu policy.");
            facts.Confidence = Math.Min(facts.Confidence, 0.69);
        }

        return facts;
    }

    public static ReceiptExtractionDto MarkRepairAttempt(ReceiptExtractionDto facts,
        IReadOnlyList<string> originalIssues)
    {
        facts.SemanticRepairApplied = true;
        facts.SemanticRepairIssues = originalIssues
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();
        return facts;
    }

    public static ReceiptExtractionDto NormalizeCanonicalFields(ReceiptExtractionDto facts)
    {
        var documentType = facts.DocumentType?.Trim().ToUpperInvariant();
        if (!IsPaperDocument(documentType))
        {
            NormalizeNonImpactingDiscount(facts);
            return facts;
        }

        // A correction response may keep a previously misplaced value after filling a
        // canonical paper identifier. Removing exact duplicates is lossless and never
        // invents evidence or promotes a merchant/terminal identifier.
        var canonicalIdentifiers = new[]
        {
            facts.InvoiceNumber, facts.ReceiptNumber, facts.TransactionReference
        }.Where(value => !string.IsNullOrWhiteSpace(value)).ToArray();
        if (canonicalIdentifiers.Any(value => SameIdentifier(facts.OrderId, value))) facts.OrderId = null;
        if (canonicalIdentifiers.Any(value => SameIdentifier(facts.BookingId, value))) facts.BookingId = null;
        if (canonicalIdentifiers.Any(value => SameIdentifier(facts.ShippingTrackingCode, value)))
            facts.ShippingTrackingCode = null;

        // A paper receipt can label its only date as either the receipt date or the transaction
        // date. Both describe the purchase event that the reimbursement policy must age-check.
        // Promoting the sole transaction date to the canonical paper evidence date is a lossless
        // field normalization; it does not invent a date or copy a delivery/completion date.
        if (string.IsNullOrWhiteSpace(facts.InvoiceDate) &&
            !string.IsNullOrWhiteSpace(facts.TransactionDate))
        {
            facts.InvoiceDate = facts.TransactionDate;
            facts.TransactionDate = null;
        }
        else if (SameDate(facts.InvoiceDate, facts.TransactionDate))
        {
            facts.TransactionDate = null;
        }

        NormalizeNonImpactingDiscount(facts);
        return facts;
    }

    private static void AddDatePlacementIssues(List<string> issues, ReceiptExtractionDto facts,
        bool isPaperDocument)
    {
        if (!isPaperDocument) return;

        if (string.IsNullOrWhiteSpace(facts.InvoiceDate))
        {
            if (!string.IsNullOrWhiteSpace(facts.CompletionDate))
            {
                issues.Add("chứng từ giấy thiếu invoiceDate; completionDate không thể thay cho ngày hóa đơn/biên nhận");
            }

            return;
        }
    }

    private static void NormalizeNonImpactingDiscount(ReceiptExtractionDto facts)
    {
        if (facts.DiscountAmount is null or <= 0 ||
            !facts.Subtotal.HasValue || !facts.TotalAmount.HasValue ||
            facts.LineItems.Count == 0 || facts.LineItems.Any(item => !item.Amount.HasValue) ||
            facts.Tax.GetValueOrDefault() != 0)
        {
            return;
        }

        var lineTotal = facts.LineItems.Sum(item => item.Amount!.Value);
        if (!AreEqual(lineTotal, facts.Subtotal.Value) ||
            !AreEqual(facts.Subtotal.Value, facts.TotalAmount.Value))
        {
            return;
        }

        var ignoredValue = facts.DiscountAmount.Value;
        facts.DiscountAmount = null;
        facts.Warnings.Add(
            $"Bỏ qua discountAmount={ignoredValue} vì tổng dòng hàng, tạm tính và tổng thanh toán đều bằng " +
            $"{facts.TotalAmount.Value}; không có giá trước giảm riêng để đối chiếu.");
    }

    private static void AddFractionalVndIssue(List<string> issues, string field, decimal? value)
    {
        if (value.HasValue && value.Value != decimal.Truncate(value.Value))
            issues.Add($"{field}={value.Value} có phần thập phân dù tiền tệ là VND");
    }

    private static void AddLineTotalIssueWhenUnambiguous(List<string> issues, ReceiptExtractionDto facts)
    {
        if (facts.DiscountAmount is < 0)
            issues.Add($"discountAmount={facts.DiscountAmount.Value} phải là trị tuyệt đối không âm");

        if (!facts.TotalAmount.HasValue || facts.LineItems.Count == 0 ||
            facts.LineItems.Any(item => !item.Amount.HasValue))
            return;

        var lineTotal = facts.LineItems.Sum(item => item.Amount!.Value);
        var total = facts.TotalAmount.Value;
        var subtotalMatchesLines = facts.Subtotal.HasValue && AreEqual(lineTotal, facts.Subtotal.Value);
        var baseAmount = facts.Subtotal ?? lineTotal;

        if (facts.DiscountAmount.HasValue)
        {
            if (facts.Subtotal.HasValue && !subtotalMatchesLines)
                issues.Add($"tổng lineItems={lineTotal} không đối chiếu được với subtotal={facts.Subtotal.Value}");

            var discount = facts.DiscountAmount.Value;
            var tax = facts.Tax.GetValueOrDefault();
            if (discount > baseAmount + Math.Max(0, tax))
                issues.Add($"discountAmount={discount} vượt quá giá trị trước giảm giá={baseAmount + Math.Max(0, tax)}");

            var subtotalAlreadyIncludesTax = baseAmount - discount;
            var taxAddedAfterSubtotal = baseAmount + tax - discount;
            if (discount < 0 ||
                (!AreEqual(subtotalAlreadyIncludesTax, total) && !AreEqual(taxAddedAfterSubtotal, total)))
            {
                issues.Add($"subtotal/lineItems={baseAmount}, tax={tax}, discountAmount={discount} không đối chiếu được với totalAmount={total}");
            }

            return;
        }

        var subtotalAndTaxMatchTotal = subtotalMatchesLines &&
            (!facts.Tax.HasValue || AreEqual(facts.Subtotal!.Value + facts.Tax.Value, total));

        if (!AreEqual(lineTotal, total) && !subtotalAndTaxMatchTotal)
            issues.Add($"tổng lineItems={lineTotal} không đối chiếu được với totalAmount={total}; hãy đọc discountAmount từ khoản giảm giá/chiết khấu/voucher nếu ảnh có in rõ");
    }

    private static bool AreEqual(decimal left, decimal right) => Math.Abs(left - right) <= 0.01m;

    private static void AddDistinct(ICollection<string> values, string value)
    {
        if (!values.Contains(value, StringComparer.OrdinalIgnoreCase))
            values.Add(value);
    }

    private static bool IsPaperDocument(string? documentType) =>
        documentType is "VAT_INVOICE" or "RETAIL_RECEIPT" or "RESTAURANT_BILL";

    private static bool HasPaperTraceableIdentifier(ReceiptExtractionDto facts) =>
        !string.IsNullOrWhiteSpace(facts.InvoiceNumber) ||
        !string.IsNullOrWhiteSpace(facts.ReceiptNumber) ||
        !string.IsNullOrWhiteSpace(facts.TransactionReference);

    private static void AddDuplicateIdentifierIssue(List<string> issues,
        string leftName, string? left, string rightName, string? right)
    {
        if (SameIdentifier(left, right))
            issues.Add($"cùng một mã đang được gán đồng thời cho {leftName} và {rightName}");
    }

    private static bool SameIdentifier(string? left, string? right) =>
        !string.IsNullOrWhiteSpace(left) && !string.IsNullOrWhiteSpace(right) &&
        string.Equals(left.Trim(), right.Trim(), StringComparison.OrdinalIgnoreCase);

    private static bool SameDate(string? left, string? right) =>
        !string.IsNullOrWhiteSpace(left) && !string.IsNullOrWhiteSpace(right) &&
        string.Equals(left.Trim(), right.Trim(), StringComparison.Ordinal);

    private static bool ContainsAny(string? source, params string[] terms) =>
        !string.IsNullOrWhiteSpace(source) && terms.Any(term =>
            source.Contains(term, StringComparison.OrdinalIgnoreCase));
}
