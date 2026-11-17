using ReviewAgent.Application.Reviews.Commands.RecordToolCall;
using ReviewAgent.Application.Tests.Fakes;
using ReviewAgent.Domain.Reviews;

namespace ReviewAgent.Application.Tests.Reviews.Commands.RecordToolCall;

public sealed class RecordToolCallCommandHandlerTests
{
    private readonly InMemoryReviewRunRepository _repository = new();

    private RecordToolCallCommandHandler Handler => new(_repository, new FixedTimeProvider(TestReviewRuns.Now));

    [Fact]
    public async Task Tool_call_is_saved()
    {
        var runId = await TestReviewRuns.SaveRunningRunAsync(_repository);

        await Handler.HandleAsync(new RecordToolCallCommand(runId, "get_diff", "src/Order.cs"), CancellationToken.None);

        var run = await _repository.FindAsync(new ReviewRunId(runId), CancellationToken.None);
        Assert.NotNull(run);
        Assert.Equal(1, run.ToolCallCount);
    }
}
