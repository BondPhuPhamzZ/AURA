using Microsoft.AspNetCore.Mvc;
using System.Threading.Tasks;
using AURA.Interfaces;
using AURA.Services;
using Microsoft.EntityFrameworkCore;

namespace AURA.Controllers
{
    public class ReviewerController : Controller
    {
        private readonly IReimbursementRepository _repo;
        private readonly IAuditLogger _audit;
        private readonly WorkflowOperationGate _operationGate;

        public ReviewerController(IReimbursementRepository repo, IAuditLogger audit,
            WorkflowOperationGate operationGate)
        {
            _repo = repo;
            _audit = audit;
            _operationGate = operationGate;
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> EscalateAction(string id, string decision)
        {
            if (string.IsNullOrWhiteSpace(id) || decision is not ("YES" or "NO" or "UNDO"))
                return BadRequest();
            if (!_operationGate.TryEnter("Quản lý đang cập nhật quyết định", out var lease))
                return WorkflowBusy();
            using var operation = lease!;

            var req = await _repo.GetRequestByIdAsync(id);
            if (req == null) return NotFound();

            if (decision != "UNDO" && (!PolicyDecisionEngine.IsEscalation(req.Status) || !req.IsForwardedToManager))
                return Conflict("Hồ sơ này không còn ở trạng thái chờ quyết định của quản lý.");

            if (decision == "UNDO")
            {
                if (!EscalationWorkflow.CanUndo(req.Status) || req.ManagerAnswer is null)
                    return Conflict("Chỉ có thể hoàn tác quyết định gần nhất của quản lý.");

                var logs = await _audit.GetByRequestIdAsync(req.Id);
                var priorAiAction = logs.FirstOrDefault(x => x.Action.StartsWith("AI_PROCESSED_ESCALATE_", StringComparison.Ordinal)
                    || x.Action.StartsWith("VERIFY_ESCALATE_", StringComparison.Ordinal));
                if (priorAiAction is null) return Conflict("Không tìm thấy trạng thái chuyển tiếp ban đầu để hoàn tác.");

                req.Status = priorAiAction.Action[(priorAiAction.Action.IndexOf("ESCALATE_", StringComparison.Ordinal))..];
                req.ManagerAnswer = null;
                req.ManagerDecisionAt = null;
                try
                {
                    await _repo.UpdateRequestWithAuditAsync(req, "MANAGER_UNDO",
                        $"Đã hoàn tác quyết định, khôi phục {req.Status}.");
                }
                catch (DbUpdateConcurrencyException)
                {
                    return Conflict(new { error = "Hồ sơ vừa được cập nhật ở thao tác khác. Vui lòng tải lại trang." });
                }
                TempData["Success"] = $"Đã hoàn tác quyết định cho hồ sơ {id}.";
                if (IsAjaxRequest())
                    return Json(new
                    {
                        message = TempData["Success"]?.ToString(),
                        status = req.Status,
                        redirectUrl = Url.Action("Index", "Home", new { tab = "audit" }) ?? "/?tab=audit"
                    });
                return RedirectToAction("Index", "Home", new { tab = "audit" });
            }

            var answer = decision == "YES";
            var originalStatus = req.Status;
            var outcome = EscalationWorkflow.Answer(originalStatus, answer);
            req.Status = outcome.Status;
            req.ManagerAnswer = answer;
            req.ManagerDecisionAt = DateTime.UtcNow;
            try
            {
                await _repo.UpdateRequestWithAuditAsync(req, outcome.AuditAction,
                    $"Question={req.ManagerQuestion}; Answer={(answer ? "ĐỒNG Ý DUYỆT" : "TỪ CHỐI DUYỆT")}; Outcome={outcome.Status}; {outcome.Message}");
            }
            catch (DbUpdateConcurrencyException)
            {
                return Conflict(new { error = "Hồ sơ vừa được cập nhật ở thao tác khác. Vui lòng tải lại trang." });
            }
            
            TempData["Success"] = outcome.Message;
            if (IsAjaxRequest())
                return Json(new
                {
                    message = outcome.Message,
                    status = outcome.Status,
                    redirectUrl = Url.Action("Index", "Home", new { tab = "reviewer" }) ?? "/?tab=reviewer"
                });
            return RedirectToAction("Index", "Home", new { tab = "reviewer" });
        }

        private bool IsAjaxRequest() =>
            string.Equals(Request.Headers["X-Requested-With"], "XMLHttpRequest", StringComparison.OrdinalIgnoreCase);

        private IActionResult WorkflowBusy() => Conflict(new
        {
            error = "Hệ thống đang xử lý một thao tác khác. Vui lòng chờ thao tác hiện tại hoàn tất rồi thử lại.",
            currentOperation = _operationGate.CurrentOperation
        });
    }
}

