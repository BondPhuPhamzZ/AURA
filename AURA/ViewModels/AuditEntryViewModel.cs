using AURA.Models;

namespace AURA.ViewModels;

public sealed class AuditEntryViewModel
{
    public required AuditLog Log { get; init; }
    public ReimbursementRequest? Request { get; init; }
}
