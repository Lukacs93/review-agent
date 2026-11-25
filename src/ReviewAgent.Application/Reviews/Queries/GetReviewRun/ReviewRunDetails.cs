using System.Globalization;
using ReviewAgent.Domain.Reviews;

namespace ReviewAgent.Application.Reviews.Queries.GetReviewRun;

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

    /// <summary>How many times the agents called the model.</summary>
    public required int ModelCallCount { get; init; }

    /// <summary>The time the model calls took together.</summary>
    public required TimeSpan ModelTime { get; init; }

    /// <summary>
    /// The input tokens of the model calls together; <see langword="null"/> when the model's API did not report
    /// them for every call.
    /// </summary>
    public long? InputTokenCount { get; init; }

    /// <summary>
    /// The output tokens of the model calls together; <see langword="null"/> when the model's API did not report
    /// them for every call.
    /// </summary>
    public long? OutputTokenCount { get; init; }

    /// <summary>The agent's summary; <see langword="null"/> until the review has finished.</summary>
    public string? Summary { get; init; }

    /// <summary>
    /// How many comments the verifier agent kept and rejected; <see langword="null"/> if it did not run.
    /// </summary>
    // Counted from the comments, not taken from the model's own sentence, which was not always true.
    public string? VerifierSummary { get; init; }

    /// <summary>Why the run stopped; <see langword="null"/> unless it failed.</summary>
    public string? FailureReason { get; init; }

    /// <summary>
    /// What the failure came from: the agent itself, the model's service, or other infrastructure;
    /// <see langword="null"/> unless the run failed, and for a failure stored without a kind.
    /// </summary>
    public ReviewFailureKind? FailureKind { get; init; }

    /// <summary>Why the verifier agent could not finish; <see langword="null"/> unless it failed.</summary>
    public string? VerificationFailureReason { get; init; }

    /// <summary>What the verifier's failure came from; <see langword="null"/> unless it failed.</summary>
    public ReviewFailureKind? VerificationFailureKind { get; init; }

    /// <summary>Why the last publishing failed; <see langword="null"/> unless it failed.</summary>
    public string? PublishingFailureReason { get; init; }

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
            ModelCallCount = run.ModelCallCount,
            ModelTime = run.ModelTime,
            InputTokenCount = run.InputTokenCount,
            OutputTokenCount = run.OutputTokenCount,
            Summary = run.Summary,
            VerifierSummary = run.IsVerified ? VerifierSummaryOf(run.Comments) : null,
            FailureReason = run.FailureReason,
            FailureKind = run.FailureKind,
            VerificationFailureReason = run.VerificationFailureReason,
            VerificationFailureKind = run.VerificationFailureKind,
            PublishingFailureReason = run.PublishingFailureReason,
            Comments = run.Comments.Select(ReviewCommentDetails.From).ToList(),
        };
    }

    private static string VerifierSummaryOf(IReadOnlyList<ReviewComment> comments)
    {
        var rejected = comments.Count(comment => comment.IsRejected);
        var kept = comments.Count - rejected;
        return string.Create(CultureInfo.InvariantCulture, $"Kept: {kept}, rejected: {rejected}.");
    }
}
