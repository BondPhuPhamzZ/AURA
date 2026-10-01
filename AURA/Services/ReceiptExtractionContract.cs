using System.Text.Json;
using System.Text.Json.Serialization;
using AURA.Models;

namespace AURA.Services;

internal static class ReceiptExtractionContract
{
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
            - VND has no decimal minor unit in this workflow. Vietnamese printed separators are thousands
              separators: `295.199 đ` -> 295199 and `3.000 đ` -> 3000 in JSON numeric fields.
            - A product-order screen with delivery status and SPX/GHN/GHTK/J&T tracking is ECOMMERCE,
              not RIDE_HAILING. RIDE_HAILING is only for passenger trips with a trip/booking/receipt ID.
            - Keep order IDs, trip/booking IDs and shipping tracking codes in their distinct fields.
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
            - If the final payable row itself is blurred, covered or cropped, set totalAmount to null and
              report that visual defect in warnings or suspiciousSignals. Never infer the final total from
              a line-item amount, subtotal or the user's claimed amount, even when the numbers look equal.

            {policy}
            """;

        return new Input(Convert.ToBase64String(imageBytes), mimeType, systemPrompt);
    }

    public static ReceiptExtractionDto ParseFacts(string content)
    {
        var json = ExtractJsonObject(content);
        var facts = JsonSerializer.Deserialize<ReceiptExtractionDto>(json, JsonOptions)
            ?? throw new JsonException("Empty extraction object.");

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
        required = new[] { "documentType", "documentStatus", "merchantName", "taxId", "merchantId", "terminalId",
            "platformName", "orderId", "bookingId", "shippingTrackingCode", "shippingProvider", "orderStatus",
            "invoiceNumber", "receiptNumber", "transactionReference", "invoiceDate", "transactionDate",
            "completionDate", "invoiceTime", "currency",
            "subtotal", "discountAmount", "tax", "totalAmount", "lineItems", "missingFields", "warnings", "suspiciousSignals",
            "confidence" },
        properties = new Dictionary<string, object>
        {
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
            ["invoiceDate"] = NullableString(), ["transactionDate"] = NullableString(),
            ["completionDate"] = NullableString(), ["invoiceTime"] = NullableString(),
            ["currency"] = NullableString(), ["subtotal"] = NullableMoneyNumber("subtotal"),
            ["discountAmount"] = NullableMoneyNumber("receipt-level discount as a non-negative absolute value"),
            ["tax"] = NullableMoneyNumber("tax"),
            ["totalAmount"] = NullableMoneyNumber("final amount actually paid"),
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
