using AURA.Options;

namespace AURA.Services;

public static class DriveSandboxRuntimePolicy
{
    public static bool IsAllowed(DriveSandboxOptions options, string environmentName, string? urls)
    {
        if (!options.Enabled ||
            !string.Equals(environmentName, "Development", StringComparison.OrdinalIgnoreCase) ||
            options.RequireMalwareScanner || string.IsNullOrWhiteSpace(urls))
            return false;
        var addresses = urls.Split(';',
            StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        return addresses.Length > 0 &&
            addresses.All(value => Uri.TryCreate(value, UriKind.Absolute, out var uri) &&
                uri.IsLoopback && uri.Scheme is "http" or "https");
    }
}
