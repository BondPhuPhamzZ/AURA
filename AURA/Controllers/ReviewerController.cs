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
            if (string.IsNullOrWhiteSpace(id) || decision is not ("APPROVE" or "REJECT"))
                return BadRequest();

            var req = await _repo.GetRequestByIdAsync(id);
            if (req == null) return NotFound();

            if (!PolicyDecisionEngine.IsEscalation(req.Status))
                return Conflict("Hồ sơ này không còn ở trạng thái chờ quyết định của quản lý.");

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

