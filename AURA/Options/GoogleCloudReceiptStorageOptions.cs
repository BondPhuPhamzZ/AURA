using System.ComponentModel.DataAnnotations;

namespace AURA.Options;

public sealed class GoogleCloudReceiptStorageOptions
{
    public const string SectionName = "ReceiptStorage:GoogleCloudStorage";

    [Required]
    public string Bucket { get; set; } = string.Empty;

    [Required]
    public string ServiceAccountJsonPath { get; set; } = string.Empty;

    public string Prefix { get; set; } = "aura/receipts";
}
