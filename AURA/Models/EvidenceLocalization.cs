namespace AURA.Models;

/// <summary>One OCR word with its pixel box; <see cref="LineId"/> groups words of one OCR text line.</summary>
public sealed record OcrWord(string Text, int X1, int Y1, int X2, int Y2, int LineId);

/// <summary>OCR output for one image in its stored pixel orientation.</summary>
public sealed record OcrPage(int Width, int Height, IReadOnlyList<OcrWord> Words);

/// <summary>Box in a 0..1000 coordinate space relative to the displayed image.</summary>
public sealed record EvidenceBox(int X, int Y, int Width, int Height);

public sealed record EvidenceRegion(string Field, string Label, string Text, EvidenceBox Box);

public sealed record UnlocatedEvidence(string Field, string Label, string Reason);

public sealed class EvidenceLocalizationResult
{
    public const int CoordinateSpace = 1000;

    public required string ImageSha256 { get; init; }
    public required string Localizer { get; init; }
    public IReadOnlyList<EvidenceRegion> Regions { get; init; } = Array.Empty<EvidenceRegion>();
    public IReadOnlyList<UnlocatedEvidence> Unlocated { get; init; } = Array.Empty<UnlocatedEvidence>();
}
