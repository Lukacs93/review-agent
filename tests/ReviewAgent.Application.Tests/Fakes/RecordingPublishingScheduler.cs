using ReviewAgent.Application.Reviews.Scheduling;

namespace ReviewAgent.Application.Tests.Fakes;

/// <summary>Remembers the runs scheduled for publishing instead of publishing them.</summary>
internal sealed class RecordingPublishingScheduler : IPublishingScheduler
{
    public List<Guid> RunIds { get; } = [];

    public ValueTask ScheduleAsync(Guid runId, CancellationToken cancellationToken)
    {
        RunIds.Add(runId);
        return ValueTask.CompletedTask;
    }
}
