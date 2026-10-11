using AURA.Options;
using AURA.Services;
using Microsoft.Extensions.Options;
using Xunit;

namespace AURA.Tests;

public sealed class VisionInvocationCoordinatorTests
{
    [Fact]
    public async Task Serializes_AI_calls_when_capacity_is_one()
    {
        var coordinator = CreateCoordinator();
        using var first = await coordinator.EnterAsync(CancellationToken.None);

        var secondTask = coordinator.EnterAsync(CancellationToken.None).AsTask();
        await Task.Delay(50);
        Assert.False(secondTask.IsCompleted);

        first.Dispose();
        using var second = await secondTask.WaitAsync(TimeSpan.FromSeconds(1));
    }

    [Fact]
    public void Tracks_and_expires_provider_cooldown()
    {
        var coordinator = CreateCoordinator();
        var now = new DateTime(2026, 10, 12, 0, 0, 0, DateTimeKind.Utc);

        coordinator.RecordCooldown("OpenRouter", now, TimeSpan.FromSeconds(120));

        Assert.Equal(TimeSpan.FromSeconds(90),
            coordinator.GetRemainingCooldown("openrouter", now.AddSeconds(30)));
        Assert.Null(coordinator.GetRemainingCooldown("OpenRouter", now.AddSeconds(121)));
    }

    [Fact]
    public void Never_shortens_an_existing_cooldown()
    {
        var coordinator = CreateCoordinator();
        var now = new DateTime(2026, 10, 12, 0, 0, 0, DateTimeKind.Utc);
        coordinator.RecordCooldown("OpenRouter", now, TimeSpan.FromSeconds(120));
        coordinator.RecordCooldown("OpenRouter", now.AddSeconds(10), TimeSpan.FromSeconds(20));

        Assert.Equal(TimeSpan.FromSeconds(110), coordinator.GetRemainingCooldown("OpenRouter", now.AddSeconds(10)));
    }

    private static VisionInvocationCoordinator CreateCoordinator() => new(
        Microsoft.Extensions.Options.Options.Create(
            new VisionOptions { MaxConcurrentRequests = 1 }));
}
