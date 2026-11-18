using ReviewAgent.Domain.Reviews;

namespace ReviewAgent.Application.Reviews.Publishing;

/// <summary>
/// What is published from a review run: the summary and the approved comments, on the commit that was reviewed.
/// The run's ID goes with it, so the published review can be recognised as this run's.
/// </summary>
public sealed record ReviewPublication(
    Guid RunId,
    PullRequestRef PullRequest,
    string? ReviewedCommitSha,
    string Summary,
    IReadOnlyList<CommentToPublish> Comments)
{
    public static ReviewPublication From(ReviewRun run)
    {
        ArgumentNullException.ThrowIfNull(run);

        return new ReviewPublication(
            run.Id.Value,
            run.PullRequest,
            run.ReviewedCommitSha,
            run.Summary ?? string.Empty,
            run.CommentsToPublish.Select(CommentToPublish.From).ToList());
    }
}
