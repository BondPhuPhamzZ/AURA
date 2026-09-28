namespace AURA.Services;

public static class VisionFallbackPolicy
{
    private static readonly HashSet<string> EligibleCodes = new(StringComparer.Ordinal)
    {
        "AI_TIMEOUT",
        "AI_RATE_LIMIT",
        "AI_NOT_CONFIGURED",
        "AI_TEMPORARILY_UNAVAILABLE",
        "AI_AUTH_ERROR",
        "AI_CREDITS_REQUIRED",
        "AI_MODEL_UNAVAILABLE",
        "AI_HTTP_ERROR",
        "AI_LOCAL_UNAVAILABLE"
    };

    public static bool IsEligible(string code) => EligibleCodes.Contains(code);
}
