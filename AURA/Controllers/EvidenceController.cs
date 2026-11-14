using AURA.Interfaces;
using AURA.Models;
using AURA.Services;
using Microsoft.AspNetCore.Mvc;

namespace AURA.Controllers;

/// <summary>
/// Read-only evidence regions for the receipt viewer. Returns 404 unless the local
/// localizer is enabled on a Development host, so Production exposes nothing new.
/// </summary>
public sealed class EvidenceController : Controller
{
    private readonly IReimbursementRepository _repository;
    private readonly EvidenceLocalizationService _localization;

    public EvidenceController(IReimbursementRepository repository, EvidenceLocalizationService localization)
    {
        _repository = repository;
        _localization = localization;
    }

    [HttpGet]
    [ResponseCache(NoStore = true, Location = ResponseCacheLocation.None)]
    public async Task<IActionResult> Regions(string id, CancellationToken cancellationToken)
    {
        if (!_localization.IsActive || string.IsNullOrWhiteSpace(id)) return NotFound();
        var request = await _repository.GetPersistedRequestByIdAsync(id);
        if (request is null || string.IsNullOrWhiteSpace(request.StoredFileName)) return NotFound();

        var (status, result) = await _localization.LocalizeAsync(request, cancellationToken);
        if (status == EvidenceLocalizationService.Status.NotFound || result is null) return NotFound();
        return Json(new
        {
            imageSha256 = result.ImageSha256,
            coordinateSpace = new
            {
                width = EvidenceLocalizationResult.CoordinateSpace,
                height = EvidenceLocalizationResult.CoordinateSpace,
                orientationApplied = true
            },
            localizer = result.Localizer,
            regions = result.Regions.Select(region => new
            {
                field = region.Field,
                label = region.Label,
                text = region.Text,
                box = new { x = region.Box.X, y = region.Box.Y, width = region.Box.Width, height = region.Box.Height }
            }),
            unlocated = result.Unlocated.Select(item => new { field = item.Field, label = item.Label, reason = item.Reason })
        });
    }
}
