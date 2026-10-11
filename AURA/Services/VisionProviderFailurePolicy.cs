namespace AURA.Services;

public static class VisionProviderFailurePolicy
{
    public static bool IsBlocking(string code) => code is
        "AI_TIMEOUT" or "AI_RATE_LIMIT" or "AI_PROVIDER_COOLDOWN" or
        "AI_NOT_CONFIGURED" or "AI_TEMPORARILY_UNAVAILABLE" or
        "AI_AUTH_ERROR" or "AI_CREDITS_REQUIRED" or "AI_MODEL_UNAVAILABLE" or
        "AI_REQUEST_INVALID" or "AI_ALL_PROVIDERS_UNAVAILABLE";
}
