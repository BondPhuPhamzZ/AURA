using System.Text.Json;
using System.Text.Json.Serialization;
using AURA.Models;

namespace AURA.Services;

internal static class ReceiptExtractionContract
{
    internal const int CurrentEvidenceContractVersion = ReceiptExtractionDto.CurrentEvidenceContractVersion;

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true,
        UnmappedMemberHandling = JsonUnmappedMemberHandling.Skip,
        NumberHandling = JsonNumberHandling.AllowReadingFromString
    };

    internal sealed record Input(string Base64Image, string MimeType, string SystemPrompt);

    public static async Task<Input> LoadInputAsync(string physicalImagePath, string policyPath,
        string contentRootPath, CancellationToken cancellationToken)
    {
        var fullImagePath = Path.GetFullPath(physicalImagePath);
        if (!File.Exists(fullImagePath))
            throw new VisionExtractionException("IMAGE_NOT_FOUND", "Không tìm thấy ảnh hóa đơn cần phân tích.");

        var contentRoot = Path.GetFullPath(contentRootPath);
        var fullPolicyPath = Path.GetFullPath(Path.Combine(contentRoot, policyPath));
        var contentRootPrefix = contentRoot.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar)
            + Path.DirectorySeparatorChar;
        if (!fullPolicyPath.StartsWith(contentRootPrefix, StringComparison.OrdinalIgnoreCase) ||
            !File.Exists(fullPolicyPath))
            throw new VisionExtractionException("POLICY_NOT_FOUND",
                "Không tìm thấy tài liệu chính sách bắt buộc; không thể trích xuất dữ kiện an toàn.");

        var imageBytes = await File.ReadAllBytesAsync(fullImagePath, cancellationToken);
        var policy = await File.ReadAllTextAsync(fullPolicyPath, cancellationToken);
        var mimeType = Path.GetExtension(fullImagePath).ToLowerInvariant() switch
        {
            ".png" => "image/png",
            ".jpg" or ".jpeg" => "image/jpeg",
            ".webp" => "image/webp",
            _ => throw new InvalidOperationException("Định dạng ảnh không được hỗ trợ.")
        };

        var systemPrompt = $"""
            You are AURA's receipt-evidence extraction component. Follow the contract below exactly.
            Return only one JSON object that follows the supplied schema. Never add Markdown or prose.

            HIGH-PRIORITY NORMALIZATION:
            - Always return evidenceContractVersion={CurrentEvidenceContractVersion}. This version requires raw visual
              provenance for the policy date and the final payable amount; never omit or downgrade it.
            - VND has no decimal minor unit in this workflow. Vietnamese printed separators are thousands
              separators: `295.199 đ` -> 295199 and `3.000 đ` -> 3000 in JSON numeric fields.
            - A product-order screen with delivery status and SPX/GHN/GHTK/J&T tracking is ECOMMERCE,
              not RIDE_HAILING. RIDE_HAILING is only for passenger trips with a trip/booking/receipt ID.
            - Keep order IDs, trip/booking IDs and shipping tracking codes in their distinct fields.
            - Preserve the exact visible Vietnamese wording and diacritics in merchantName and every
              lineItems.description. Never silently replace a visually supported word with a more common
              policy word; in particular `Bìa hồ sơ` must not become `Bia hồ sơ`. If a policy-relevant word
              is genuinely unreadable, retain the best visible transcription, lower confidence and add a
              concise warning instead of confidently guessing a different product.
            - Read the document's overall status banner before local delivery milestones. Any visible
              `đã trả hàng`, `hoàn tiền`, `refunded`, `returned`, `đã hủy` or `cancelled` label takes
              precedence over `giao hàng thành công`/`completed`; set documentStatus and orderStatus to
              the corresponding RETURNED, REFUNDED or CANCELLED state and never mark that document COMPLETED.
            - On VAT invoices and receipts, actively read the seller name, tax ID, invoice/receipt number
              and the final payable row labelled `TỔNG THANH TOÁN`, `TỔNG CỘNG`, `THÀNH TIỀN`,
              `THANH TOÁN` or `SỐ TIỀN PHẢI THU`.
              When a clearly printed final total is visible, totalAmount must not be null and must not be
              confused with a line-item subtotal.
            - If the receipt prints a receipt-level `GIẢM GIÁ`, `CHIẾT KHẤU`, `VOUCHER`, `KHUYẾN MÃI`
              or equivalent reduction, put its absolute non-negative value in discountAmount. For example,
              a printed `-2.828` VND reduction becomes discountAmount=2828, not -2828. Do not infer a
              discount only from the difference between subtotal and totalAmount or from the claimed amount.
              Loyalty points/`điểm tích lũy`, point balances, voucher codes or percentages, cash tendered,
              change returned, quantities and terminal/customer numbers are not discountAmount. If subtotal,
              line-item total and totalAmount are already the same, do not turn an unrelated visible number
              into a discount unless the image also shows a distinct before-discount base that reconciles it.
              When no receipt-level reduction is visibly printed, set discountAmount to null rather than 0.
              totalAmount remains the final amount actually payable after the visible reduction. When
              subtotal + tax - discountAmount reconciles to totalAmount, do not report that expected
              difference as a warning or suspicious signal.
            - Keep formal invoice numbers, receipt/bill numbers, and transaction references separate.
              `Invoice No`/`Số hóa đơn` belongs in invoiceNumber; `Receipt No`/`Bill No`/`Số biên nhận`
              belongs in receiptNumber; a per-purchase `Check`, `Transaction No`, `Trace`, `RRN` or
              `Mã giao dịch` belongs in transactionReference. Shop/store ID, POS/register ID, terminal ID,
              merchant ID, pager number, tax ID, invoice serial or form number are not transaction references.
              `Mã CQT`/`Tax authority code` belongs only in taxAuthorityCode; `Ký hiệu`/`Serial No`
              belongs only in invoiceSerial; `Số chứng từ`/`Document No` belongs only in documentNumber;
              `POS No`/register number belongs only in posNumber. A number explicitly labelled `PTT` on a
              `PHIẾU TÍNH TIỀN` is the printed receipt number and belongs in receiptNumber, never both
              receiptNumber and transactionReference.
            - Copy the exact visible date text into invoiceDateEvidence or transactionDateEvidence alongside
              the corresponding normalized date. For Vietnamese receipts, numeric dates are day-first:
              `04-10-26` and `04/10/2026` mean 2026-10-04, not 2026-04-10. Do not invent a raw evidence string.
            - If the final payable row itself is blurred, covered or cropped, set totalAmount to null and
              report that visual defect in warnings or suspiciousSignals. Never infer the final total from
              a line-item amount, subtotal or the user's claimed amount, even when the numbers look equal.
            - Set totalAmountSource to PRINTED_FINAL_TOTAL only when both the final-payable label and its
              numeric value are directly visible. Copy that one row verbatim into totalAmountEvidence.
              If the label is visible but its value is torn/cropped, use NOT_VISIBLE, set totalAmount and
              totalAmountEvidence to null, add totalAmount to missingFields and record the defect. Use
              AMBIGUOUS when multiple final candidates cannot be resolved and INFERRED only to disclose a
              non-authoritative arithmetic guess; AMBIGUOUS and INFERRED must also keep totalAmount null.
              `Subtotal`, `Tạm tính`, cash tendered/`Tiền mặt`, change/`Tiền thối lại`, and included VAT are
              never final-payable evidence by themselves.

            {policy}
            """;

        return new Input(Convert.ToBase64String(imageBytes), mimeType, systemPrompt);
    }

    public static ReceiptExtractionDto ParseFacts(string content)
    {
        var json = ExtractJsonObject(content);
        var facts = JsonSerializer.Deserialize<ReceiptExtractionDto>(json, JsonOptions)
            ?? throw new JsonException("Empty extraction object.");

        if (facts.EvidenceContractVersion != CurrentEvidenceContractVersion)
            throw new JsonException(
                $"Unsupported or missing evidenceContractVersion. Expected {CurrentEvidenceContractVersion}.");

        facts.LineItems ??= [];
        facts.MissingFields ??= [];
        facts.Warnings ??= [];
        facts.SuspiciousSignals ??= [];
        facts.ValidationIssues ??= [];
        facts.SemanticRepairIssues ??= [];
        facts.Confidence = Math.Clamp(facts.Confidence, 0, 1);
        return ReceiptSemanticValidator.NormalizeCanonicalFields(facts);
    }

    public static object BuildResponseSchema() => new
    {
        type = "object",
        additionalProperties = false,
        required = new[] { "evidenceContractVersion", "documentType", "documentStatus", "merchantName", "taxId", "merchantId", "terminalId",
            "platformName", "orderId", "bookingId", "shippingTrackingCode", "shippingProvider", "orderStatus",
            "invoiceNumber", "receiptNumber", "transactionReference", "taxAuthorityCode", "invoiceSerial",
            "documentNumber", "posNumber", "invoiceDate", "invoiceDateEvidence", "transactionDate",
            "transactionDateEvidence",
            "completionDate", "invoiceTime", "currency",
            "subtotal", "discountAmount", "tax", "totalAmount", "totalAmountSource", "totalAmountEvidence",
            "lineItems", "missingFields", "warnings", "suspiciousSignals",
            "confidence" },
        properties = new Dictionary<string, object>
        {
            ["evidenceContractVersion"] = new { type = "integer", @enum = new[] { CurrentEvidenceContractVersion } },
            ["documentType"] = EnumOrNull("VAT_INVOICE", "RETAIL_RECEIPT", "RESTAURANT_BILL",
                "RIDE_HAILING", "ECOMMERCE", "OTHER"),
            ["documentStatus"] = EnumOrNull("ISSUED", "COMPLETED", "DRAFT", "CANCELLED",
                "REFUNDED", "RETURNED", "UNKNOWN"),
            ["merchantName"] = NullableString(), ["taxId"] = NullableString(),
            ["merchantId"] = NullableString(), ["terminalId"] = NullableString(),
            ["platformName"] = NullableString(), ["orderId"] = NullableString(), ["bookingId"] = NullableString(),
            ["shippingTrackingCode"] = NullableString(), ["shippingProvider"] = NullableString(),
            ["orderStatus"] = NullableString(), ["invoiceNumber"] = NullableString(),
            ["receiptNumber"] = NullableString(), ["transactionReference"] = NullableString(),
            ["taxAuthorityCode"] = NullableString(), ["invoiceSerial"] = NullableString(),
            ["documentNumber"] = NullableString(), ["posNumber"] = NullableString(),
            ["invoiceDate"] = NullableString(), ["invoiceDateEvidence"] = NullableString(),
            ["transactionDate"] = NullableString(), ["transactionDateEvidence"] = NullableString(),
            ["completionDate"] = NullableString(), ["invoiceTime"] = NullableString(),
            ["currency"] = NullableString(), ["subtotal"] = NullableMoneyNumber("subtotal"),
            ["discountAmount"] = NullableMoneyNumber("receipt-level discount as a non-negative absolute value"),
            ["tax"] = NullableMoneyNumber("tax"),
            ["totalAmount"] = NullableMoneyNumber("final amount actually paid"),
            ["totalAmountSource"] = EnumOrNull("PRINTED_FINAL_TOTAL", "NOT_VISIBLE", "AMBIGUOUS", "INFERRED"),
            ["totalAmountEvidence"] = NullableString(),
            ["lineItems"] = new
            {
                type = "array",
                items = new
                {
                    type = "object",
                    additionalProperties = false,
                    required = new[] { "description", "quantity", "unitPrice", "amount" },
                    properties = new Dictionary<string, object>
                    {
                        ["description"] = new { type = "string" }, ["quantity"] = NullableNumber(),
                        ["unitPrice"] = NullableMoneyNumber("line unit price"),
                        ["amount"] = NullableMoneyNumber("line amount")
                    }
                }
            },
            ["missingFields"] = StringArray(), ["warnings"] = StringArray(),
            ["suspiciousSignals"] = StringArray(),
            ["confidence"] = new { type = "number", minimum = 0, maximum = 1 }
        }
    };

    private static string TrimCodeFence(string content)
    {
        var trimmed = content.Trim();
        if (!trimmed.StartsWith("```", StringComparison.Ordinal) ||
            !trimmed.EndsWith("```", StringComparison.Ordinal))
            return trimmed;

        var firstNewLine = trimmed.IndexOf('\n');
        return firstNewLine < 0 ? trimmed : trimmed[(firstNewLine + 1)..^3].Trim();
    }

    private static string ExtractJsonObject(string content)
    {
        var trimmed = TrimCodeFence(content);
        var start = trimmed.IndexOf('{');
        var end = trimmed.LastIndexOf('}');
        return start >= 0 && end >= start ? trimmed[start..(end + 1)] : trimmed;
    }

    private static object NullableString() => new { type = new[] { "string", "null" } };
    private static object NullableNumber() => new { type = new[] { "number", "null" } };
    private static object NullableMoneyNumber(string meaning) => new
    {
        type = new[] { "number", "null" },
        description = $"Normalized numeric {meaning}. For VND remove thousands separators: 295.199 đ becomes 295199, never 295.199."
    };
    private static object EnumOrNull(params string[] values) => new
    {
        type = new[] { "string", "null" },
        @enum = values.Cast<object?>().Append(null).ToArray()
    };
    private static object StringArray() => new { type = "array", items = new { type = "string" } };
}
