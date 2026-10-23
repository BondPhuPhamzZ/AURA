namespace AURA.Models;

/// <summary>
/// Monotonic SQL fencing counter for the single shared vision-provider permit.
/// A result may only be accepted by the permit generation that acquired it.
/// </summary>
public sealed class VisionInvocationFence
{
    public int Id { get; set; }
    public long Token { get; set; }
}
