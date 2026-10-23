using System.ComponentModel.DataAnnotations;

namespace AURA.Options;

public sealed class ReceiptStorageOptions
{
    public const string SectionName = "ReceiptStorage";

    public string Provider { get; set; } = "LocalFileSystem";

    [Required]
    public string Directory { get; set; } = "App_Data/receipts";

    [Range(1, 20)]
    public int MaxFileSizeMb { get; set; } = 5;

    [Range(1, 720)]
    public int StaleTempFileAgeHours { get; set; } = 24;

    [Range(5, 1440)]
    public int TempFileCleanupIntervalMinutes { get; set; } = 60;
}
