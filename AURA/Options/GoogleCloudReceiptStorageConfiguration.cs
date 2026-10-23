namespace AURA.Options;

public static class GoogleCloudReceiptStorageConfiguration
{
    public static bool IsValid(GoogleCloudReceiptStorageOptions options, string contentRoot,
        bool isProviderEnabled, string? applicationCredentialsPath = null)
    {
        if (!isProviderEnabled) return true;
        if (string.IsNullOrWhiteSpace(options.Bucket) ||
            !System.Text.RegularExpressions.Regex.IsMatch(options.Bucket,
                "^[a-z0-9][a-z0-9.-]{1,61}[a-z0-9]$") ||
            string.IsNullOrWhiteSpace(options.Prefix) ||
            !System.Text.RegularExpressions.Regex.IsMatch(options.Prefix, "^[A-Za-z0-9._/-]+$") ||
            options.Prefix.Contains("..", StringComparison.Ordinal) ||
            options.Prefix.Contains("//", StringComparison.Ordinal))
            return false;

        if (string.Equals(options.CredentialMode, GoogleCloudReceiptStorageOptions.ApplicationDefault,
                StringComparison.OrdinalIgnoreCase))
        {
            if (!string.IsNullOrWhiteSpace(options.ServiceAccountJsonPath)) return false;
            return string.IsNullOrWhiteSpace(applicationCredentialsPath) ||
                IsExistingExternalPath(applicationCredentialsPath, contentRoot);
        }

        if (!string.Equals(options.CredentialMode, GoogleCloudReceiptStorageOptions.JsonServiceAccount,
                StringComparison.OrdinalIgnoreCase) ||
            !IsExistingExternalPath(options.ServiceAccountJsonPath, contentRoot))
            return false;

        return true;
    }

    private static bool IsExistingExternalPath(string path, string contentRoot)
    {
        if (!Path.IsPathFullyQualified(path) || !File.Exists(path)) return false;
        var relative = Path.GetRelativePath(Path.GetFullPath(contentRoot), Path.GetFullPath(path));
        return Path.IsPathFullyQualified(relative) || relative == ".." ||
            relative.StartsWith(".." + Path.DirectorySeparatorChar, StringComparison.Ordinal);
    }
}
