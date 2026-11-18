using ReviewAgent.Application.Reviews.Publishing;

namespace ReviewAgent.Application.Tests.Fakes;

/// <summary>
/// Stands in for GitHub: keeps the published reviews in memory, and can fail before or after publishing, as a lost
/// connection or a lost answer would.
/// </summary>
internal sealed class InMemoryReviewPublisher : IReviewPublisher
{
    public List<ReviewPublication> Published { get; } = [];

    /// <summary>Thrown by the next publish before the review is stored.</summary>
    public Exception? FailBeforePublishing { get; set; }

    /// <summary>Thrown by the next publish after the review is stored: it went out, but the answer was lost.</summary>
    public Exception? FailAfterPublishing { get; set; }

    public Task<bool> IsPublishedAsync(ReviewPublication publication, CancellationToken cancellationToken) =>
        Task.FromResult(Published.Exists(published => published.RunId == publication.RunId));

    public Task PublishAsync(ReviewPublication publication, CancellationToken cancellationToken)
    {
        var failureBefore = FailBeforePublishing;
        if (failureBefore is not null)
        {
            FailBeforePublishing = null;
            throw failureBefore;
        }

        Published.Add(publication);

        var failureAfter = FailAfterPublishing;
        if (failureAfter is not null)
        {
            FailAfterPublishing = null;
            throw failureAfter;
        }

        return Task.CompletedTask;
    }
}
