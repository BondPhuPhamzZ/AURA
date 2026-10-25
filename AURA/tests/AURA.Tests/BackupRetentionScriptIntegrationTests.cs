using System.Diagnostics;
using System.Security.Cryptography;
using System.Text.Json;
using Xunit;

namespace AURA.Tests;

public sealed class BackupRetentionScriptIntegrationTests
{
    [Theory]
    [InlineData("powershell.exe", false)]
    [InlineData("powershell.exe", true)]
    [InlineData("pwsh.exe", false)]
    [InlineData("pwsh.exe", true)]
    public async Task Backup_cleanup_preserves_precise_UTC_deadline_and_never_deletes_early(
        string shell, bool metadataOnly)
    {
        // Opt-in because both Windows PowerShell 5.1 and PowerShell 7 must be installed.
        if (Environment.GetEnvironmentVariable("AURA_RUN_POWERSHELL_INTEGRATION") != "1") return;
        var name = "AuraBackupUtcIT_" + Guid.NewGuid().ToString("N");
        var root = Path.Combine(Path.GetTempPath(), name);
        var set = Path.Combine(root, "AURA_BACKUP_synthetic");
        var receipts = Path.Combine(set, "receipts");
        Directory.CreateDirectory(receipts);
        try
        {
            var database = Path.Combine(set, "synthetic.bak");
            await File.WriteAllTextAsync(database, "SYNTHETIC_MARKER_ONLY");
            var now = DateTimeOffset.UtcNow;
            var expiry = now.AddHours(3).AddTicks(1234);
            var imageBytes = new byte[] { 1, 2, 3 };
            var imageHash = Convert.ToHexString(SHA256.HashData(imageBytes));
            if (!metadataOnly)
                await File.WriteAllBytesAsync(Path.Combine(receipts, "synthetic.jpg"), imageBytes);
            var manifest = new Dictionary<string, object?>
            {
                ["format"] = "AURA_LOCAL_BACKUP_RETENTION_V1",
                ["backupSetName"] = "AURA_BACKUP_synthetic",
                ["databaseBackup"] = new
                {
                    path = "synthetic.bak", sizeBytes = new FileInfo(database).Length,
                    sha256 = Convert.ToHexString(SHA256.HashData(await File.ReadAllBytesAsync(database)))
                },
                ["createdAtUtc"] = now.ToString("o"),
                ["earliestIncludedDataExpiryUtc"] = expiry.ToOffset(TimeSpan.FromHours(7)).ToString("o"),
                ["purgeAtUtc"] = expiry.ToString("o"),
                ["retentionDays"] = 7,
                ["receiptCount"] = metadataOnly ? 0 : 1,
                ["receipts"] = metadataOnly ? Array.Empty<object>() : new object[]
                {
                    new { path = "receipts/synthetic.jpg", sizeBytes = 3, sha256 = imageHash,
                        retentionExpiresAtUtc = expiry.ToString("o") }
                }
            };
            if (metadataOnly)
            {
                manifest["metadataOnlyRecordCount"] = 1;
                manifest["metadataOnlyExpiriesUtc"] = new[] { expiry.ToString("o") };
            }
            await File.WriteAllTextAsync(Path.Combine(set, ".aura-retention.json"), JsonSerializer.Serialize(manifest));

            await RunCleanupAsync(shell, root, expiry.AddTicks(-1), apply: true);
            Assert.True(Directory.Exists(set));
            var report = await RunCleanupAsync(shell, root, expiry, apply: false);
            Assert.Contains("EXPIRED (report only)", report, StringComparison.Ordinal);
            Assert.Contains(expiry.ToString("o"), report, StringComparison.Ordinal);
            Assert.True(Directory.Exists(set));
            await RunCleanupAsync(shell, root, expiry, apply: true);
            Assert.False(Directory.Exists(set));
        }
        finally
        {
            var resolved = Path.GetFullPath(root);
            if (Path.GetDirectoryName(resolved)?.TrimEnd(Path.DirectorySeparatorChar) !=
                    Path.GetTempPath().TrimEnd(Path.DirectorySeparatorChar) || Path.GetFileName(resolved) != name)
                throw new InvalidOperationException("Unsafe synthetic test cleanup path.");
            Directory.Delete(resolved, recursive: true);
        }
    }

    private static async Task<string> RunCleanupAsync(string shell, string root, DateTimeOffset now, bool apply)
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory is not null && !File.Exists(Path.Combine(directory.FullName, "AURA.csproj")))
            directory = directory.Parent;
        if (directory is null) throw new DirectoryNotFoundException("AURA project root not found.");
        var start = new ProcessStartInfo(shell) { UseShellExecute = false, CreateNoWindow = true,
            RedirectStandardOutput = true, RedirectStandardError = true };
        // A test runner launched by PowerShell 7 inherits its module path. Let
        // each child shell discover its own version's built-in modules.
        start.Environment.Remove("PSModulePath");
        foreach (var argument in new[] { "-NoProfile", "-File",
            Path.Combine(directory.FullName, "tools", "Remove-ExpiredAuraLocalBackups.ps1"),
            "-BackupRoot", root, "-NowUtc", now.ToString("o") })
            start.ArgumentList.Add(argument);
        if (apply) start.ArgumentList.Add("-Apply");
        using var process = Process.Start(start) ?? throw new InvalidOperationException("PowerShell could not start.");
        var stdout = process.StandardOutput.ReadToEndAsync();
        var stderr = process.StandardError.ReadToEndAsync();
        using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(20));
        try { await process.WaitForExitAsync(timeout.Token); }
        finally { if (!process.HasExited) process.Kill(entireProcessTree: true); }
        var output = await stdout;
        var error = await stderr;
        Assert.True(process.ExitCode == 0, output + error);
        return output;
    }
}
