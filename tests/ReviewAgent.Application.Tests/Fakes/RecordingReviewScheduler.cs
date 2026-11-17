using ReviewAgent.Application.Reviews.Scheduling;

namespace ReviewAgent.Application.Tests.Fakes;

/// <summary>Remembers the queued runs instead of reviewing them.</summary>
internal sealed class RecordingReviewScheduler : IReviewScheduler
{
    public List<Guid> RunIds { get; } = [];

    public ValueTask ScheduleAsync(Guid runId, CancellationToken cancellationToken)
    {
        RunIds.Add(runId);
        return ValueTask.CompletedTask;
    }
}
