using ReviewAgent.Domain.Reviews;
using ReviewAgent.Domain.Reviews.Errors;
using ReviewAgent.Domain.Reviews.Events;
using ReviewAgent.Domain.Rules;

namespace ReviewAgent.Domain.Tests.Reviews;

public sealed class ReviewRunTests
{
    private const string Summary = "Two rule violations found.";
    private const string RejectionReason = "The query does not change state, so CQRS-01 is not broken.";
    private const string CommitSha = "9fceb02d0ae598e95dc970b74767f19372d61af8";
    private const string PublishingFailure = "GitHub answered 422: the line must be part of the diff.";

    private static DateTimeOffset Now => new(2026, 11, 14, 10, 0, 0, TimeSpan.Zero);

    private static PullRequestRef PullRequest => new("test", "demo", 1);

    [Fact]
    public void Started_run_is_running_and_raises_ReviewStarted()
    {
        var runId = ReviewRunId.New();

        var run = ReviewRun.Start(runId, PullRequest, Now);

        Assert.Equal(runId, run.Id);
        Assert.Equal(ReviewStatus.Running, run.Status);
        Assert.Equal(PullRequest, run.PullRequest);
        var started = Assert.IsType<ReviewStarted>(Assert.Single(run.UncommittedEvents));
        Assert.Equal(runId, started.RunId);
    }

    [Fact]
    public void Tool_calls_are_counted()
    {
        var run = StartedRun();

        run.RecordToolCall("get_diff", "src/Order.cs", Now);
        run.RecordToolCall("get_team_rules", null, Now);

        Assert.Equal(2, run.ToolCallCount);
    }

    [Fact]
    public void Proposed_comment_is_pending()
    {
        var run = StartedRun();

        var commentId = ProposeComment(run);

        var comment = Assert.Single(run.Comments);
        Assert.Equal(commentId, comment.Id);
        Assert.Equal(CommentDecision.Pending, comment.Decision);
        Assert.False(comment.IsPublished);
    }

    [Fact]
    public void Comment_cannot_be_proposed_after_the_review_finished()
    {
        var run = StartedRun();
        run.Finish(Summary, CommitSha, Now);

        AssertFails(ReviewRunErrors.ProposeWhenNotRunning, () => ProposeComment(run));
    }

    [Fact]
    public void Finished_review_awaits_approval()
    {
        var run = StartedRun();

        run.Finish(Summary, CommitSha, Now);

        Assert.Equal(ReviewStatus.AwaitingApproval, run.Status);
        Assert.Equal(Summary, run.Summary);
        Assert.Equal(CommitSha, run.ReviewedCommitSha);
    }

    [Theory]
    [InlineData(null)]
    [InlineData(" ")]
    public void Review_without_a_known_commit_can_finish(string? reviewedCommitSha)
    {
        var run = StartedRun();

        run.Finish(Summary, reviewedCommitSha, Now);

        Assert.Equal(ReviewStatus.AwaitingApproval, run.Status);
        Assert.Null(run.ReviewedCommitSha);
    }

    [Fact]
    public void Failed_review_keeps_the_reason()
    {
        var run = StartedRun();

        run.Fail("Step limit reached.", Now);

        Assert.Equal(ReviewStatus.Failed, run.Status);
        Assert.Equal("Step limit reached.", run.FailureReason);
    }

    [Fact]
    public void Finished_review_cannot_fail()
    {
        var run = StartedRun();
        run.Finish(Summary, CommitSha, Now);

        AssertFails(ReviewRunErrors.FailWhenNotRunning, () => run.Fail("Too late.", Now));
    }

    [Fact]
    public void Comment_cannot_be_decided_while_the_review_is_running()
    {
        var run = StartedRun();
        var commentId = ProposeComment(run);

        AssertFails(ReviewRunErrors.DecideWhenNotFinished, () => run.ApproveComment(commentId, Now));
    }

    [Fact]
    public void Unknown_comment_cannot_be_decided()
    {
        var (run, _) = FinishedRunWithComments(1);

        var unknownId = CommentId.New();

        AssertFails(ReviewRunErrors.CommentNotFound(unknownId), () => run.ApproveComment(unknownId, Now));
    }

    [Fact]
    public void Edited_comment_keeps_the_proposed_text()
    {
        var (run, commentIds) = FinishedRunWithComments(1);

        run.EditComment(commentIds[0], "Move the discount calculation into Order.", Now);

        var comment = Assert.Single(run.Comments);
        Assert.Equal(CommentDecision.Edited, comment.Decision);
        Assert.Equal("Move the discount calculation into Order.", comment.Body);
        Assert.Equal("The controller uses OrdersDbContext directly.", comment.ProposedBody);
    }

    [Fact]
    public void Edit_with_empty_body_is_rejected()
    {
        var (run, commentIds) = FinishedRunWithComments(1);

        AssertFails(ReviewRunErrors.EditedBodyRequired, () => run.EditComment(commentIds[0], " ", Now));
    }

    [Fact]
    public void Requested_publishing_is_in_progress()
    {
        var (run, commentIds) = FinishedRunWithComments(1);
        run.ApproveComment(commentIds[0], Now);

        run.RequestPublishing(Now);

        Assert.Equal(ReviewStatus.Publishing, run.Status);
        Assert.IsType<PublishingRequested>(LastEventOf(run));
    }

    [Fact]
    public void Publishing_requires_a_decision_on_every_comment()
    {
        var (run, commentIds) = FinishedRunWithComments(2);
        run.ApproveComment(commentIds[0], Now);

        AssertFails(ReviewRunErrors.UndecidedComments, () => run.RequestPublishing(Now));
    }

    [Fact]
    public void Running_review_cannot_be_published()
    {
        var run = StartedRun();

        AssertFails(ReviewRunErrors.PublishWhenNotFinished, () => run.RequestPublishing(Now));
    }

    [Fact]
    public void Publishing_cannot_be_recorded_before_it_is_requested()
    {
        var (run, commentIds) = FinishedRunWithComments(1);
        run.ApproveComment(commentIds[0], Now);

        AssertFails(ReviewRunErrors.NotPublishing, () => run.MarkCommentPublished(commentIds[0], Now));
        AssertFails(ReviewRunErrors.NotPublishing, () => run.CompletePublishing(Now));
        AssertFails(ReviewRunErrors.NotPublishing, () => run.FailPublishing(PublishingFailure, Now));
    }

    [Fact]
    public void Only_approved_comments_are_published()
    {
        var (run, commentIds) = FinishedRunWithComments(2);
        run.ApproveComment(commentIds[0], Now);
        run.DiscardComment(commentIds[1], Now);
        run.RequestPublishing(Now);

        Assert.Equal(commentIds[0], Assert.Single(run.CommentsToPublish).Id);
        AssertFails(
            ReviewRunErrors.CommentNotApproved(commentIds[1]),
            () => run.MarkCommentPublished(commentIds[1], Now));
    }

    [Fact]
    public void Publishing_the_same_comment_twice_raises_one_event()
    {
        var (run, commentIds) = FinishedRunWithComments(1);
        run.ApproveComment(commentIds[0], Now);
        run.RequestPublishing(Now);
        run.ClearUncommittedEvents();

        run.MarkCommentPublished(commentIds[0], Now);
        run.MarkCommentPublished(commentIds[0], Now);

        Assert.IsType<CommentPublished>(Assert.Single(run.UncommittedEvents));
    }

    [Fact]
    public void Decisions_cannot_change_once_publishing_is_requested()
    {
        var (run, commentIds) = FinishedRunWithComments(1);
        run.ApproveComment(commentIds[0], Now);
        run.RequestPublishing(Now);

        AssertFails(ReviewRunErrors.DecisionsLocked, () => run.DiscardComment(commentIds[0], Now));
    }

    [Fact]
    public void Failed_publishing_waits_for_the_developer_with_the_reason()
    {
        var (run, commentIds) = FinishedRunWithComments(1);
        run.ApproveComment(commentIds[0], Now);
        run.RequestPublishing(Now);

        run.FailPublishing(PublishingFailure, Now);

        Assert.Equal(ReviewStatus.AwaitingApproval, run.Status);
        Assert.Equal(PublishingFailure, run.PublishingFailureReason);
        Assert.False(Assert.Single(run.Comments).IsPublished);
    }

    [Fact]
    public void Decisions_stay_locked_after_a_failed_publishing()
    {
        var (run, commentIds) = FinishedRunWithComments(1);
        run.ApproveComment(commentIds[0], Now);
        run.RequestPublishing(Now);
        run.FailPublishing(PublishingFailure, Now);

        AssertFails(ReviewRunErrors.DecisionsLocked, () => run.DiscardComment(commentIds[0], Now));
    }

    [Fact]
    public void Publishing_can_be_requested_again_after_it_failed()
    {
        var (run, commentIds) = FinishedRunWithComments(1);
        run.ApproveComment(commentIds[0], Now);
        run.RequestPublishing(Now);
        run.FailPublishing(PublishingFailure, Now);

        run.RequestPublishing(Now);

        Assert.Equal(ReviewStatus.Publishing, run.Status);
        Assert.Null(run.PublishingFailureReason);
    }

    [Fact]
    public void Publishing_failure_needs_a_reason()
    {
        var (run, _) = FinishedRunWithComments(0);
        run.RequestPublishing(Now);

        AssertFails(ReviewRunErrors.PublishingFailureReasonRequired, () => run.FailPublishing(" ", Now));
    }

    [Fact]
    public void Review_is_published_once_every_approved_comment_is_published()
    {
        var (run, commentIds) = FinishedRunWithComments(2);
        run.ApproveComment(commentIds[0], Now);
        run.EditComment(commentIds[1], "Rename Basket to Cart.", Now);
        run.RequestPublishing(Now);

        run.MarkCommentPublished(commentIds[0], Now);
        AssertFails(ReviewRunErrors.UnpublishedComments, () => run.CompletePublishing(Now));

        run.MarkCommentPublished(commentIds[1], Now);
        run.CompletePublishing(Now);

        Assert.Equal(ReviewStatus.Published, run.Status);
    }

    [Fact]
    public void Review_without_comments_can_be_published()
    {
        var run = StartedRun();
        run.Finish("No rule violations found.", CommitSha, Now);
        run.RequestPublishing(Now);

        run.CompletePublishing(Now);

        Assert.Equal(ReviewStatus.Published, run.Status);
    }

    [Fact]
    public void Rejected_comment_keeps_the_reason()
    {
        var run = StartedRun();
        var commentId = ProposeComment(run);

        run.RejectComment(commentId, RejectionReason, Now);

        var comment = Assert.Single(run.Comments);
        Assert.True(comment.IsRejected);
        Assert.Equal(RejectionReason, comment.RejectionReason);
        Assert.IsType<CommentRejected>(LastEventOf(run));
    }

    [Fact]
    public void Rejection_needs_a_reason()
    {
        var run = StartedRun();
        var commentId = ProposeComment(run);

        AssertFails(ReviewRunErrors.RejectionReasonRequired, () => run.RejectComment(commentId, " ", Now));
    }

    [Fact]
    public void Comment_cannot_be_rejected_twice()
    {
        var run = StartedRun();
        var commentId = ProposeComment(run);
        run.RejectComment(commentId, RejectionReason, Now);

        AssertFails(
            ReviewRunErrors.CommentAlreadyRejected(commentId),
            () => run.RejectComment(commentId, RejectionReason, Now));
    }

    [Fact]
    public void Comment_cannot_be_rejected_after_the_review_finished()
    {
        var (run, commentIds) = FinishedRunWithComments(1);

        AssertFails(
            ReviewRunErrors.RejectWhenNotRunning,
            () => run.RejectComment(commentIds[0], RejectionReason, Now));
    }

    [Fact]
    public void Rejected_comment_cannot_be_decided()
    {
        var run = StartedRun();
        var rejected = ProposeComment(run);
        run.RejectComment(rejected, RejectionReason, Now);
        run.Finish(Summary, CommitSha, Now);

        AssertFails(
            ReviewRunErrors.RejectedCommentCannotBeDecided(rejected),
            () => run.ApproveComment(rejected, Now));
    }

    [Fact]
    public void Rejected_comments_do_not_block_publishing()
    {
        var run = StartedRun();
        var rejected = ProposeComment(run);
        var kept = ProposeComment(run);
        run.RejectComment(rejected, RejectionReason, Now);
        run.Finish(Summary, CommitSha, Now);
        run.ApproveComment(kept, Now);
        run.RequestPublishing(Now);

        run.MarkCommentPublished(kept, Now);
        run.CompletePublishing(Now);

        Assert.Equal(ReviewStatus.Published, run.Status);
    }

    [Fact]
    public void Verification_is_recorded()
    {
        var run = StartedRun();

        run.FinishVerification(Now);

        Assert.True(run.IsVerified);
        Assert.Equal(ReviewStatus.Running, run.Status);
        Assert.IsType<VerificationFinished>(LastEventOf(run));
    }

    [Fact]
    public void Run_is_not_verified_until_the_verifier_finishes()
    {
        var run = StartedRun();

        Assert.False(run.IsVerified);
    }

    [Fact]
    public void Verification_cannot_be_recorded_after_the_review_finished()
    {
        var run = StartedRun();
        run.Finish(Summary, CommitSha, Now);

        AssertFails(ReviewRunErrors.VerifyWhenNotRunning, () => run.FinishVerification(Now));
    }

    [Fact]
    public void Verification_is_rebuilt_from_the_history()
    {
        var run = StartedRun();
        run.FinishVerification(Now);

        var rebuilt = ReviewRun.FromHistory(run.UncommittedEvents);

        Assert.True(rebuilt.IsVerified);
    }

    [Fact]
    public void Rejection_is_rebuilt_from_the_history()
    {
        var run = StartedRun();
        var commentId = ProposeComment(run);
        run.RejectComment(commentId, RejectionReason, Now);

        var rebuilt = ReviewRun.FromHistory(run.UncommittedEvents);

        Assert.Equal(RejectionReason, Assert.Single(rebuilt.Comments).RejectionReason);
    }

    [Fact]
    public void Run_rebuilt_from_its_history_has_the_same_state()
    {
        var (original, commentIds) = FinishedRunWithComments(2);
        original.ApproveComment(commentIds[0], Now);
        original.EditComment(commentIds[1], "Rename Basket to Cart.", Now);

        var rebuilt = ReviewRun.FromHistory(original.UncommittedEvents);

        Assert.Equal(original.Id, rebuilt.Id);
        Assert.Equal(original.Status, rebuilt.Status);
        Assert.Equal(original.Version, rebuilt.Version);
        Assert.Equal(original.ToolCallCount, rebuilt.ToolCallCount);
        Assert.Equal(
            original.Comments.Select(c => (c.Id, c.Decision, c.Body)),
            rebuilt.Comments.Select(c => (c.Id, c.Decision, c.Body)));
        Assert.Empty(rebuilt.UncommittedEvents);
    }

    [Fact]
    public void Publishing_state_is_rebuilt_from_the_history()
    {
        var (original, commentIds) = FinishedRunWithComments(1);
        original.ApproveComment(commentIds[0], Now);
        original.RequestPublishing(Now);
        original.FailPublishing(PublishingFailure, Now);

        var rebuilt = ReviewRun.FromHistory(original.UncommittedEvents);

        Assert.Equal(ReviewStatus.AwaitingApproval, rebuilt.Status);
        Assert.Equal(PublishingFailure, rebuilt.PublishingFailureReason);
        Assert.Equal(CommitSha, rebuilt.ReviewedCommitSha);
        AssertFails(ReviewRunErrors.DecisionsLocked, () => rebuilt.DiscardComment(commentIds[0], Now));
    }

    [Fact]
    public void History_must_start_with_ReviewStarted()
    {
        var history = new ReviewEvent[] { new ToolCalled(ReviewRunId.New(), Now, "get_diff", "") };

        AssertFails(ReviewRunErrors.InvalidFirstEvent, () => ReviewRun.FromHistory(history));
    }

    [Fact]
    public void Empty_history_is_rejected()
    {
        AssertFails(ReviewRunErrors.EmptyHistory, () => ReviewRun.FromHistory([]));
    }

    private static void AssertFails(string expectedMessage, Action action)
    {
        var error = Assert.Throws<DomainException>(action);
        Assert.Equal(expectedMessage, error.Message);
    }

    // The event the last call raised: the run's last uncommitted event.
    private static ReviewEvent LastEventOf(ReviewRun run) => run.UncommittedEvents[run.UncommittedEvents.Count - 1];

    private static ReviewRun StartedRun() => ReviewRun.Start(ReviewRunId.New(), PullRequest, Now);

    private static CommentId ProposeComment(ReviewRun run) =>
        run.ProposeComment(
            new CodeLocation("src/Orders.Api/Controllers/OrdersController.cs", 14),
            new RuleId("ARCH-01"),
            Severity.Error,
            "The controller uses OrdersDbContext directly.",
            "Call an application handler instead.",
            Now);

    private static (ReviewRun Run, IReadOnlyList<CommentId> CommentIds) FinishedRunWithComments(int count)
    {
        var run = StartedRun();
        run.RecordToolCall("get_diff", "src/Orders.Api/Controllers/OrdersController.cs", Now);
        var commentIds = Enumerable.Range(0, count).Select(_ => ProposeComment(run)).ToList();
        run.Finish(Summary, CommitSha, Now);
        return (run, commentIds);
    }
}
