using Microsoft.Extensions.Logging;
using ReviewAgent.Infrastructure.Persistence.RunLog;

namespace ReviewAgent.Infrastructure.Logging;

/// <summary>
/// A logging provider that keeps the log lines written while a review run is running: the lines inside a log scope
/// with the run's ID. Other lines are left to the other providers (console, OpenTelemetry).
/// </summary>
// The .NET way to add a log destination: a provider the logger factory sends every line to, and scopes through
// ISupportExternalScope. "RunLog" is its name in the Logging section of the configuration.
[ProviderAlias("RunLog")]
internal sealed class RunLogProvider(RunLogQueue queue, TimeProvider timeProvider)
    : ILoggerProvider, ISupportExternalScope
{
    /// <summary>The key of the run's ID in the log scope that marks a review run.</summary>
    public const string RunIdKey = "RunId";

    private readonly RunLogQueue _queue = queue;
    private readonly TimeProvider _timeProvider = timeProvider;
    private IExternalScopeProvider? _scopeProvider;

    public ILogger CreateLogger(string categoryName) => new RunLogger(this, categoryName);

    public void SetScopeProvider(IExternalScopeProvider scopeProvider) => _scopeProvider = scopeProvider;

    public void Dispose()
    {
    }

    // The innermost scope with a run's ID wins, should scopes ever be nested.
    private Guid? RunIdOfCurrentScope()
    {
        Guid? runId = null;
        _scopeProvider?.ForEachScope(
            (scope, _) =>
            {
                if (scope is IEnumerable<KeyValuePair<string, object?>> values)
                {
                    foreach (var (key, value) in values)
                    {
                        if (key == RunIdKey && value is Guid id)
                        {
                            runId = id;
                        }
                    }
                }
            },
            (object?)null);
        return runId;
    }

    private sealed class RunLogger(RunLogProvider provider, string categoryName) : ILogger
    {
        public IDisposable? BeginScope<TState>(TState state)
            where TState : notnull =>
            provider._scopeProvider?.Push(state);

        // Which levels and categories reach this provider is set by the logging filters.
        public bool IsEnabled(LogLevel logLevel) => logLevel != LogLevel.None;

        public void Log<TState>(
            LogLevel logLevel,
            EventId eventId,
            TState state,
            Exception? exception,
            Func<TState, Exception?, string> formatter)
        {
            ArgumentNullException.ThrowIfNull(formatter);

            if (!IsEnabled(logLevel) || provider.RunIdOfCurrentScope() is not { } runId)
            {
                return;
            }

            provider._queue.Add(new StoredRunLogEntry
            {
                RunId = runId,
                Timestamp = provider._timeProvider.GetUtcNow(),
                Level = logLevel.ToString(),
                Category = categoryName,
                Message = formatter(state, exception),
                Exception = exception?.ToString(),
            });
        }
    }
}
