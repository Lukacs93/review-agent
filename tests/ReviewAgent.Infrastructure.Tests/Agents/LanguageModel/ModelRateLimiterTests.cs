using Microsoft.Extensions.Logging.Abstractions;
using ReviewAgent.Infrastructure.Agents.LanguageModel;

namespace ReviewAgent.Infrastructure.Tests.Agents.LanguageModel;

public sealed class ModelRateLimiterTests
{
    // A short window instead of a minute, so the test does not wait long.
    private static readonly TimeSpan _window = TimeSpan.FromMilliseconds(300);

    [Fact]
    public async Task Request_within_the_limit_goes_at_once()
    {
        using var limiter = Limiter(permitLimit: 2);

        using var lease = await limiter.AcquireAsync(CancellationToken.None);

        Assert.True(lease.IsAcquired);
    }

    [Fact]
    public async Task Request_over_the_limit_waits_for_its_turn_instead_of_failing()
    {
        using var limiter = Limiter(permitLimit: 1);
        using var first = await limiter.AcquireAsync(CancellationToken.None);

        var second = limiter.AcquireAsync(CancellationToken.None).AsTask();

        Assert.False(second.IsCompleted);
        using var lease = await second;
        Assert.True(lease.IsAcquired);
    }

    private static ModelRateLimiter Limiter(int permitLimit) =>
        new(permitLimit, _window, TimeProvider.System, NullLogger<ModelRateLimiter>.Instance);
}
