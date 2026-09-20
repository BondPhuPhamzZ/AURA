using Microsoft.AspNetCore.Mvc;
using System.Threading.Tasks;
using AURA.Interfaces;
using AURA.Services;

namespace AURA.Controllers
{
    public class ReviewerController : Controller
    {
        private readonly IReimbursementRepository _repo;
        private readonly IAuditLogger _audit;

        public ReviewerController(IReimbursementRepository repo, IAuditLogger audit)
        {
            _repo = repo;
            _audit = audit;
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> EscalateAction(string id, string decision)
        {
            if (string.IsNullOrWhiteSpace(id) || decision is not ("APPROVE" or "REJECT" or "UNDO"))
                return BadRequest();

            var req = await _repo.GetRequestByIdAsync(id);
            if (req == null) return NotFound();

            if (decision != "UNDO" && !PolicyDecisionEngine.IsEscalation(req.Status))
                return Conflict("Hồ sơ này không còn ở trạng thái chờ quyết định của quản lý.");

            if (decision == "UNDO")
            {
                if (req.Status is not ("APPROVED_BY_MANAGER" or "REJECTED_BY_MANAGER"))
                    return Conflict("Chỉ có thể hoàn tác quyết định gần nhất của quản lý.");

                var logs = await _audit.GetByRequestIdAsync(req.Id);
                var priorAiAction = logs.FirstOrDefault(x => x.Action.StartsWith("AI_PROCESSED_ESCALATE_", StringComparison.Ordinal)
                    || x.Action.StartsWith("VERIFY_ESCALATE_", StringComparison.Ordinal));
                if (priorAiAction is null) return Conflict("Không tìm thấy trạng thái chuyển tiếp ban đầu để hoàn tác.");

                req.Status = priorAiAction.Action[(priorAiAction.Action.IndexOf("ESCALATE_", StringComparison.Ordinal))..];
                await _repo.UpdateRequestAsync(req);
                await _audit.LogActionAsync(req.Id, "MANAGER_UNDO", $"Đã hoàn tác quyết định, khôi phục {req.Status}.");
                TempData["Success"] = $"Đã hoàn tác quyết định cho hồ sơ {id}.";
                return RedirectToAction("Index", "Home");
            }

            if (decision == "APPROVE")
            {
                req.Status = "APPROVED_BY_MANAGER";
                await _audit.LogActionAsync(req.Id, "MANAGER_APPROVE", "Sếp đã duyệt ngoại lệ thành công.");
            }
            else if (decision == "REJECT")
            {
                req.Status = "REJECTED_BY_MANAGER";
                await _audit.LogActionAsync(req.Id, "MANAGER_REJECT", "Sếp đã từ chối hóa đơn này.");
            }

            await _repo.UpdateRequestAsync(req);
            
            TempData["Success"] = $"Đã xử lý hồ sơ {id} thành công!";
            return RedirectToAction("Index", "Home");
        }
    }
}

