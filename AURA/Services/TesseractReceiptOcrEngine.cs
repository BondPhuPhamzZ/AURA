using AURA.Interfaces;
using AURA.Models;
using AURA.Options;
using Microsoft.Extensions.Options;
using Tesseract;

namespace AURA.Services;

/// <summary>
/// In-process Tesseract 5 OCR. The native engine is created lazily on first use, so a host with
/// the feature disabled never loads it. One recognition runs at a time; the image stays on this
/// machine (on Windows leptonica stages it in a delete-on-close temp file while decoding).
/// </summary>
public sealed class TesseractReceiptOcrEngine : IReceiptOcrEngine, IDisposable
{
    private readonly EvidenceLocalizationOptions _options;
    private readonly SemaphoreSlim _gate = new(1, 1);
    private TesseractEngine? _engine;

    public TesseractReceiptOcrEngine(IOptions<EvidenceLocalizationOptions> options)
    {
        _options = options.Value;
    }

    public string Name => $"tesseract-5.2.0/{_options.Languages}";

    public async Task<OcrPage> RecognizeAsync(byte[] imageBytes, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(imageBytes);
        await _gate.WaitAsync(cancellationToken);
        // Tesseract cannot be interrupted mid-page. Keep the gate until the native call returns,
        // even if the caller stops waiting, so two recognitions never share the engine.
        var work = Task.Run(() => Recognize(imageBytes), CancellationToken.None);
        _ = work.ContinueWith(_ => _gate.Release(), CancellationToken.None,
            TaskContinuationOptions.ExecuteSynchronously, TaskScheduler.Default);
        return await work.WaitAsync(TimeSpan.FromSeconds(_options.TimeoutSeconds), cancellationToken);
    }

    private OcrPage Recognize(byte[] imageBytes)
    {
        _engine ??= new TesseractEngine(_options.TessdataPath, _options.Languages, EngineMode.LstmOnly);
        using var pix = Pix.LoadFromMemory(imageBytes);
        using var page = _engine.Process(pix, PageSegMode.Auto);
        var words = new List<OcrWord>();
        using var iterator = page.GetIterator();
        iterator.Begin();
        var lineId = 0;
        do
        {
            if (iterator.IsAtBeginningOf(PageIteratorLevel.TextLine)) lineId++;
            var text = iterator.GetText(PageIteratorLevel.Word)?.Trim();
            if (!string.IsNullOrEmpty(text) && iterator.TryGetBoundingBox(PageIteratorLevel.Word, out var box))
                words.Add(new OcrWord(text, box.X1, box.Y1, box.X2, box.Y2, lineId));
        } while (iterator.Next(PageIteratorLevel.Word));
        return new OcrPage(pix.Width, pix.Height, words);
    }

    public void Dispose()
    {
        _engine?.Dispose();
        _gate.Dispose();
    }
}
