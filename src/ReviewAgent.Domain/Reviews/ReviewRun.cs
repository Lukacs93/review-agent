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

    public void Finish(string summary, DateTimeOffset now)
    {
        EnsureStatus(ReviewStatus.Running, ReviewRunErrors.FinishWhenNotRunning);
        EnsureNotBlank(summary, ReviewRunErrors.SummaryRequired);

        Raise(new ReviewFinished(Id, now, summary.Trim()));
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
    /// Records that one approved comment has been published. Calling it again for the same comment does nothing,
    /// so a publish that is retried after a failure never posts a comment twice.
    /// </summary>
    public void MarkCommentPublished(CommentId commentId, DateTimeOffset now)
    {
        EnsureReadyToPublish();

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
        EnsureReadyToPublish();

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

            case ReviewFinished finished:
                Summary = finished.Summary;
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
        EnsureStatus(ReviewStatus.AwaitingApproval, ReviewRunErrors.DecideWhenNotFinished);
        _ = FindComment(commentId);

        if (_comments.Any(comment => comment.IsPublished))
        {
            throw new DomainException(ReviewRunErrors.DecisionsLocked);
        }
    }

    private void EnsureReadyToPublish()
    {
        EnsureStatus(ReviewStatus.AwaitingApproval, ReviewRunErrors.PublishWhenNotFinished);

        if (_comments.Any(comment => !comment.IsDecided))
        {
            throw new DomainException(ReviewRunErrors.UndecidedComments);
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
