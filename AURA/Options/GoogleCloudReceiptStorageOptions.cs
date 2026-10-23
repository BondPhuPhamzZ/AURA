namespace AURA.Options;

public sealed class GoogleCloudReceiptStorageOptions
{
    public const string SectionName = "ReceiptStorage:GoogleCloudStorage";
    public const string JsonServiceAccount = "ServiceAccountJson";
    public const string ApplicationDefault = "ApplicationDefault";

    public string Bucket { get; set; } = string.Empty;

    public string ServiceAccountJsonPath { get; set; } = string.Empty;

    public string CredentialMode { get; set; } = JsonServiceAccount;

    public string Prefix { get; set; } = "aura/receipts";
}
