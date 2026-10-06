using System.Globalization;
using System.Text;
using System.Text.RegularExpressions;
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

    private static readonly string[] VisibleDateFormats =
    [
        "yyyy-MM-dd", "yyyy/M/d", "yyyy/MM/dd", "yyyy.M.d",
        "d/M/yyyy", "dd/MM/yyyy", "d-M-yyyy", "dd-MM-yyyy", "d.M.yyyy", "dd.MM.yyyy",
        "d/M/yy", "dd/MM/yy", "d-M-yy", "dd-MM-yy", "d.M.yy", "dd.MM.yy",
        "ddd dd MMM yyyy", "dd MMM yyyy", "ddd d MMM yyyy", "d MMM yyyy"
    ];

    private static readonly string[] VisibleTimeFormats =
    [
        "H:mm", "HH:mm", "H:mm:ss", "HH:mm:ss",
        "h:mm tt", "hh:mm tt", "h:mm:ss tt", "hh:mm:ss tt"
    ];

    public static IReadOnlyList<string> Validate(ReceiptExtractionDto facts)
    {
        var issues = new List<string>();
        var documentType = facts.DocumentType?.Trim().ToUpperInvariant();

        if (facts.EvidenceContractVersion is not 0 and not ReceiptExtractionContract.CurrentEvidenceContractVersion)
            issues.Add($"evidenceContractVersion={facts.EvidenceContractVersion} không được hỗ trợ");

        if (!string.IsNullOrWhiteSpace(documentType) && !SupportedDocumentTypes.Contains(documentType))
            issues.Add($"documentType '{facts.DocumentType}' không thuộc tập giá trị được hỗ trợ");

        if (string.Equals(facts.Currency, "VND", StringComparison.OrdinalIgnoreCase))
        {
            AddFractionalVndIssue(issues, "subtotal", facts.Subtotal);
            AddFractionalVndIssue(issues, "discountAmount", facts.DiscountAmount);
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
        AddSupportingIdentifierCollisionIssues(issues, facts);

        if (!string.IsNullOrWhiteSpace(facts.OrderId) &&
            !string.IsNullOrWhiteSpace(facts.ShippingTrackingCode) &&
            string.Equals(facts.OrderId.Trim(), facts.ShippingTrackingCode.Trim(), StringComparison.OrdinalIgnoreCase))
        {
            issues.Add("cùng một mã đang được gán đồng thời cho orderId và shippingTrackingCode");
        }

        if (facts.EvidenceContractVersion >= ReceiptExtractionContract.CurrentEvidenceContractVersion)
        {
            AddDateEvidenceIssues(issues, facts);
            AddFinalTotalEvidenceIssues(issues, facts);
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
              Một khoản VAT được in là đã bao gồm trong tổng có thể có phần lẻ thập phân; không được nâng
              khoản VAT đó thành totalAmount và không được thêm nó lần nữa vào tổng đã gồm thuế.
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
              `Mã CQT` chỉ đi vào taxAuthorityCode; `Ký hiệu`/`Serial No` chỉ đi vào invoiceSerial;
              `Số chứng từ` chỉ đi vào documentNumber; `POS No` chỉ đi vào posNumber. Trên phiếu
              tính tiền, số có nhãn `PTT` là receiptNumber, không đồng thời là transactionReference.
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
            - Chép nguyên văn ngày nhìn thấy vào invoiceDateEvidence hoặc transactionDateEvidence.
              Hóa đơn Việt Nam dùng thứ tự ngày-tháng-năm: 04-10-26 là 2026-10-04.
            - totalAmountSource chỉ là PRINTED_FINAL_TOTAL khi cả nhãn tổng phải trả và con số trên
              chính dòng đó đều nhìn thấy; chép nguyên dòng vào totalAmountEvidence. Nếu giá trị dòng
              tổng bị rách/cắt/che thì dùng NOT_VISIBLE, totalAmount=null và ghi cảnh báo. Không dùng
              subtotal, tổng dòng hàng, tiền khách đưa, tiền thối hay VAT đã bao gồm để dựng tổng cuối.
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

        if (issues.Any(IsFinalTotalEvidenceIssue))
        {
            AddDistinct(facts.MissingFields, "totalAmount");
            AddDistinct(facts.Warnings,
                "Không xác minh được dòng tổng thanh toán cuối được in trực tiếp; cần người kiểm tra.");
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
        facts.TotalAmountSource = facts.TotalAmountSource?.Trim().ToUpperInvariant();
        NormalizeInvoiceTime(facts);
        facts.InvoiceDate = NormalizeDateFromEvidence(
            facts.InvoiceDate, facts.InvoiceDateEvidence, "invoiceDate", facts.Warnings);
        facts.TransactionDate = NormalizeDateFromEvidence(
            facts.TransactionDate, facts.TransactionDateEvidence, "transactionDate", facts.Warnings);

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
            facts.InvoiceDateEvidence = facts.TransactionDateEvidence;
            facts.TransactionDate = null;
            facts.TransactionDateEvidence = null;
        }
        else if (SameDate(facts.InvoiceDate, facts.TransactionDate))
        {
            facts.TransactionDate = null;
            facts.TransactionDateEvidence = null;
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

    private static void AddDateEvidenceIssues(List<string> issues, ReceiptExtractionDto facts)
    {
        AddDateEvidenceIssue(issues, "invoiceDate", facts.InvoiceDate, facts.InvoiceDateEvidence);
        AddDateEvidenceIssue(issues, "transactionDate", facts.TransactionDate, facts.TransactionDateEvidence);
    }

    private static void AddDateEvidenceIssue(List<string> issues, string field, string? normalized, string? evidence)
    {
        if (string.IsNullOrWhiteSpace(normalized) && string.IsNullOrWhiteSpace(evidence)) return;

        if (string.IsNullOrWhiteSpace(evidence))
        {
            issues.Add($"{field} có giá trị nhưng thiếu bằng chứng ngày nguyên văn từ ảnh");
            return;
        }

        if (!TryParseVisibleDate(evidence, out var evidenceDate))
        {
            issues.Add($"không đọc chắc chắn được ngày nguyên văn cho {field}: '{evidence}'");
            return;
        }

        var expected = evidenceDate.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture);
        if (!string.Equals(normalized, expected, StringComparison.Ordinal))
            issues.Add($"{field}={normalized} không khớp bằng chứng ngày nguyên văn '{evidence}' ({expected})");
    }

    private static void AddFinalTotalEvidenceIssues(List<string> issues, ReceiptExtractionDto facts)
    {
        var source = facts.TotalAmountSource?.Trim().ToUpperInvariant();
        if (!string.Equals(source, "PRINTED_FINAL_TOTAL", StringComparison.Ordinal))
        {
            issues.Add(source switch
            {
                "NOT_VISIBLE" => "dòng tổng thanh toán cuối không hiển thị đầy đủ trên ảnh",
                "AMBIGUOUS" => "dòng tổng thanh toán cuối còn mơ hồ trên ảnh",
                "INFERRED" => "totalAmount chỉ được suy ra, không phải dòng tổng thanh toán cuối được in trực tiếp",
                _ => "thiếu totalAmountSource chứng minh dòng tổng thanh toán cuối được in trực tiếp"
            });

            if (facts.TotalAmount.HasValue)
                issues.Add($"totalAmount={facts.TotalAmount.Value} không được phép khi totalAmountSource={source ?? "null"}");
            return;
        }

        if (facts.TotalAmount is null or <= 0)
        {
            issues.Add("totalAmountSource=PRINTED_FINAL_TOTAL nhưng thiếu totalAmount hợp lệ");
            return;
        }

        if (string.IsNullOrWhiteSpace(facts.TotalAmountEvidence))
        {
            issues.Add("thiếu totalAmountEvidence nguyên văn cho dòng tổng thanh toán cuối");
            return;
        }

        var normalizedEvidence = NormalizeSearchText(facts.TotalAmountEvidence);
        if (!ContainsFinalTotalLabel(normalizedEvidence))
            issues.Add($"totalAmountEvidence không có nhãn tổng phải trả đáng tin cậy: '{facts.TotalAmountEvidence}'");

        if (string.Equals(facts.Currency, "VND", StringComparison.OrdinalIgnoreCase) &&
            !ContainsMatchingVndAmount(facts.TotalAmountEvidence, facts.TotalAmount.Value))
        {
            issues.Add($"totalAmountEvidence không chứa đúng totalAmount={facts.TotalAmount.Value}: '{facts.TotalAmountEvidence}'");
        }
    }

    private static bool ContainsFinalTotalLabel(string normalizedEvidence)
    {
        var padded = $" {normalizedEvidence} ";
        string[] labels =
        [
            "tong cong", "tong thanh toan", "tong tien", "thanh tien", "tien can thanh toan",
            "so tien phai thu", "grand total", "amount due", "total due", "final total", "payable",
            "total"
        ];
        return labels.Any(label => padded.Contains($" {label} ", StringComparison.Ordinal));
    }

    private static bool ContainsMatchingVndAmount(string evidence, decimal total)
    {
        foreach (Match match in Regex.Matches(evidence, @"(?<!\d)\d+(?:[\s.,]\d{3})*(?!\d)", RegexOptions.CultureInvariant))
        {
            var digits = new string(match.Value.Where(char.IsDigit).ToArray());
            if (decimal.TryParse(digits, NumberStyles.None, CultureInfo.InvariantCulture, out var parsed) &&
                AreEqual(parsed, total))
                return true;
        }

        return false;
    }

    private static bool IsFinalTotalEvidenceIssue(string issue) =>
        issue.Contains("tổng thanh toán cuối", StringComparison.OrdinalIgnoreCase) ||
        issue.Contains("totalAmountSource", StringComparison.OrdinalIgnoreCase) ||
        issue.Contains("totalAmountEvidence", StringComparison.OrdinalIgnoreCase);

    private static string? NormalizeDateFromEvidence(string? normalized, string? evidence,
        string fieldName, ICollection<string> warnings)
    {
        if (string.IsNullOrWhiteSpace(evidence) || !TryParseVisibleDate(evidence, out var evidenceDate))
            return normalized;

        var canonical = evidenceDate.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture);
        if (!string.IsNullOrWhiteSpace(normalized) && !string.Equals(normalized, canonical, StringComparison.Ordinal))
        {
            AddDistinct(warnings,
                $"Chuẩn hóa lại {fieldName} từ bằng chứng nguyên văn '{evidence}' thành {canonical}.");
        }

        return canonical;
    }

    private static bool TryParseVisibleDate(string evidence, out DateTime date)
    {
        var match = Regex.Match(evidence.Trim(), @"(?<!\d)(\d{1,4}[-/.]\d{1,2}[-/.]\d{1,4})(?!\d)",
            RegexOptions.CultureInvariant);
        var textDateMatch = Regex.Match(evidence.Trim(),
            @"(?<![A-Za-z])((?:Mon|Tue|Wed|Thu|Fri|Sat|Sun)?\s*\d{1,2}\s+(?:Jan|Feb|Mar|Apr|May|Jun|Jul|Aug|Sep|Oct|Nov|Dec)\s+\d{4})(?!\d)",
            RegexOptions.CultureInvariant | RegexOptions.IgnoreCase);
        var token = match.Success
            ? match.Groups[1].Value
            : textDateMatch.Success
                ? textDateMatch.Groups[1].Value.Trim()
                : evidence.Trim();
        return DateTime.TryParseExact(token, VisibleDateFormats, CultureInfo.InvariantCulture,
            DateTimeStyles.None, out date);
    }

    private static void NormalizeInvoiceTime(ReceiptExtractionDto facts)
    {
        if (string.IsNullOrWhiteSpace(facts.InvoiceTime)) return;

        var token = facts.InvoiceTime.Trim().ToUpperInvariant();
        if (TimeOnly.TryParseExact(token, VisibleTimeFormats, CultureInfo.InvariantCulture,
                DateTimeStyles.None, out var time))
        {
            facts.InvoiceTime = time.ToString("HH:mm", CultureInfo.InvariantCulture);
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

    private static void AddSupportingIdentifierCollisionIssues(List<string> issues, ReceiptExtractionDto facts)
    {
        (string Name, string? Value)[] canonical =
        [
            ("invoiceNumber", facts.InvoiceNumber),
            ("receiptNumber", facts.ReceiptNumber),
            ("transactionReference", facts.TransactionReference)
        ];
        (string Name, string? Value)[] supporting =
        [
            ("taxAuthorityCode", facts.TaxAuthorityCode),
            ("invoiceSerial", facts.InvoiceSerial),
            ("documentNumber", facts.DocumentNumber),
            ("posNumber", facts.PosNumber)
        ];

        foreach (var left in canonical)
        foreach (var right in supporting)
            AddDuplicateIdentifierIssue(issues, left.Name, left.Value, right.Name, right.Value);
    }

    private static bool SameIdentifier(string? left, string? right) =>
        !string.IsNullOrWhiteSpace(left) && !string.IsNullOrWhiteSpace(right) &&
        string.Equals(left.Trim(), right.Trim(), StringComparison.OrdinalIgnoreCase);

    private static bool SameDate(string? left, string? right) =>
        !string.IsNullOrWhiteSpace(left) && !string.IsNullOrWhiteSpace(right) &&
        string.Equals(left.Trim(), right.Trim(), StringComparison.Ordinal);

    private static string NormalizeSearchText(string value)
    {
        var decomposed = value.Normalize(NormalizationForm.FormD);
        var builder = new StringBuilder(decomposed.Length);
        var previousWasSpace = true;
        foreach (var character in decomposed)
        {
            if (CharUnicodeInfo.GetUnicodeCategory(character) == UnicodeCategory.NonSpacingMark)
                continue;

            var lower = char.ToLowerInvariant(character);
            if (char.IsLetterOrDigit(lower))
            {
                builder.Append(lower);
                previousWasSpace = false;
            }
            else if (!previousWasSpace)
            {
                builder.Append(' ');
                previousWasSpace = true;
            }
        }

        return builder.ToString().Trim();
    }

    private static bool ContainsAny(string? source, params string[] terms) =>
        !string.IsNullOrWhiteSpace(source) && terms.Any(term =>
            source.Contains(term, StringComparison.OrdinalIgnoreCase));
}
