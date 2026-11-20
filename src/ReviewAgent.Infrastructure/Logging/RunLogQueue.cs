using System.Threading.Channels;
using Microsoft.Extensions.Options;
using ReviewAgent.Infrastructure.Persistence.RunLog;

namespace ReviewAgent.Infrastructure.Logging;

/// <summary>
/// The log lines of review runs waiting to be saved. The logger only puts lines here, so logging never waits for
/// the database; <see cref="RunLogWriter"/> saves them.
/// </summary>
internal sealed class RunLogQueue
{
    private readonly Channel<StoredRunLogEntry> _channel;
    private long _droppedCount;

    public RunLogQueue(IOptions<RunLogOptions> options)
    {
        ArgumentNullException.ThrowIfNull(options);

        // A full queue drops its oldest line instead of making the review wait; the drops are counted and logged.
        _channel = Channel.CreateBounded<StoredRunLogEntry>(
            new BoundedChannelOptions(options.Value.QueueCapacity)
            {
                FullMode = BoundedChannelFullMode.DropOldest,
                SingleReader = true,
            },
            _ => Interlocked.Increment(ref _droppedCount));
    }

    public ChannelReader<StoredRunLogEntry> Reader => _channel.Reader;

    /// <summary>Adds a line; never waits.</summary>
    public void Add(StoredRunLogEntry entry) => _channel.Writer.TryWrite(entry);

    /// <summary>How many lines were dropped since the last call.</summary>
    public long TakeDroppedCount() => Interlocked.Exchange(ref _droppedCount, 0);
}
