using ReviewAgent.Domain.Rules;

namespace ReviewAgent.Domain.Reviews.Events;

/// <summary>Something that happened to a review run. The run's state is rebuilt from these events.</summary>
public abstract record ReviewEvent(ReviewRunId RunId, DateTimeOffset OccurredAt);

/// <summary>A review run started on a pull request.</summary>
public sealed record ReviewStarted(ReviewRunId RunId, DateTimeOffset OccurredAt, PullRequestRef PullRequest)
    : ReviewEvent(RunId, OccurredAt);

/// <summary>The agent called a tool.</summary>
public sealed record ToolCalled(ReviewRunId RunId, DateTimeOffset OccurredAt, string ToolName, string Arguments)
    : ReviewEvent(RunId, OccurredAt);

/// <summary>The agent proposed a review comment.</summary>
public sealed record CommentProposed(
    ReviewRunId RunId,
    DateTimeOffset OccurredAt,
    CommentId CommentId,
    CodeLocation Location,
    RuleId Rule,
    Severity Severity,
    string Body,
    string? SuggestedFix)
    : ReviewEvent(RunId, OccurredAt);

/// <summary>The verifier agent rejected a proposed comment, so it will not reach the developer.</summary>
public sealed record CommentRejected(ReviewRunId RunId, DateTimeOffset OccurredAt, CommentId CommentId, string Reason)
    : ReviewEvent(RunId, OccurredAt);

/// <summary>
/// The agent finished the review. <paramref name="ReviewedCommitSha"/> is the pull request's latest commit when the
/// agent read it, so the comments can be published on the code they were written for; <see langword="null"/> when
/// the source does not tell it.
/// </summary>
public sealed record ReviewFinished(
    ReviewRunId RunId,
    DateTimeOffset OccurredAt,
    string Summary,
    string? ReviewedCommitSha)
    : ReviewEvent(RunId, OccurredAt);

/// <summary>The run stopped with an error.</summary>
public sealed record ReviewFailed(ReviewRunId RunId, DateTimeOffset OccurredAt, string Reason)
    : ReviewEvent(RunId, OccurredAt);

/// <summary>A developer approved a comment as proposed.</summary>
public sealed record CommentApproved(ReviewRunId RunId, DateTimeOffset OccurredAt, CommentId CommentId)
    : ReviewEvent(RunId, OccurredAt);

/// <summary>A developer approved a comment with a rewritten body.</summary>
public sealed record CommentEdited(ReviewRunId RunId, DateTimeOffset OccurredAt, CommentId CommentId, string Body)
    : ReviewEvent(RunId, OccurredAt);

/// <summary>A developer discarded a comment.</summary>
public sealed record CommentDiscarded(ReviewRunId RunId, DateTimeOffset OccurredAt, CommentId CommentId)
    : ReviewEvent(RunId, OccurredAt);

/// <summary>An approved comment was published. Recorded per comment so a retried publish skips it.</summary>
public sealed record CommentPublished(ReviewRunId RunId, DateTimeOffset OccurredAt, CommentId CommentId)
    : ReviewEvent(RunId, OccurredAt);

/// <summary>Every approved comment has been published.</summary>
public sealed record ReviewPublished(ReviewRunId RunId, DateTimeOffset OccurredAt)
    : ReviewEvent(RunId, OccurredAt);
