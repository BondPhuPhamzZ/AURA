using Microsoft.AspNetCore.Mvc;
using System.Threading.Tasks;
using AURA.Interfaces;
using AURA.Options;
using AURA.Services;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace AURA.Controllers
{
    public class ReviewerController : Controller
    {
        private readonly IReimbursementRepository _repo;
        private readonly IAuditLogger _audit;
        private readonly bool _retentionEnabled;

        public ReviewerController(IReimbursementRepository repo, IAuditLogger audit,
            IOptions<ReceiptRetentionOptions>? retentionOptions = null)
        {
            _repo = repo;
            _audit = audit;
            _retentionEnabled = retentionOptions?.Value.Enabled == true;
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> EscalateAction(string id, string decision)
        {
            if (string.IsNullOrWhiteSpace(id) || decision is not ("YES" or "NO" or "UNDO"))
                return BadRequest();
            var req = await _repo.GetRequestByIdAsync(id);
            if (req == null) return NotFound();

            var answer = decision == "YES";
            if (decision != "UNDO")
            {
                if (EscalationWorkflow.IsSameCompletedDecision(req.Status, req.ManagerAnswer,
                    req.ManagerDecisionAt, answer))
                    return ManagerDecisionAlreadyApplied(req, answer);

                if (!PolicyDecisionEngine.IsEscalation(req.Status) || !req.IsForwardedToManager ||
                    req.ManagerAnswer is not null || req.ManagerDecisionAt is not null)
                    return Conflict("Hồ sơ này không còn ở trạng thái chờ quyết định của quản lý.");
            }

            if (decision == "UNDO")
            {
                if (_retentionEnabled &&
                    ReceiptRetentionPolicy.IsExpired(req.RetentionExpiresAt, DateTime.UtcNow))
                    return Conflict("Đã hết thời hạn lưu; không thể hoàn tác quyết định này.");

                var logs = await _audit.GetByRequestIdAsync(req.Id);
                if (!EscalationWorkflow.CanUndo(req.Status) || req.ManagerAnswer is null)
                {
                    if (EscalationWorkflow.IsAlreadyUndone(req.Status, req.ManagerAnswer,
                        req.ManagerDecisionAt, logs))
                        return UndoAlreadyApplied(req);
                    return Conflict("Chỉ có thể hoàn tác quyết định gần nhất của quản lý.");
                }

                var priorAiAction = logs.FirstOrDefault(x => x.Action.StartsWith("AI_PROCESSED_ESCALATE_", StringComparison.Ordinal)
                    || x.Action.StartsWith("VERIFY_ESCALATE_", StringComparison.Ordinal));
                if (priorAiAction is null) return Conflict("Không tìm thấy trạng thái chuyển tiếp ban đầu để hoàn tác.");

                req.Status = priorAiAction.Action[(priorAiAction.Action.IndexOf("ESCALATE_", StringComparison.Ordinal))..];
                req.ManagerAnswer = null;
                req.ManagerDecisionAt = null;
                req.RetentionExpiresAt = null;
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

            var originalStatus = req.Status;
            var outcome = EscalationWorkflow.Answer(originalStatus, answer);
            req.Status = outcome.Status;
            req.ManagerAnswer = answer;
            req.ManagerDecisionAt = DateTime.UtcNow;
            req.RetentionExpiresAt = ReceiptRetentionPolicy.ForManagerDecision(req);
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

        private IActionResult ManagerDecisionAlreadyApplied(AURA.Models.ReimbursementRequest request, bool answer)
        {
            var message = answer
                ? "Quyết định đồng ý của quản lý đã được ghi nhận trước đó."
                : "Quyết định từ chối của quản lý đã được ghi nhận trước đó.";
            TempData["Success"] = message;
            var redirectUrl = Url.Action("Index", "Home", new { tab = "reviewer" }) ?? "/?tab=reviewer";
            if (!IsAjaxRequest()) return Redirect(redirectUrl);
            return Json(new { message, status = request.Status, alreadyApplied = true, redirectUrl });
        }

        private IActionResult UndoAlreadyApplied(AURA.Models.ReimbursementRequest request)
        {
            const string message = "Lượt hoàn tác này đã được ghi nhận trước đó.";
            TempData["Success"] = message;
            var redirectUrl = Url.Action("Index", "Home", new { tab = "audit" }) ?? "/?tab=audit";
            if (!IsAjaxRequest()) return Redirect(redirectUrl);
            return Json(new { message, status = request.Status, alreadyApplied = true, redirectUrl });
        }

    }
}

