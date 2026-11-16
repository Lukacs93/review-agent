using ReviewAgent.Domain.Reviews;

namespace ReviewAgent.Application.Reviews.Queries;

/// <summary>
/// A review run as plain data. Callers get values only, so they cannot call the run's methods
/// and change it outside a command.
/// </summary>
public sealed record ReviewRunDetails(
    Guid Id,
    string RepositoryOwner,
    string RepositoryName,
    int PullRequestNumber,
    ReviewStatus Status,
    DateTimeOffset StartedAt,
    int ToolCallCount,
    string? Summary,
    string? FailureReason,
    IReadOnlyList<ReviewCommentDetails> Comments)
{
    public static ReviewRunDetails From(ReviewRun run)
    {
        ArgumentNullException.ThrowIfNull(run);

        return new ReviewRunDetails(
            run.Id.Value,
            run.PullRequest.Owner,
            run.PullRequest.Repository,
            run.PullRequest.Number,
            run.Status,
            run.StartedAt,
            run.ToolCallCount,
            run.Summary,
            run.FailureReason,
            run.Comments.Select(ReviewCommentDetails.From).ToList());
    }
}
