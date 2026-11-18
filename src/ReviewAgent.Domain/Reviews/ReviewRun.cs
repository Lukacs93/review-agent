using ReviewAgent.Domain.Reviews.Errors;
using ReviewAgent.Domain.Reviews.Events;
using ReviewAgent.Domain.Rules;

namespace ReviewAgent.Domain.Reviews;

/// <summary>
/// One run of the review agent on a pull request, from the first tool call to publishing.
/// Event-sourced: every change is recorded as an event, and the state is rebuilt by replaying them.
/// </summary>
public sealed class ReviewRun
{
    private readonly List<ReviewComment> _comments = [];
    private readonly List<ReviewEvent> _uncommittedEvents = [];
    private bool _publishingRequested;

    private ReviewRun()
    {
    }

    public ReviewRunId Id { get; private set; }

    public PullRequestRef PullRequest { get; private set; } = null!;

    public ReviewStatus Status { get; private set; }

    public DateTimeOffset StartedAt { get; private set; }

    public int ToolCallCount { get; private set; }

    public string? Summary { get; private set; }

    public string? FailureReason { get; private set; }

    /// <summary>
    /// The pull request's latest commit when the agent read it; <see langword="null"/> until the review has finished,
    /// or when the source does not tell it.
    /// </summary>
    public string? ReviewedCommitSha { get; private set; }

    /// <summary>Why the last publishing failed; <see langword="null"/> if it has not failed.</summary>
    public string? PublishingFailureReason { get; private set; }

    /// <summary>The number of events applied so far; used to detect concurrent changes when saving.</summary>
    public int Version { get; private set; }

    public IReadOnlyList<ReviewComment> Comments => _comments;

    /// <summary>Approved or edited comments that have not been published yet.</summary>
    public IReadOnlyList<ReviewComment> CommentsToPublish =>
        _comments.Where(comment => comment.ShouldBePublished && !comment.IsPublished).ToList();

    /// <summary>Events raised since the run was loaded or created, waiting to be saved.</summary>
    public IReadOnlyList<ReviewEvent> UncommittedEvents => _uncommittedEvents;

    public static ReviewRun Start(ReviewRunId id, PullRequestRef pullRequest, DateTimeOffset now)
    {
        ArgumentNullException.ThrowIfNull(pullRequest);

        var run = new ReviewRun();
        run.Raise(new ReviewStarted(id, now, pullRequest));
        return run;
    }

    /// <summary>Rebuilds a run by replaying its stored events in order.</summary>
    public static ReviewRun FromHistory(IEnumerable<ReviewEvent> history)
    {
        ArgumentNullException.ThrowIfNull(history);

        var run = new ReviewRun();
        foreach (var reviewEvent in history)
        {
            run.Apply(reviewEvent);
        }

        if (run.Version == 0)
        {
            throw new DomainException(ReviewRunErrors.EmptyHistory);
        }

        return run;
    }

    public void RecordToolCall(string toolName, string? arguments, DateTimeOffset now)
    {
        EnsureStatus(ReviewStatus.Running, ReviewRunErrors.ToolCallWhenNotRunning);
        EnsureNotBlank(toolName, ReviewRunErrors.ToolNameRequired);

        Raise(new ToolCalled(Id, now, toolName.Trim(), arguments ?? string.Empty));
    }

    public CommentId ProposeComment(
        CodeLocation location,
        RuleId rule,
        Severity severity,
        string body,
        string? suggestedFix,
        DateTimeOffset now)
    {
        ArgumentNullException.ThrowIfNull(location);
        ArgumentNullException.ThrowIfNull(rule);
        EnsureStatus(ReviewStatus.Running, ReviewRunErrors.ProposeWhenNotRunning);
        EnsureNotBlank(body, ReviewRunErrors.CommentBodyRequired);

        var commentId = CommentId.New();
        var fix = string.IsNullOrWhiteSpace(suggestedFix) ? null : suggestedFix.Trim();
        Raise(new CommentProposed(Id, now, commentId, location, rule, severity, body.Trim(), fix));
        return commentId;
    }

    /// <summary>
    /// Rejects a proposed comment, with the reason. The verifier agent does this before the review finishes,
    /// so the developer only gets the comments it kept.
    /// </summary>
    public void RejectComment(CommentId commentId, string reason, DateTimeOffset now)
    {
        EnsureStatus(ReviewStatus.Running, ReviewRunErrors.RejectWhenNotRunning);
        EnsureNotBlank(reason, ReviewRunErrors.RejectionReasonRequired);

        if (FindComment(commentId).IsRejected)
        {
            throw new DomainException(ReviewRunErrors.CommentAlreadyRejected(commentId));
        }

        Raise(new CommentRejected(Id, now, commentId, reason.Trim()));
    }

    public void Finish(string summary, string? reviewedCommitSha, DateTimeOffset now)
    {
        EnsureStatus(ReviewStatus.Running, ReviewRunErrors.FinishWhenNotRunning);
        EnsureNotBlank(summary, ReviewRunErrors.SummaryRequired);

        var commitSha = string.IsNullOrWhiteSpace(reviewedCommitSha) ? null : reviewedCommitSha.Trim();
        Raise(new ReviewFinished(Id, now, summary.Trim(), commitSha));
    }

    public void Fail(string reason, DateTimeOffset now)
    {
        EnsureStatus(ReviewStatus.Running, ReviewRunErrors.FailWhenNotRunning);
        EnsureNotBlank(reason, ReviewRunErrors.FailureReasonRequired);

        Raise(new ReviewFailed(Id, now, reason.Trim()));
    }

    public void ApproveComment(CommentId commentId, DateTimeOffset now)
    {
        EnsureCanDecide(commentId);

        Raise(new CommentApproved(Id, now, commentId));
    }

    public void EditComment(CommentId commentId, string body, DateTimeOffset now)
    {
        EnsureCanDecide(commentId);
        EnsureNotBlank(body, ReviewRunErrors.EditedBodyRequired);

        Raise(new CommentEdited(Id, now, commentId, body.Trim()));
    }

    public void DiscardComment(CommentId commentId, DateTimeOffset now)
    {
        EnsureCanDecide(commentId);

        Raise(new CommentDiscarded(Id, now, commentId));
    }

    /// <summary>
    /// A developer asks for the approved comments to be published. From here on the decisions cannot change, so
    /// what is published is what was approved, even if publishing has to be asked for again after a failure.
    /// </summary>
    public void RequestPublishing(DateTimeOffset now)
    {
        EnsureStatus(ReviewStatus.AwaitingApproval, ReviewRunErrors.PublishWhenNotFinished);

        if (_comments.Any(comment => !comment.IsDecided))
        {
            throw new DomainException(ReviewRunErrors.UndecidedComments);
        }

        Raise(new PublishingRequested(Id, now));
    }

    /// <summary>Publishing failed; the run waits for the developer again, with the reason.</summary>
    public void FailPublishing(string reason, DateTimeOffset now)
    {
        EnsureStatus(ReviewStatus.Publishing, ReviewRunErrors.NotPublishing);
        EnsureNotBlank(reason, ReviewRunErrors.PublishingFailureReasonRequired);

        Raise(new PublishingFailed(Id, now, reason.Trim()));
    }

    /// <summary>
    /// Records that one approved comment has been published. Calling it again for the same comment does nothing.
    /// </summary>
    public void MarkCommentPublished(CommentId commentId, DateTimeOffset now)
    {
        EnsureStatus(ReviewStatus.Publishing, ReviewRunErrors.NotPublishing);

        var comment = FindComment(commentId);
        if (!comment.ShouldBePublished)
        {
            throw new DomainException(ReviewRunErrors.CommentNotApproved(commentId));
        }

        if (comment.IsPublished)
        {
            return;
        }

        Raise(new CommentPublished(Id, now, commentId));
    }

    /// <summary>Completes publishing once every approved comment has been published.</summary>
    public void CompletePublishing(DateTimeOffset now)
    {
        EnsureStatus(ReviewStatus.Publishing, ReviewRunErrors.NotPublishing);

        if (CommentsToPublish.Count > 0)
        {
            throw new DomainException(ReviewRunErrors.UnpublishedComments);
        }

        Raise(new ReviewPublished(Id, now));
    }

    /// <summary>Called by the repository after the uncommitted events have been saved.</summary>
    public void ClearUncommittedEvents() => _uncommittedEvents.Clear();

    private void Raise(ReviewEvent reviewEvent)
    {
        Apply(reviewEvent);
        _uncommittedEvents.Add(reviewEvent);
    }

    private void Apply(ReviewEvent reviewEvent)
    {
        var isFirstEvent = Version == 0;
        if (isFirstEvent != (reviewEvent is ReviewStarted))
        {
            throw new DomainException(ReviewRunErrors.InvalidFirstEvent);
        }

        switch (reviewEvent)
        {
            case ReviewStarted started:
                Id = started.RunId;
                PullRequest = started.PullRequest;
                StartedAt = started.OccurredAt;
                Status = ReviewStatus.Running;
                break;

            case ToolCalled:
                ToolCallCount++;
                break;

            case CommentProposed proposed:
                _comments.Add(new ReviewComment(
                    proposed.CommentId,
                    proposed.Location,
                    proposed.Rule,
                    proposed.Severity,
                    proposed.Body,
                    proposed.SuggestedFix));
                break;

            case CommentRejected rejected:
                FindComment(rejected.CommentId).Reject(rejected.Reason);
                break;

            case ReviewFinished finished:
                Summary = finished.Summary;
                ReviewedCommitSha = finished.ReviewedCommitSha;
                Status = ReviewStatus.AwaitingApproval;
                break;

            case ReviewFailed failed:
                FailureReason = failed.Reason;
                Status = ReviewStatus.Failed;
                break;

            case CommentApproved approved:
                FindComment(approved.CommentId).Approve();
                break;

            case CommentEdited edited:
                FindComment(edited.CommentId).Edit(edited.Body);
                break;

            case CommentDiscarded discarded:
                FindComment(discarded.CommentId).Discard();
                break;

            case PublishingRequested:
                _publishingRequested = true;
                PublishingFailureReason = null;
                Status = ReviewStatus.Publishing;
                break;

            case PublishingFailed publishingFailed:
                PublishingFailureReason = publishingFailed.Reason;
                Status = ReviewStatus.AwaitingApproval;
                break;

            case CommentPublished published:
                FindComment(published.CommentId).MarkPublished();
                break;

            case ReviewPublished:
                Status = ReviewStatus.Published;
                break;

            default:
                throw new DomainException(ReviewRunErrors.UnknownEvent(reviewEvent.GetType().Name));
        }

        Version++;
    }

    private void EnsureCanDecide(CommentId commentId)
    {
        // Checked before the status: after a failed publishing the status is AwaitingApproval again, but the
        // decisions stay locked.
        if (_publishingRequested)
        {
            throw new DomainException(ReviewRunErrors.DecisionsLocked);
        }

        EnsureStatus(ReviewStatus.AwaitingApproval, ReviewRunErrors.DecideWhenNotFinished);

        if (FindComment(commentId).IsRejected)
        {
            throw new DomainException(ReviewRunErrors.RejectedCommentCannotBeDecided(commentId));
        }
    }

    private void EnsureStatus(ReviewStatus expected, string message)
    {
        if (Status != expected)
        {
            throw new DomainException(message);
        }
    }

    private ReviewComment FindComment(CommentId commentId) =>
        _comments.Find(comment => comment.Id == commentId)
        ?? throw new DomainException(ReviewRunErrors.CommentNotFound(commentId));

    private static void EnsureNotBlank(string value, string message)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            throw new DomainException(message);
        }
    }
}
