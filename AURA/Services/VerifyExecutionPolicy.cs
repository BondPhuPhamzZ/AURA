namespace AURA.Services;

/// <summary>
/// The unauthenticated Verify Harness is a development-only diagnostic. A hosting
/// environment variable cannot accidentally expose it on a public Production site.
/// </summary>
public static class VerifyExecutionPolicy
{
    public static bool IsAllowed(bool enabled, string? environmentName) =>
        enabled && string.Equals(environmentName, "Development", StringComparison.OrdinalIgnoreCase);
}
