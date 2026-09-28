namespace AURA.Services;

public sealed class VisionExecutionContext
{
    public string? PrimaryProvider { get; private set; }
    public string? ServedProvider { get; private set; }
    public string? PrimaryErrorCode { get; private set; }
    public bool FallbackUsed { get; private set; }

    public void Begin(string primaryProvider)
    {
        PrimaryProvider = primaryProvider;
        ServedProvider = null;
        PrimaryErrorCode = null;
        FallbackUsed = false;
    }

    public void MarkSuccess(string provider, bool fallbackUsed = false)
    {
        ServedProvider = provider;
        FallbackUsed = fallbackUsed;
    }

    public void MarkPrimaryFailure(string code) => PrimaryErrorCode = code;
}
