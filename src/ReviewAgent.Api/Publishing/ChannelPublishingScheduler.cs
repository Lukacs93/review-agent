using System.Threading.Channels;
using ReviewAgent.Application.Reviews.Scheduling;

namespace ReviewAgent.Api.Publishing;

/// <summary>
/// An in-memory queue of the runs to publish. The API writes to it when publishing is asked for, and
/// <see cref="PublishingWorker"/> reads from it. Runs still queued when the application stops are not lost: they
/// stay Publishing in the event store and are queued again at the next start.
/// </summary>
internal sealed class ChannelPublishingScheduler : IPublishingScheduler, IScheduledPublishingReader
{
    private readonly Channel<Guid> _runIds = Channel.CreateUnbounded<Guid>(
        new UnboundedChannelOptions { SingleReader = true });

    public ValueTask ScheduleAsync(Guid runId, CancellationToken cancellationToken) =>
        _runIds.Writer.WriteAsync(runId, cancellationToken);

    public IAsyncEnumerable<Guid> ReadAllAsync(CancellationToken cancellationToken) =>
        _runIds.Reader.ReadAllAsync(cancellationToken);
}
