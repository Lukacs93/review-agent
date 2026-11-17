using ReviewAgent.Domain.Reviews;

namespace ReviewAgent.Application.Reviews.Queries;

/// <summary>
/// A review run as plain data. Callers get values only, so they cannot call the run's methods
/// and change it outside a command. Every value is set by name, so values of the same type cannot be swapped.
/// </summary>
public sealed record ReviewRunDetails
{
    public required Guid Id { get; init; }

    public required string RepositoryOwner { get; init; }

    public required string RepositoryName { get; init; }

    public required int PullRequestNumber { get; init; }

    public required ReviewStatus Status { get; init; }

    public required DateTimeOffset StartedAt { get; init; }

    public required int ToolCallCount { get; init; }

    /// <summary>The agent's summary; <see langword="null"/> until the review has finished.</summary>
    public string? Summary { get; init; }

    /// <summary>Why the run stopped; <see langword="null"/> unless it failed.</summary>
    public string? FailureReason { get; init; }

    public required IReadOnlyList<ReviewCommentDetails> Comments { get; init; }

    public static ReviewRunDetails From(ReviewRun run)
    {
        ArgumentNullException.ThrowIfNull(run);

        return new ReviewRunDetails
        {
            Id = run.Id.Value,
            RepositoryOwner = run.PullRequest.Owner,
            RepositoryName = run.PullRequest.Repository,
            PullRequestNumber = run.PullRequest.Number,
            Status = run.Status,
            StartedAt = run.StartedAt,
            ToolCallCount = run.ToolCallCount,
            Summary = run.Summary,
            FailureReason = run.FailureReason,
            Comments = run.Comments.Select(ReviewCommentDetails.From).ToList(),
        };
    }
}
