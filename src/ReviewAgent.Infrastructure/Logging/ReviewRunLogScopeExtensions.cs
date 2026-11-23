using Microsoft.Extensions.Logging;

namespace ReviewAgent.Infrastructure.Logging;

/// <summary>The log scope of a review run, by which <see cref="RunLogProvider"/> keeps the run's lines.</summary>
public static class ReviewRunLogScopeExtensions
{
    // Defined once, like the log messages, so the template is not parsed on every run (CA1848).
    private static readonly Func<ILogger, Guid, IDisposable?> _reviewRunScope =
        LoggerMessage.DefineScope<Guid>("Review run {" + RunLogProvider.RunIdKey + "}");

    /// <summary>
    /// Opens the log scope of a review run: the lines logged until it is disposed go into the run's log.
    /// </summary>
    public static IDisposable? BeginReviewRunScope(this ILogger logger, Guid runId) => _reviewRunScope(logger, runId);
}
