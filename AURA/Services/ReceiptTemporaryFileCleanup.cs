using System.Text.RegularExpressions;

namespace AURA.Services;

public sealed record ReceiptTemporaryFileCleanupResult(int DeletedCount, int SkippedCount);

public static partial class ReceiptTemporaryFileCleanup
{
    private const int MaximumFilesPerRun = 500;

    [GeneratedRegex("^\\.[0-9a-f]{32}\\.(?:jpg|jpeg|png)\\.[0-9a-f]{32}\\.tmp$",
        RegexOptions.CultureInvariant | RegexOptions.IgnoreCase)]
    private static partial Regex StagingFileNamePattern();

    public static ReceiptTemporaryFileCleanupResult Clean(string root, DateTime utcNow,
        TimeSpan minimumAge)
    {
        if (minimumAge <= TimeSpan.Zero)
            throw new ArgumentOutOfRangeException(nameof(minimumAge));
        if (!Directory.Exists(root)) return new ReceiptTemporaryFileCleanupResult(0, 0);

        var deleted = 0;
        var skipped = 0;
        foreach (var path in Directory.EnumerateFiles(root, "*.tmp", SearchOption.TopDirectoryOnly))
        {
            if (deleted + skipped >= MaximumFilesPerRun) break;
            if (!StagingFileNamePattern().IsMatch(Path.GetFileName(path))) continue;

            try
            {
                if ((File.GetAttributes(path) & FileAttributes.ReparsePoint) != 0 ||
                    utcNow - File.GetLastWriteTimeUtc(path) < minimumAge)
                    continue;

                // Receipt uploads create each staging path exclusively. Taking an exclusive
                // handle ensures a live writer is never mistaken for crash debris.
                using (new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.None,
                           1, FileOptions.SequentialScan))
                {
                    if (utcNow - File.GetLastWriteTimeUtc(path) < minimumAge)
                        continue;
                }

                File.Delete(path);
                deleted++;
            }
            catch (FileNotFoundException)
            {
                // Another cleanup pass or normal upload completion removed/moved it.
            }
            catch (DirectoryNotFoundException)
            {
                // The storage root was removed while this pass was enumerating it.
                break;
            }
            catch (IOException)
            {
                skipped++;
            }
            catch (UnauthorizedAccessException)
            {
                skipped++;
            }
        }

        return new ReceiptTemporaryFileCleanupResult(deleted, skipped);
    }
}
