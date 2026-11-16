namespace ReviewAgent.Application.Tests.Fakes;

/// <summary>A clock that always shows the same time, so tests can check the times handlers record.</summary>
internal sealed class FixedTimeProvider(DateTimeOffset now) : TimeProvider
{
    public override DateTimeOffset GetUtcNow() => now;
}
