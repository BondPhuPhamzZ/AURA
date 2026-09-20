using Microsoft.AspNetCore.Mvc;
using System.Threading.Tasks;
using AURA.Interfaces;
using System.Linq;
using AURA.ViewModels;

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
                var requests = (await _repo.GetAllRequestsAsync()).ToDictionary(x => x.Id);
                var logs = await _audit.GetRecentAsync();
                var items = logs.Select(log => new AuditEntryViewModel
                {
                    Log = log,
                    Request = requests.GetValueOrDefault(log.RequestId)
                });
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
