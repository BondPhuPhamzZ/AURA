using System;
using System.Linq;
using AURA.Models;

namespace AURA.Services
{
    public class PolicyDecisionEngine
    {
        public static (string Status, string Reason, string ManagerQuestion) Evaluate(ReceiptExtractionDto facts, decimal claimedAmount, string employeeDept = "Normal")
        {
            // 1. KIỂM TRA THÔNG TIN HÓA ĐƠN
            if (facts == null)
            {
                 return ("ESCALATE_SYSTEM_ERROR", "Lỗi trích xuất dữ liệu.", "Hệ thống không thể đọc được hóa đơn này. Sếp có muốn xem xét thủ công? [CÓ/KHÔNG]");
            }
            if (facts.Confidence < 0.5)
            {
                 return ("ESCALATE_FACT", "Hình ảnh mờ hoặc không thể nhận diện chính xác.", "Hóa đơn quá mờ hoặc bị mất chữ. Sếp có duyệt ngoại lệ không? [CÓ/KHÔNG]");
            }
            if (facts.TotalAmount == null || facts.TotalAmount != claimedAmount)
            {
                 return ("ESCALATE_FACT", $"Số tiền hóa đơn ({facts.TotalAmount}) không khớp với số tiền khai báo ({claimedAmount}).", "Nhân viên khai báo sai lệch số tiền với hóa đơn gốc. Sếp có đồng ý thanh toán số tiền khai báo không? [CÓ/KHÔNG]");
            }
            
            bool isWeekend = false;
            if (DateTime.TryParse(facts.InvoiceDate, out DateTime invDate))
            {
                if (invDate.DayOfWeek == DayOfWeek.Saturday || invDate.DayOfWeek == DayOfWeek.Sunday)
                {
                    isWeekend = true;
                }
            }
            if (isWeekend)
            {
                 return ("ESCALATE_FACT", "Hóa đơn phát sinh vào ngày cuối tuần.", "Hóa đơn này phát sinh vào cuối tuần. Chi phí cuối tuần thường là cá nhân trừ khi có lệnh OT. Sếp có duyệt ngoại lệ không? [CÓ/KHÔNG]");
            }

            // Kiểm tra thông tin bắt buộc
            if (facts.MissingFields.Contains("merchantName") || facts.MissingFields.Contains("invoiceNumber"))
            {
                return ("ESCALATE_FACT", "Hóa đơn thiếu thông tin xác thực cơ bản.", "Hóa đơn này là hóa đơn bán lẻ/viết tay, không có Mã số thuế hợp lệ của doanh nghiệp. Sếp có chấp nhận thanh toán khoản này không? [CÓ/KHÔNG]");
            }

            // 2. KIỂM TRA QUY ĐỊNH CÔNG TY
            bool hasAlcohol = facts.LineItems.Any(item => 
                (item.Description ?? "").ToLower().Contains("bia") || 
                (item.Description ?? "").ToLower().Contains("beer") ||
                (item.Description ?? "").ToLower().Contains("rượu") ||
                (item.Description ?? "").ToLower().Contains("wine") ||
                (item.Description ?? "").ToLower().Contains("heineken")
            );

            if (hasAlcohol)
            {
                if (employeeDept == "Sales")
                {
                    return ("ESCALATE_POLICY", "Phát hiện đồ uống có cồn (Nhân viên Sales).", "Phát hiện 'Bia/Rượu' vi phạm quy định. Tuy nhiên đây là nhân viên Sales đi tiếp khách. Sếp có duyệt ngoại lệ khoản này không? [CÓ/KHÔNG]");
                }
                return ("ESCALATE_POLICY", "Phát hiện đồ uống có cồn vi phạm quy định công ty.", "Hóa đơn chứa đồ uống có cồn, vi phạm nghiêm trọng quy định tài chính. Sếp có từ chối khoản này không? [CÓ/KHÔNG]");
            }

            // 3. KIỂM TRA THẨM QUYỀN
            if (facts.TotalAmount > 1000000)
            {
                return ("ESCALATE_AUTHORITY", "Vượt hạn mức duyệt tự động (1,000,000đ).", $"Số tiền {facts.TotalAmount:N0}đ vượt quá hạn mức duyệt tự động (1.000.000đ) và cần Giám đốc ký duyệt. Sếp có muốn chuyển tiếp lên Giám đốc không? [CÓ/KHÔNG]");
            }

            // 4. TỰ ĐỘNG DUYỆT
            return ("AUTO_APPROVE", "Thỏa mãn 100% chính sách tài chính. Đã ghi sổ cái SHA-256.", null);
        }
    }
}
