using Microsoft.AspNetCore.Mvc;
using System.Threading.Tasks;
using AURA.Interfaces;

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
        public async Task<IActionResult> EscalateAction(string id, string decision)
        {
            var req = await _repo.GetRequestByIdAsync(id);
            if (req == null) return NotFound();

            if (decision == "APPROVE")
            {
                req.Status = "APPROVED_BY_MANAGER";
                await _audit.LogActionAsync(req.Id, "MANAGER_APPROVE", "Sếp đã duyệt ngoại lệ thành công.");
            }
            else
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
