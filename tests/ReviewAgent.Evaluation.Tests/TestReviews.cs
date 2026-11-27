using ReviewAgent.Application.Reviews.Queries.GetReviewRun;
using ReviewAgent.Domain.Reviews;
using ReviewAgent.Domain.Rules;
using ReviewAgent.Evaluation.Dataset;

namespace ReviewAgent.Evaluation.Tests;

/// <summary>Comments, findings and reviews for the tests.</summary>
internal static class TestReviews
{
    public const string ControllerFile = "src/Orders.Api/Controllers/OrdersController.cs";
    public const string OrderFile = "src/Orders.Api/Domain/Order.cs";

    public static ExpectedFinding Finding(string rule, string file, bool optional = false, params int[] lines) =>
        new(rule, file, lines.Select(line => new ExpectedLine(line, "code")).ToList(), optional, Reason: null);

    public static ReviewCommentDetails Comment(string rule, string file, int line, string? rejectionReason = null) =>
        new()
        {
            Id = Guid.NewGuid(),
            FilePath = file,
            Line = line,
            RuleId = rule,
            Severity = Severity.Error,
            ProposedBody = "Use a handler instead of the DbContext.",
            Body = "Use a handler instead of the DbContext.",
            Decision = CommentDecision.Pending,
            IsPublished = false,
            RejectionReason = rejectionReason,
        };

    public static ReviewRunDetails Review(
        ReviewStatus status,
        IReadOnlyList<ReviewCommentDetails> comments,
        string? failureReason = null,
        ReviewFailureKind? failureKind = null) =>
        new()
        {
            Id = Guid.NewGuid(),
            RepositoryOwner = "evaluation",
            RepositoryName = "tasks",
            PullRequestNumber = 1,
            Status = status,
            StartedAt = new DateTimeOffset(2026, 11, 25, 10, 0, 0, TimeSpan.Zero),
            ToolCallCount = 3,
            ModelCallCount = 4,
            ModelTime = TimeSpan.FromSeconds(12),
            FailureReason = failureReason,
            FailureKind = failureKind,
            Comments = comments,
        };
}
