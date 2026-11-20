using ReviewAgent.Application.Reviews.Queries.GetRunLog;
using ReviewAgent.Application.Tests.Fakes;

namespace ReviewAgent.Application.Tests.Reviews.Queries.GetRunLog;

public sealed class GetRunLogQueryHandlerTests
{
    private readonly InMemoryReviewRunRepository _repository = new();
    private readonly InMemoryRunLogReader _reader = new();

    private GetRunLogQueryHandler Handler => new(_repository, _reader);

    [Fact]
    public async Task Log_lines_of_the_run_are_returned()
    {
        var runId = await TestReviewRuns.SaveRunningRunAsync(_repository);
        var line = new RunLogEntryDetails(
            TestReviewRuns.Now,
            "Warning",
            "ReviewAgentRunner",
            "The model is slow.",
            Exception: null);
        _reader.Add(runId, line);

        var lines = await Handler.HandleAsync(new GetRunLogQuery(runId), CancellationToken.None);

        Assert.NotNull(lines);
        Assert.Equal(line, Assert.Single(lines));
    }

    [Fact]
    public async Task Unknown_run_returns_null()
    {
        var lines = await Handler.HandleAsync(new GetRunLogQuery(Guid.NewGuid()), CancellationToken.None);

        Assert.Null(lines);
    }
}
