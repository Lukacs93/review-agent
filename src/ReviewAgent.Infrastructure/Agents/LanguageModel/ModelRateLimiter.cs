using System.Threading.RateLimiting;
using Microsoft.Extensions.Logging;

namespace ReviewAgent.Infrastructure.Agents.LanguageModel;

/// <summary>
/// Keeps the model's requests within the number its service allows in a time window (<c>Agent:RequestsPerMinute</c>),
/// so a request waits for its turn instead of being refused (429). One for the whole application: every model
/// request of every review counts.
/// </summary>
// A sliding window of short segments, so no window's worth of time holds more requests than allowed, however the
// service counts its windows. The requests wait in order; a review sends them one at a time, so the queue stays short.
internal sealed partial class ModelRateLimiter : IDisposable
{
    private const int SegmentsPerWindow = 60;

    private readonly SlidingWindowRateLimiter _limiter;
    private readonly int _permitLimit;
    private readonly TimeProvider _timeProvider;
    private readonly ILogger<ModelRateLimiter> _logger;

    public ModelRateLimiter(
        int permitLimit,
        TimeSpan window,
        TimeProvider timeProvider,
        ILogger<ModelRateLimiter> logger)
    {
        _permitLimit = permitLimit;
        _timeProvider = timeProvider;
        _logger = logger;
        _limiter = new SlidingWindowRateLimiter(new SlidingWindowRateLimiterOptions
        {
            PermitLimit = permitLimit,
            Window = window,
            SegmentsPerWindow = SegmentsPerWindow,
            QueueLimit = int.MaxValue,
            QueueProcessingOrder = QueueProcessingOrder.OldestFirst,
            AutoReplenishment = true,
        });
    }

    /// <summary>Waits until the request may go; a wait goes to the log, so a slow review shows why.</summary>
    public async ValueTask<RateLimitLease> AcquireAsync(CancellationToken cancellationToken)
    {
        var lease = _limiter.AttemptAcquire();
        if (lease.IsAcquired)
        {
            return lease;
        }

        lease.Dispose();
        var waitStarted = _timeProvider.GetTimestamp();
        lease = await _limiter.AcquireAsync(permitCount: 1, cancellationToken);
        var waitedSeconds = _timeProvider.GetElapsedTime(waitStarted).TotalSeconds;
        LogWaited(_logger, waitedSeconds, _permitLimit);
        return lease;
    }

    public void Dispose() => _limiter.Dispose();

    [LoggerMessage(
        Level = LogLevel.Information,
        Message = "A model request waited {Seconds:0.#} s for its turn: the model's service allows "
            + "{PermitLimit} requests a minute (Agent:RequestsPerMinute).")]
    private static partial void LogWaited(ILogger logger, double seconds, int permitLimit);
}
