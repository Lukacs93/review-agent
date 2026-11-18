namespace ReviewAgent.Application.Reviews.Publishing;

/// <summary>
/// Publishes a review on its pull request: the summary and the approved comments together, in one step, so a review
/// is either fully published or not at all.
/// </summary>
public interface IReviewPublisher
{
    /// <summary>
    /// Whether the review of this run is already on the pull request. Asked before publishing, so a publishing that
    /// stopped after the review went out, but before it was recorded, is not repeated.
    /// </summary>
    Task<bool> IsPublishedAsync(ReviewPublication publication, CancellationToken cancellationToken);

    Task PublishAsync(ReviewPublication publication, CancellationToken cancellationToken);
}
