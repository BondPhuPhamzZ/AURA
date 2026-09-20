using System;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using AURA.Interfaces;
using AURA.Models;
using AURA.Services;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;

namespace AURA.Controllers
{
    public class ApplicantController : Controller
    {
        private readonly IWebHostEnvironment _env;
        private readonly IReimbursementRepository _repo;
        private readonly IAuditLogger _audit;
        private readonly IVisionExtractor _vision;
        private readonly ILogger<ApplicantController> _logger;

        public ApplicantController(IWebHostEnvironment env, IReimbursementRepository repo, IAuditLogger audit, IVisionExtractor vision, ILogger<ApplicantController> logger)
        {
            _env = env;
            _repo = repo;
            _audit = audit;
            _vision = vision;
            _logger = logger;
        }

        public IActionResult Index()
        {
            return View();
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> UploadReceipt(IFormFile receiptFile)
        {
            if (receiptFile == null || receiptFile.Length == 0)
            {
                return Json(new { error = "Vui lòng chọn file hóa đơn hợp lệ." });
            }

            // 1. Kiểm tra file
            var allowedExtensions = new[] { ".jpg", ".jpeg", ".png" };
            var extension = Path.GetExtension(receiptFile.FileName).ToLowerInvariant();
            if (!allowedExtensions.Contains(extension))
            {
                return Json(new { error = "Chỉ chấp nhận file ảnh (JPG, PNG)." });
            }

            if (receiptFile.Length > 5 * 1024 * 1024)
            {
                return Json(new { error = "Kích thước file không được vượt quá 5MB." });
            }

            // 2. Lưu ảnh
            var uploadsFolder = Path.Combine(_env.WebRootPath, "uploads");
            if (!Directory.Exists(uploadsFolder)) Directory.CreateDirectory(uploadsFolder);
            
            var uniqueFileName = Guid.NewGuid().ToString() + extension;
            var filePath = Path.Combine(uploadsFolder, uniqueFileName);

            using (var stream = new FileStream(filePath, FileMode.Create))
            {
                await receiptFile.CopyToAsync(stream);
            }

            var request = new ReimbursementRequest
            {
                Id = Guid.NewGuid().ToString(),
                
                
                ClaimedAmount = 0m,
                ImageUrl = "/uploads/" + uniqueFileName,
                CreatedAt = DateTime.UtcNow
            };

            // 3. AI Trích xuất & Xét duyệt
            var sw = Stopwatch.StartNew();
            try 
            {
                // - Đọc dữ liệu hóa đơn
                var facts = await _vision.ExtractFactsAsync(request.ImageUrl);
                
                // - Áp dụng luật công ty
                var decision = PolicyDecisionEngine.Evaluate(facts, request.ClaimedAmount);
                
                request.Status = decision.Status;
                request.AiReasoning = decision.Reason;
                request.ManagerQuestion = decision.ManagerQuestion;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Vision extraction failed");
                request.Status = "ESCALATE_SYSTEM_ERROR";
                request.AiReasoning = $"Lỗi trích xuất hệ thống: {ex.Message}";
                request.ManagerQuestion = "Hệ thống AI không thể xử lý ảnh do lỗi kỹ thuật. Sếp có muốn kiểm tra thủ công?";
            }
            sw.Stop();
            request.ProcessingLatencyMs = sw.ElapsedMilliseconds;

            // 4. Lưu DB & Log
            await _repo.AddRequestAsync(request);
            await _audit.LogActionAsync(request.Id, $"AI_PROCESSED_{request.Status}", $"Reasoning: {request.AiReasoning} | Latency: {sw.ElapsedMilliseconds}ms");

            return RedirectToAction("Index", "Home");
        }
    }
}





