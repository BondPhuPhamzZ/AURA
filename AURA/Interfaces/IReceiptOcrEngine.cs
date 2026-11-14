using AURA.Models;

namespace AURA.Interfaces;

/// <summary>Local OCR used only to place evidence boxes for reviewers; never for decisions.</summary>
public interface IReceiptOcrEngine
{
    /// <summary>Engine identity recorded with every result, e.g. "tesseract-5.2.0/vie+eng".</summary>
    string Name { get; }

    Task<OcrPage> RecognizeAsync(byte[] imageBytes, CancellationToken cancellationToken);
}
