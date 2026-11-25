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
    private const string ProposedReplacementCode = "    public decimal DiscountAmount { get; init; }";
    private const string EditedReplacementCode = "    public decimal DiscountAmount { get; private set; }";
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
    public void Model_calls_are_added_up()
    {
        var run = StartedRun();

        run.RecordModelCall("code-reviewer", TimeSpan.FromSeconds(2), 1200, 80, Now);
        run.RecordModelCall("verifier", TimeSpan.FromSeconds(3), 900, 20, Now);

        Assert.Equal(2, run.ModelCallCount);
        Assert.Equal(TimeSpan.FromSeconds(5), run.ModelTime);
        Assert.Equal(2100, run.InputTokenCount);
        Assert.Equal(100, run.OutputTokenCount);
        Assert.IsType<ModelCalled>(LastEventOf(run));
    }

    [Fact]
    public void Run_without_model_calls_has_used_no_tokens()
    {
        var run = StartedRun();

        Assert.Equal(0, run.InputTokenCount);
        Assert.Equal(0, run.OutputTokenCount);
    }

    // A total that silently left out a call would be too low; it is unknown instead, also after later calls.
    [Fact]
    public void Token_total_is_unknown_once_a_call_did_not_report_its_count()
    {
        var run = StartedRun();
        run.RecordModelCall("code-reviewer", TimeSpan.FromSeconds(1), 1200, 80, Now);

        run.RecordModelCall("code-reviewer", TimeSpan.FromSeconds(1), inputTokens: null, outputTokens: 20, Now);
        run.RecordModelCall("verifier", TimeSpan.FromSeconds(1), 300, 10, Now);

        Assert.Equal(3, run.ModelCallCount);
        Assert.Null(run.InputTokenCount);
        Assert.Equal(110, run.OutputTokenCount);
    }

    [Fact]
    public void Model_call_needs_the_agents_name()
    {
        var run = StartedRun();

        AssertFails(
            ReviewRunErrors.AgentNameRequired,
            () => run.RecordModelCall(" ", TimeSpan.FromSeconds(1), 1, 1, Now));
    }

    [Fact]
    public void Model_call_cannot_take_negative_time()
    {
        var run = StartedRun();

        AssertFails(
            ReviewRunErrors.NegativeDuration,
            () => run.RecordModelCall("code-reviewer", TimeSpan.FromSeconds(-1), 1, 1, Now));
    }

    [Theory]
    [InlineData(-1, 1)]
    [InlineData(1, -1)]
    public void Model_call_cannot_have_negative_tokens(long inputTokens, long outputTokens)
    {
        var run = StartedRun();

        AssertFails(
            ReviewRunErrors.NegativeTokens,
            () => run.RecordModelCall("code-reviewer", TimeSpan.FromSeconds(1), inputTokens, outputTokens, Now));
    }

    [Fact]
    public void Model_calls_cannot_be_recorded_after_the_review_finished()
    {
        var run = StartedRun();
        run.Finish(Summary, CommitSha, Now);

        AssertFails(
            ReviewRunErrors.ModelCallWhenNotRunning,
            () => run.RecordModelCall("code-reviewer", TimeSpan.FromSeconds(1), 1, 1, Now));
    }

    [Fact]
    public void Model_calls_are_rebuilt_from_the_history()
    {
        var run = StartedRun();
        run.RecordModelCall("code-reviewer", TimeSpan.FromSeconds(2), 1200, 80, Now);

        var rebuilt = ReviewRun.FromHistory(run.UncommittedEvents);

        Assert.Equal(1, rebuilt.ModelCallCount);
        Assert.Equal(TimeSpan.FromSeconds(2), rebuilt.ModelTime);
        Assert.Equal(1200, rebuilt.InputTokenCount);
        Assert.Equal(80, rebuilt.OutputTokenCount);
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

    // The indentation is part of the code: only the line breaks at the end are dropped, and every line break becomes
    // '\n', so a suggested change does not bring other line endings into the file.
    [Theory]
    [InlineData("    GetOrderHandler getOrder)\n", "    GetOrderHandler getOrder)")]
    [InlineData("    GetOrderHandler getOrder)", "    GetOrderHandler getOrder)")]
    [InlineData("    a,\r\n    b)\r\n", "    a,\n    b)")]
    [InlineData("   ", null)]
    public void Replacement_code_keeps_its_indentation(string replacementCode, string? expected)
    {
        var run = StartedRun();

        run.ProposeComment(
            new CodeLocation("src/Orders.Api/Controllers/OrdersController.cs", 14),
            new RuleId("ARCH-01"),
            Severity.Error,
            "The controller uses OrdersDbContext directly.",
            replacementCode,
            Now);

        Assert.Equal(expected, Assert.Single(run.Comments).ReplacementCode);
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

        run.EditComment(commentIds[0], "Move the discount calculation into Order.", replacementCode: null, Now);

        var comment = Assert.Single(run.Comments);
        Assert.Equal(CommentDecision.Edited, comment.Decision);
        Assert.Equal("Move the discount calculation into Order.", comment.Body);
        Assert.Equal("The controller uses OrdersDbContext directly.", comment.ProposedBody);
    }

    [Fact]
    public void Approved_comment_keeps_the_proposed_replacement_code()
    {
        var (run, commentId) = FinishedRunWithReplacementCode();

        run.ApproveComment(commentId, Now);

        Assert.Equal(ProposedReplacementCode, Assert.Single(run.Comments).ReplacementCode);
    }

    // The developer's code may come from any system: its line breaks become '\n' too.
    [Fact]
    public void Edited_replacement_code_gets_the_same_line_breaks()
    {
        var (run, commentId) = FinishedRunWithReplacementCode();

        run.EditComment(commentId, "Entity properties must not have public setters.", "    a,\r\n    b)", Now);

        Assert.Equal("    a,\n    b)", Assert.Single(run.Comments).ReplacementCode);
    }

    // The finding can be right and the proposed code wrong: the developer corrects or removes the code only, and
    // leaving the code out of an edit keeps the proposed one.
    [Theory]
    [InlineData(EditedReplacementCode, EditedReplacementCode)]
    [InlineData("", null)]
    [InlineData(null, ProposedReplacementCode)]
    public void Edited_replacement_code_decides_what_is_published(string? editedCode, string? publishedCode)
    {
        var (run, commentId) = FinishedRunWithReplacementCode();

        run.EditComment(commentId, "Entity properties must not have public setters.", editedCode, Now);

        var comment = Assert.Single(run.Comments);
        Assert.Equal(publishedCode, comment.ReplacementCode);
        Assert.Equal(ProposedReplacementCode, comment.ProposedReplacementCode);
    }

    [Fact]
    public void Edit_with_empty_body_is_rejected()
    {
        var (run, commentIds) = FinishedRunWithComments(1);

        AssertFails(
            ReviewRunErrors.EditedBodyRequired,
            () => run.EditComment(commentIds[0], " ", replacementCode: null, Now));
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
        run.EditComment(commentIds[1], "Rename Basket to Cart.", replacementCode: null, Now);
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
        original.EditComment(commentIds[1], "Rename Basket to Cart.", replacementCode: null, Now);

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

    private static (ReviewRun Run, CommentId CommentId) FinishedRunWithReplacementCode()
    {
        var run = StartedRun();
        var commentId = run.ProposeComment(
            new CodeLocation("src/Orders.Api/Domain/Order.cs", 13),
            new RuleId("DDD-01"),
            Severity.Error,
            "Entity properties must not have public setters.",
            ProposedReplacementCode,
            Now);
        run.Finish(Summary, CommitSha, Now);
        return (run, commentId);
    }

    private static CommentId ProposeComment(ReviewRun run) =>
        run.ProposeComment(
            new CodeLocation("src/Orders.Api/Controllers/OrdersController.cs", 14),
            new RuleId("ARCH-01"),
            Severity.Error,
            "The controller uses OrdersDbContext directly.",
            replacementCode: null,
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
