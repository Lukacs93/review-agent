using ReviewAgent.Application.Reviews;
using ReviewAgent.Domain.Reviews;
using ReviewAgent.Domain.Rules;

namespace ReviewAgent.Application.Tests.Reviews;

/// <summary>Review runs in a known state, saved to a repository, for the handler tests to start from.</summary>
internal static class TestReviewRuns
{
    public const string RepositoryOwner = "test";
    public const string RepositoryName = "demo";
    public const int PullRequestNumber = 1;
    public const string FilePath = "src/Orders.Api/Controllers/OrdersController.cs";
    public const int Line = 12;
    public const string Rule = "ARCH-01";
    public const string CommentBody = "Use an application handler instead of the DbContext.";
    public const string Summary = "One architecture issue.";
    public const string CommitSha = "9fceb02d0ae598e95dc970b74767f19372d61af8";

    public static DateTimeOffset Now => new(2026, 11, 16, 9, 0, 0, TimeSpan.Zero);

    /// <summary>A run the agent has finished, with one comment waiting for a decision.</summary>
    public static async Task<(Guid RunId, Guid CommentId)> SaveFinishedRunWithOneCommentAsync(
        IReviewRunRepository repository)
    {
        var run = ReviewRun.Start(
            ReviewRunId.New(),
            new PullRequestRef(RepositoryOwner, RepositoryName, PullRequestNumber),
            Now);
        run.RecordToolCall("get_diff", null, Now);
        var commentId = run.ProposeComment(
            new CodeLocation(FilePath, Line),
            new RuleId(Rule),
            Severity.Error,
            CommentBody,
            replacementCode: null,
            Now);
        run.Finish(Summary, CommitSha, Now);

        await repository.SaveAsync(run, CancellationToken.None);
        return (run.Id.Value, commentId.Value);
    }

    /// <summary>A finished run with its one comment approved; publishing has not been asked for yet.</summary>
    public static async Task<Guid> SaveApprovedRunAsync(IReviewRunRepository repository)
    {
        var (runId, commentId) = await SaveFinishedRunWithOneCommentAsync(repository);

        var run = await repository.GetAsync(new ReviewRunId(runId), CancellationToken.None);
        run.ApproveComment(new CommentId(commentId), Now);
        await repository.SaveAsync(run, CancellationToken.None);
        return runId;
    }

    /// <summary>A run being published: its one comment approved, and publishing asked for.</summary>
    public static async Task<Guid> SavePublishingRunAsync(IReviewRunRepository repository)
    {
        var runId = await SaveApprovedRunAsync(repository);

        var run = await repository.GetAsync(new ReviewRunId(runId), CancellationToken.None);
        run.RequestPublishing(Now);
        await repository.SaveAsync(run, CancellationToken.None);
        return runId;
    }

    /// <summary>A run the agent is still working on, with one proposed comment.</summary>
    public static async Task<(Guid RunId, Guid CommentId)> SaveRunningRunWithOneCommentAsync(
        IReviewRunRepository repository)
    {
        var run = ReviewRun.Start(
            ReviewRunId.New(),
            new PullRequestRef(RepositoryOwner, RepositoryName, PullRequestNumber),
            Now);
        var commentId = run.ProposeComment(
            new CodeLocation(FilePath, Line),
            new RuleId(Rule),
            Severity.Error,
            CommentBody,
            replacementCode: null,
            Now);

        await repository.SaveAsync(run, CancellationToken.None);
        return (run.Id.Value, commentId.Value);
    }

    /// <summary>A run the agent is still working on.</summary>
    public static async Task<Guid> SaveRunningRunAsync(IReviewRunRepository repository)
    {
        var run = ReviewRun.Start(
            ReviewRunId.New(),
            new PullRequestRef(RepositoryOwner, RepositoryName, PullRequestNumber),
            Now);

        await repository.SaveAsync(run, CancellationToken.None);
        return run.Id.Value;
    }
}
