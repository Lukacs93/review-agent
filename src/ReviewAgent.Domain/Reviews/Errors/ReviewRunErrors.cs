namespace ReviewAgent.Domain.Reviews.Errors;

/// <summary>The messages of the <see cref="DomainException"/>s thrown by <see cref="ReviewRun"/>.</summary>
public static class ReviewRunErrors
{
    // Lifecycle
    public const string EmptyHistory = "A review run needs at least one event.";
    public const string InvalidFirstEvent = "A review run must start with exactly one ReviewStarted event.";
    public const string ToolCallWhenNotRunning = "Tool calls can only be recorded while the review is running.";
    public const string ProposeWhenNotRunning = "Comments can only be proposed while the review is running.";
    public const string FinishWhenNotRunning = "Only a running review can be finished.";
    public const string FailWhenNotRunning = "Only a running review can fail.";
    public const string RejectWhenNotRunning = "Comments can only be rejected while the review is running.";
    public const string VerifyWhenNotRunning = "Verification can only be recorded while the review is running.";

    // Required values
    public const string ToolNameRequired = "Tool name is required.";
    public const string CommentBodyRequired = "Comment body is required.";
    public const string SummaryRequired = "Summary is required.";
    public const string FailureReasonRequired = "Failure reason is required.";
    public const string EditedBodyRequired = "Edited comment body is required.";
    public const string RejectionReasonRequired = "Rejection reason is required.";

    // Decisions
    public const string DecideWhenNotFinished = "Comments can only be decided after the review has finished.";
    public const string DecisionsLocked = "Decisions cannot change once publishing has been asked for.";

    // Publishing
    public const string PublishWhenNotFinished = "Only a finished review can be published.";
    public const string UndecidedComments = "Every comment needs a decision before publishing.";
    public const string NotPublishing = "Publishing can only be recorded while the review is being published.";
    public const string PublishingFailureReasonRequired = "Publishing failure reason is required.";
    public const string UnpublishedComments =
        "Every approved comment must be published before the review is completed.";

    public static string CommentNotFound(CommentId commentId) =>
        $"Comment {commentId} was not found in this review.";

    public static string CommentNotApproved(CommentId commentId) =>
        $"Comment {commentId} was not approved, so it cannot be published.";

    public static string CommentAlreadyRejected(CommentId commentId) =>
        $"Comment {commentId} has already been rejected.";

    public static string RejectedCommentCannotBeDecided(CommentId commentId) =>
        $"Comment {commentId} was rejected by the verifier, so it needs no decision.";

    public static string UnknownEvent(string eventName) => $"Unknown review event: {eventName}.";
}
