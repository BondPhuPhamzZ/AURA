using Microsoft.AspNetCore.Mvc;
using System.Threading.Tasks;
using AURA.Interfaces;
using System.Linq;
using AURA.ViewModels;
using AURA.Services;

namespace AURA.ViewComponents
{
    public class AuditLogQueueViewComponent : ViewComponent
    {
        private readonly IReimbursementRepository _repo;
        private readonly IAuditLogger _audit;
        private readonly ILogger<AuditLogQueueViewComponent> _logger;

        public AuditLogQueueViewComponent(IReimbursementRepository repo, IAuditLogger audit,
            ILogger<AuditLogQueueViewComponent> logger)
        {
            _repo = repo;
            _audit = audit;
            _logger = logger;
        }

        public async Task<IViewComponentResult> InvokeAsync()
        {
            try
            {
                // Read the event window first, then every request touched at or after its oldest
                // event, so a request created between the two reads is never shown as missing.
                var logs = await _audit.GetRecentAsync(500);
                var requests = logs.Count == 0
                    ? new Dictionary<string, AURA.Models.ReimbursementRequest>()
                    : (await _repo.GetRequestsWithAuditSinceAsync(logs.Min(x => x.Timestamp)))
                        .ToDictionary(x => x.Id);
                var items = AuditTrailProjector.Project(logs, requests);
                return View(items);
            }
            catch (Exception exception)
            {
                _logger.LogError(exception, "Cannot load audit trail.");
                ViewData["LoadError"] = "Không thể tải nhật ký kiểm toán. Hãy kiểm tra kết nối cơ sở dữ liệu.";
                return View(Array.Empty<AuditEntryViewModel>());
            }
        }
    }
}
