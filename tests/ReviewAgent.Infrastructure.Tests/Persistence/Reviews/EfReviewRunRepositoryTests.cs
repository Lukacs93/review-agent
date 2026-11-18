using Microsoft.EntityFrameworkCore;
using ReviewAgent.Application.Reviews;
using ReviewAgent.Application.Reviews.Errors;
using ReviewAgent.Domain.Reviews;
using ReviewAgent.Domain.Rules;
using ReviewAgent.Infrastructure.Persistence.Reviews;

namespace ReviewAgent.Infrastructure.Tests.Persistence.Reviews;

public sealed class EfReviewRunRepositoryTests : IDisposable
{
    private const string RepositoryOwner = "test";
    private const string RepositoryName = "demo";
    private const int PullRequestNumber = 1;
    private const string FilePath = "src/Orders.Api/Controllers/OrdersController.cs";
    private const int Line = 12;
    private const string CommentBody = "Use an application handler instead of the DbContext.";
    private const string Summary = "One architecture issue.";

    private readonly SqliteTestDatabase _database = new();

    private static DateTimeOffset Now => new(2026, 11, 16, 9, 0, 0, TimeSpan.Zero);

    [Fact]
    public async Task Saved_run_is_rebuilt_from_its_events()
    {
        var run = FinishedRunWithOneComment();
        await SaveAsync(run);

        var loaded = await FindAsync(run.Id);

        Assert.NotNull(loaded);
        Assert.Equal(run.Id, loaded.Id);
        Assert.Equal(new PullRequestRef(RepositoryOwner, RepositoryName, PullRequestNumber), loaded.PullRequest);
        Assert.Equal(ReviewStatus.AwaitingApproval, loaded.Status);
        Assert.Equal(1, loaded.ToolCallCount);
        Assert.Equal(Summary, loaded.Summary);
        Assert.Equal(4, loaded.Version);
        Assert.Empty(loaded.UncommittedEvents);

        var comment = Assert.Single(loaded.Comments);
        Assert.Equal(new CodeLocation(FilePath, Line), comment.Location);
        Assert.Equal(new RuleId("ARCH-01"), comment.Rule);
        Assert.Equal(Severity.Error, comment.Severity);
        Assert.Equal(CommentBody, comment.Body);
    }

    [Fact]
    public async Task Events_are_stored_in_order_with_their_type_names()
    {
        var run = FinishedRunWithOneComment();
        await SaveAsync(run);

        using var dbContext = _database.CreateDbContext();
        var storedEvents = await dbContext.ReviewEvents
            .Where(storedEvent => storedEvent.RunId == run.Id.Value)
            .OrderBy(storedEvent => storedEvent.Version)
            .ToListAsync(CancellationToken.None);

        string[] expectedEventTypes = ["ReviewStarted", "ToolCalled", "CommentProposed", "ReviewFinished"];
        Assert.Equal(Enumerable.Range(1, 4), storedEvents.Select(storedEvent => storedEvent.Version));
        Assert.Equal(expectedEventTypes, storedEvents.Select(storedEvent => storedEvent.EventType));
    }

    [Fact]
    public async Task Run_version_is_stored_with_the_events()
    {
        var run = FinishedRunWithOneComment();

        await SaveAsync(run);

        Assert.Equal(4, await StoredVersionAsync(run.Id));
    }

    [Fact]
    public async Task Saving_clears_the_uncommitted_events()
    {
        var run = FinishedRunWithOneComment();

        await SaveAsync(run);

        Assert.Empty(run.UncommittedEvents);
    }

    [Fact]
    public async Task Later_save_appends_only_the_new_events()
    {
        var run = FinishedRunWithOneComment();
        await SaveAsync(run);

        var loaded = await FindAsync(run.Id);
        Assert.NotNull(loaded);
        loaded.ApproveComment(loaded.Comments[0].Id, Now);
        await SaveAsync(loaded);

        var reloaded = await FindAsync(run.Id);
        Assert.NotNull(reloaded);
        Assert.Equal(5, reloaded.Version);
        Assert.Equal(CommentDecision.Approved, reloaded.Comments[0].Decision);
        Assert.Equal(5, await CountStoredEventsAsync(run.Id));
        Assert.Equal(5, await StoredVersionAsync(run.Id));
    }

    [Fact]
    public async Task Saving_without_new_events_writes_nothing()
    {
        var run = FinishedRunWithOneComment();
        await SaveAsync(run);

        await SaveAsync(run);

        Assert.Equal(4, await CountStoredEventsAsync(run.Id));
    }

    [Fact]
    public async Task Unknown_run_is_not_found()
    {
        var loaded = await FindAsync(ReviewRunId.New());

        Assert.Null(loaded);
    }

    [Fact]
    public async Task Concurrent_change_to_the_same_run_is_rejected()
    {
        var run = FinishedRunWithOneComment();
        await SaveAsync(run);

        var first = await FindAsync(run.Id);
        var second = await FindAsync(run.Id);
        Assert.NotNull(first);
        Assert.NotNull(second);

        first.ApproveComment(first.Comments[0].Id, Now);
        await SaveAsync(first);

        second.DiscardComment(second.Comments[0].Id, Now);
        var error = await Assert.ThrowsAsync<ConcurrencyConflictException>(() => SaveAsync(second));

        Assert.Equal(ReviewRunRepositoryErrors.ConcurrentChange(run.Id), error.Message);
        Assert.Equal(5, await CountStoredEventsAsync(run.Id));
        var stored = await FindAsync(run.Id);
        Assert.NotNull(stored);
        Assert.Equal(CommentDecision.Approved, stored.Comments[0].Decision);
    }

    public void Dispose() => _database.Dispose();

    [Fact]
    public async Task Only_runs_not_finished_or_failed_are_running()
    {
        var started = StartedRun();
        var working = StartedRun();
        working.RecordToolCall("get_diff", null, Now);
        var finished = FinishedRunWithOneComment();
        var failed = StartedRun();
        failed.Fail("The model could not be reached.", Now);
        foreach (var run in new[] { started, working, finished, failed })
        {
            await SaveAsync(run);
        }

        using var dbContext = _database.CreateDbContext();
        var runningIds = await new EfReviewRunRepository(dbContext).FindRunningIdsAsync(CancellationToken.None);

        ReviewRunId[] expected = [started.Id, working.Id];
        Assert.Equal(expected.OrderBy(id => id.Value), runningIds.OrderBy(id => id.Value));
    }

    private static ReviewRun StartedRun() =>
        ReviewRun.Start(
            ReviewRunId.New(),
            new PullRequestRef(RepositoryOwner, RepositoryName, PullRequestNumber),
            Now);

    private static ReviewRun FinishedRunWithOneComment()
    {
        var run = ReviewRun.Start(
            ReviewRunId.New(),
            new PullRequestRef(RepositoryOwner, RepositoryName, PullRequestNumber),
            Now);
        run.RecordToolCall("get_diff", null, Now);
        run.ProposeComment(
            new CodeLocation(FilePath, Line),
            new RuleId("ARCH-01"),
            Severity.Error,
            CommentBody,
            suggestedFix: null,
            Now);
        run.Finish(Summary, Now);
        return run;
    }

    // Every call uses a new context, as separate requests would.
    private async Task SaveAsync(ReviewRun run)
    {
        using var dbContext = _database.CreateDbContext();
        await new EfReviewRunRepository(dbContext).SaveAsync(run, CancellationToken.None);
    }

    private async Task<ReviewRun?> FindAsync(ReviewRunId id)
    {
        using var dbContext = _database.CreateDbContext();
        return await new EfReviewRunRepository(dbContext).FindAsync(id, CancellationToken.None);
    }

    private async Task<int> CountStoredEventsAsync(ReviewRunId id)
    {
        using var dbContext = _database.CreateDbContext();
        return await dbContext.ReviewEvents
            .CountAsync(storedEvent => storedEvent.RunId == id.Value, CancellationToken.None);
    }

    private async Task<int> StoredVersionAsync(ReviewRunId id)
    {
        using var dbContext = _database.CreateDbContext();
        return await dbContext.ReviewRuns
            .Where(storedRun => storedRun.Id == id.Value)
            .Select(storedRun => storedRun.Version)
            .SingleAsync(CancellationToken.None);
    }
}
