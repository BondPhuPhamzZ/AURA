using System.Globalization;
using AURA.Models;

namespace AURA.Services;

public static class PolicyDecisionEngine
{
    public const decimal AutoApprovalLimitVnd = 1_000_000m;
    private const double MinimumConfidence = 0.70;

    private static readonly string[] ProhibitedTerms =
    [
        "alcohol", "beer", "bia", "rượu", "ruou", "wine", "whisky", "vodka", "heineken", "tiger",
        "thuốc lá", "thuoc la", "tobacco", "cigarette", "karaoke", "cinema", "massage", "entertainment",
        "personal item", "đồ cá nhân", "do ca nhan"
    ];

    public static (string Status, string Reason, string ManagerQuestion) Evaluate(
        ReceiptExtractionDto? facts,
        decimal claimedAmount,
        bool isDuplicate = false,
        DateTime? utcNow = null)
    {
        if (facts is null)
            return Fact("Không nhận được dữ liệu trích xuất.",
                "Hệ thống không đọc được hóa đơn. Quản lý có đồng ý tiếp nhận hồ sơ để kiểm tra thủ công không? [CÓ/KHÔNG]");

        var factProblems = new List<string>();
        if (facts.Confidence < MinimumConfidence)
            factProblems.Add($"độ tin cậy trích xuất chỉ {facts.Confidence:P0}");
        if (ContainsAny(facts.DocumentStatus, "draft", "nháp", "unissued", "chưa phát hành", "cancelled", "canceled", "đã hủy", "refunded", "hoàn tiền", "returned", "trả hàng"))
            factProblems.Add($"chứng từ ở trạng thái {facts.DocumentStatus}, chưa phải hóa đơn đã phát hành hợp lệ");
        if (isDuplicate)
            factProblems.Add("ảnh hóa đơn trùng với hồ sơ đã lưu");
        if (facts.TotalAmount is null or <= 0)
            factProblems.Add("không xác định được tổng tiền hợp lệ");
        if (claimedAmount <= 0)
            factProblems.Add("số tiền đề nghị hoàn ứng không hợp lệ");
        else if (facts.TotalAmount.HasValue && decimal.Round(facts.TotalAmount.Value, 0) != decimal.Round(claimedAmount, 0))
            factProblems.Add($"tổng tiền {facts.TotalAmount.Value:N0} VND không khớp số tiền khai báo {claimedAmount:N0} VND");

        if (string.IsNullOrWhiteSpace(facts.MerchantName))
            factProblems.Add("thiếu tên đơn vị bán hàng");

        var isEcommerce = ContainsAny(facts.DocumentType, "ecommerce", "e-commerce", "online order");
        var isRideHailing = ContainsAny(facts.DocumentType, "ride_hailing", "ride-hailing", "ride hailing");
        var isVatInvoice = ContainsAny(facts.DocumentType, "vat_invoice", "vat invoice", "hóa đơn giá trị gia tăng");
        var isDigital = isEcommerce || isRideHailing ||
            ContainsAny(facts.DocumentType, "digital", "electronic", "e-invoice");

        if (!isDigital && string.IsNullOrWhiteSpace(facts.InvoiceNumber))
            factProblems.Add("thiếu số hóa đơn/biên nhận");

        if (isEcommerce)
        {
            var hasTraceableIdentifier = !string.IsNullOrWhiteSpace(facts.InvoiceNumber) ||
                !string.IsNullOrWhiteSpace(facts.OrderId) ||
                !string.IsNullOrWhiteSpace(facts.BookingId) ||
                !string.IsNullOrWhiteSpace(facts.ShippingTrackingCode);
            if (!hasTraceableIdentifier)
                factProblems.Add("thiếu mã đơn hàng, mã đặt chỗ, mã vận chuyển hoặc số biên nhận để đối chiếu");

            if (string.IsNullOrWhiteSpace(facts.OrderStatus))
            {
                factProblems.Add("thiếu trạng thái hoàn tất/thanh toán của đơn hàng trực tuyến");
            }
            else if (ContainsAny(facts.OrderStatus, "cancelled", "canceled", "đã hủy", "refunded", "hoàn tiền", "returned", "trả hàng"))
            {
                factProblems.Add($"đơn hàng trực tuyến có trạng thái không đủ điều kiện ({facts.OrderStatus})");
            }
            else if (!ContainsAny(facts.OrderStatus, "completed", "delivered", "paid", "hoàn thành", "giao hàng thành công", "đã thanh toán"))
            {
                factProblems.Add($"đơn hàng trực tuyến chưa có trạng thái hoàn tất đáng tin cậy ({facts.OrderStatus})");
            }
        }
        else if (isRideHailing && string.IsNullOrWhiteSpace(facts.BookingId) &&
                 string.IsNullOrWhiteSpace(facts.InvoiceNumber))
        {
            factProblems.Add("thiếu mã chuyến đi/đặt chỗ hoặc số biên nhận để đối chiếu");
        }

        if (isVatInvoice && string.IsNullOrWhiteSpace(facts.TaxId))
            factProblems.Add("hóa đơn GTGT thiếu mã số thuế của đơn vị bán hàng");

        if (!string.Equals(facts.Currency, "VND", StringComparison.OrdinalIgnoreCase))
            factProblems.Add(string.IsNullOrWhiteSpace(facts.Currency)
                ? "không xác định được tiền tệ"
                : $"tiền tệ {facts.Currency} chưa có tỷ giá/quy tắc quy đổi");

        var evidenceDate = isDigital && !string.IsNullOrWhiteSpace(facts.TransactionDate)
            ? facts.TransactionDate
            : facts.InvoiceDate;
        var evidenceDateLabel = isDigital ? "ngày giao dịch/thanh toán" : "ngày hóa đơn";
        if (!TryParseInvoiceDate(evidenceDate, out var invoiceDate))
            factProblems.Add($"thiếu hoặc không đọc được {evidenceDateLabel} theo định dạng YYYY-MM-DD");
        else
        {
            var today = (utcNow ?? DateTime.UtcNow).Date;
            if (invoiceDate.Date > today.AddDays(1)) factProblems.Add("ngày hóa đơn nằm trong tương lai");
            if (invoiceDate.Date < today.AddDays(-90)) factProblems.Add("hóa đơn đã quá thời hạn hoàn ứng 90 ngày");
            if (invoiceDate.DayOfWeek is DayOfWeek.Saturday or DayOfWeek.Sunday)
                factProblems.Add("hóa đơn phát sinh vào cuối tuần");
        }

        if (!string.IsNullOrWhiteSpace(facts.InvoiceTime))
        {
            if (!TimeOnly.TryParseExact(facts.InvoiceTime, "HH:mm", CultureInfo.InvariantCulture, DateTimeStyles.None, out var time))
                factProblems.Add("không đọc chắc chắn được giờ trên hóa đơn");
            else if (time < new TimeOnly(6, 0) || time > new TimeOnly(22, 0))
                factProblems.Add($"thời điểm {time:HH:mm} nằm ngoài khung 06:00-22:00");
        }

        var criticalWarnings = facts.Warnings
            .Concat(facts.SuspiciousSignals)
            .Where(x => ContainsAny(x, "blurry", "mờ", "cropped", "cắt", "unreadable", "tamper", "sửa", "partial", "thiếu trang", "prompt injection", "impossible arithmetic", "sai số học"))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();
        if (criticalWarnings.Count > 0)
            factProblems.Add("ảnh có dấu hiệu cần xác minh: " + string.Join(", ", criticalWarnings));

        if (factProblems.Count > 0)
        {
            var summary = string.Join("; ", factProblems);
            return Fact("Không đủ dữ kiện đáng tin cậy: " + summary + ".",
                $"Hồ sơ có vấn đề dữ kiện: {summary}. Quản lý có đồng ý tiếp nhận hồ sơ để kiểm tra thủ công không? [CÓ/KHÔNG]");
        }

        var prohibitedItems = facts.LineItems
            .Select(x => x.Description)
            .Where(description => ProhibitedTerms.Any(term => ContainsAny(description, term)))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();
        if (prohibitedItems.Count > 0)
        {
            var items = string.Join(", ", prohibitedItems);
            return ("ESCALATE_POLICY",
                $"Phát hiện hạng mục ngoài chính sách: {items}.",
                $"Hóa đơn có hạng mục ngoài chính sách ({items}). Quản lý có đồng ý duyệt ngoại lệ khoản chi này không? [CÓ/KHÔNG]");
        }

        if (facts.TotalAmount!.Value > AutoApprovalLimitVnd)
        {
            return ("ESCALATE_AUTHORITY",
                $"Tổng tiền {facts.TotalAmount.Value:N0} VND vượt hạn mức tự động {AutoApprovalLimitVnd:N0} VND.",
                $"Khoản chi {facts.TotalAmount.Value:N0} VND vượt hạn mức tự động {AutoApprovalLimitVnd:N0} VND. Quản lý có đồng ý chuyển hồ sơ lên cấp có thẩm quyền không? [CÓ/KHÔNG]");
        }

        return ("AUTO_APPROVE",
            $"Hóa đơn đủ dữ kiện, đúng số tiền, trong hạn 90 ngày, không có hạng mục cấm và không vượt {AutoApprovalLimitVnd:N0} VND.",
            string.Empty);
    }

    public static bool IsEscalation(string? status) =>
        status?.StartsWith("ESCALATE_", StringComparison.Ordinal) == true;

    private static (string, string, string) Fact(string reason, string question) =>
        ("ESCALATE_FACT", reason, question);

    private static bool ContainsAny(string? source, params string[] terms) =>
        !string.IsNullOrWhiteSpace(source) && terms.Any(term => source.Contains(term, StringComparison.OrdinalIgnoreCase));

    private static bool TryParseInvoiceDate(string? value, out DateTime date) =>
        DateTime.TryParseExact(value, "yyyy-MM-dd", CultureInfo.InvariantCulture, DateTimeStyles.None, out date);
}
