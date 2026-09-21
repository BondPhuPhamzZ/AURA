using AURA.Models;
using AURA.ViewModels;

namespace AURA.Services;

public static class AuditTrailProjector
{
    public static IReadOnlyList<AuditEntryViewModel> Project(
        IEnumerable<AuditLog> logs,
        IReadOnlyDictionary<string, ReimbursementRequest> requests)
    {
        return logs
            .GroupBy(log => log.RequestId, StringComparer.Ordinal)
            .Select(group =>
            {
                var timeline = group
                    .OrderByDescending(log => log.Timestamp)
                    .ThenByDescending(log => log.Id)
                    .ToList();
                return new AuditEntryViewModel
                {
                    Log = timeline[0],
                    Timeline = timeline,
                    Request = requests.GetValueOrDefault(group.Key)
                };
            })
            .OrderByDescending(entry => entry.Log.Timestamp)
            .ThenByDescending(entry => entry.Log.Id)
            .ToList();
    }
}
