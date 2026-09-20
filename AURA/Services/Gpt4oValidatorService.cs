using System;
using System.Diagnostics;
using System.Threading.Tasks;
using AURA.Interfaces;
using AURA.Models;

namespace AURA.Services
{
    public class Gpt4oValidatorService : IVisualValidator
    {
        public async Task<ReimbursementRequest> ValidateReceiptAsync(ReimbursementRequest request)
        {
            var sw = Stopwatch.StartNew();
            
            // TODO: Integrate actual GPT-4o API call here.
            // For now, simulate latency and return mock processing.
            await Task.Delay(150); // Simulate network latency

            // Temporary Mock Logic based on amounts for Sprint 1 Harness
            if (request.ClaimedAmount > 1000000)
            {
                request.Status = "ESCALATE - AUTHORITY";
                request.ManagerQuestion = $"Số tiền {request.ClaimedAmount}đ vượt quá hạn mức duyệt tự động. Sếp có muốn chuyển tiếp lên Giám đốc không? [CÓ/KHÔNG]";
                request.AiReasoning = "Vượt hạn mức 1,000,000đ theo chính sách.";
            }
            else if (request.ClaimedAmount == 200000) // Trigger for Fact Escalate (No Tax ID)
            {
                request.Status = "ESCALATE - FACT";
                request.ManagerQuestion = "Hóa đơn này là hóa đơn viết tay, không có Mã số thuế hợp lệ, và bị mờ. Sếp có chấp nhận thanh toán khoản này không? [CÓ/KHÔNG]";
                request.AiReasoning = "Không tìm thấy Mã Số Thuế hợp lệ.";
            }
            else if (request.ClaimedAmount == 850000) // Trigger for Policy Escalate (Alcohol)
            {
                request.Status = "ESCALATE - POLICY";
                request.ManagerQuestion = "Phát hiện 'Bia' vi phạm nội quy. Tuy nhiên đây là nhân sự Sales đi tiếp khách. Sếp có duyệt ngoại lệ khoản này không? [CÓ/KHÔNG]";
                request.AiReasoning = "Phát hiện mặt hàng cấm: Bia.";
            }
            else
            {
                request.Status = "AUTO_APPROVE";
                request.AiReasoning = "Thỏa mãn 100% chính sách. Đã tự động ghi vào Sổ cái SHA-256.";
            }

            sw.Stop();
            request.ProcessingLatencyMs = sw.ElapsedMilliseconds;

            return request;
        }
    }
}
