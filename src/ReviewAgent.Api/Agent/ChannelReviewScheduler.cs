using System.Threading.Channels;
using ReviewAgent.Application.Reviews;

namespace ReviewAgent.Api.Agent;

/// <summary>
/// An in-memory queue of review runs. The API writes to it when a review starts, and <see cref="ReviewWorker"/>
/// reads from it. Runs still queued when the application stops are lost; they stay in the Running status.
/// </summary>
internal sealed class ChannelReviewScheduler : IReviewScheduler, IScheduledReviewReader
{
    private readonly Channel<Guid> _runIds = Channel.CreateUnbounded<Guid>(
        new UnboundedChannelOptions { SingleReader = true });

    public ValueTask ScheduleAsync(Guid runId, CancellationToken cancellationToken) =>
        _runIds.Writer.WriteAsync(runId, cancellationToken);

    public IAsyncEnumerable<Guid> ReadAllAsync(CancellationToken cancellationToken) =>
        _runIds.Reader.ReadAllAsync(cancellationToken);
}
