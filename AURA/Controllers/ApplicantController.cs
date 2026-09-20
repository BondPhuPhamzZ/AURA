using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Http;
using System.IO;
using System.Threading.Tasks;
using AURA.Interfaces;
using AURA.Models;
using AURA.ViewModels;
using System.Linq;

namespace AURA.Controllers
{
    public class ApplicantController : Controller
    {
        private readonly IReimbursementRepository _repo;
        private readonly IVisualValidator _validator;
        private readonly IAuditLogger _audit;

        public ApplicantController(IReimbursementRepository repo, IVisualValidator validator, IAuditLogger audit)
        {
            _repo = repo;
            _validator = validator;
            _audit = audit;
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> UploadReceipt(IFormFile receiptFile)
        {
            if (receiptFile == null || receiptFile.Length == 0)
            {
                TempData["Error"] = "Vui lòng chọn file hóa đơn hợp lệ.";
                return RedirectToAction("Index", "Home");
            }

            var allowedExtensions = new[] { ".jpg", ".jpeg", ".png", ".pdf" };
            var extension = Path.GetExtension(receiptFile.FileName).ToLowerInvariant();

            if (!allowedExtensions.Contains(extension))
            {
                TempData["Error"] = "Chỉ chấp nhận file định dạng JPG, PNG hoặc PDF.";
                return RedirectToAction("Index", "Home");
            }

            // Dummy processing for the sprint 1 mock
            var req = new ReimbursementRequest
            {
                EmployeeId = "EMP001",
                EmployeeName = "Nhân viên Upload",
                ClaimedAmount = 500000,
                ImageUrl = $"/test_data/images/{receiptFile.FileName}"
            };

            // Call AI
            var validatedReq = await _validator.ValidateReceiptAsync(req);
            
            // Save to DB
            await _repo.AddRequestAsync(validatedReq);

            // Audit
            await _audit.LogActionAsync(validatedReq.Id, "UPLOAD_AND_SCAN", $"User uploaded receipt. AI Status: {validatedReq.Status}");

            TempData["Success"] = "Đã phân tích hóa đơn và lưu thành công!";
            return RedirectToAction("Index", "Home");
        }
    }
}

