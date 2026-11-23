using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using ReviewAgent.Infrastructure.Logging;
using ReviewAgent.Infrastructure.Persistence.RunLog;

namespace ReviewAgent.Infrastructure.Tests.Logging;

/// <summary>
/// A logger factory with only the run log provider, to see which lines a class logs into a review run's log.
/// </summary>
internal sealed class RunLogCapture : IDisposable
{
    private readonly RunLogQueue _queue = new(Options.Create(new RunLogOptions()));
    private readonly ILoggerFactory _loggerFactory;

    public RunLogCapture()
    {
        var provider = new RunLogProvider(_queue, TimeProvider.System);
        _loggerFactory = LoggerFactory.Create(logging => logging.AddProvider(provider));
    }

    public ILogger<T> CreateLogger<T>() => _loggerFactory.CreateLogger<T>();

    /// <summary>The lines kept so far, in the order they were logged.</summary>
    public IReadOnlyList<StoredRunLogEntry> Lines()
    {
        var lines = new List<StoredRunLogEntry>();
        while (_queue.Reader.TryRead(out var line))
        {
            lines.Add(line);
        }

        return lines;
    }

    public void Dispose() => _loggerFactory.Dispose();
}
