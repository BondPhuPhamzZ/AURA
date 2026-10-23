namespace AURA.Models;

/// <summary>Shared provider cooldown state used when the SQL AI gate is enabled.</summary>
public sealed class VisionProviderCooldown
{
    public string Provider { get; set; } = string.Empty;
    public DateTime CooldownUntilUtc { get; set; }
}
