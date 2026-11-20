using ReviewAgent.Application.Reviews.Commands.RecordModelCall;
using ReviewAgent.Application.Tests.Fakes;
using ReviewAgent.Domain.Reviews;

namespace ReviewAgent.Application.Tests.Reviews.Commands.RecordModelCall;

public sealed class RecordModelCallCommandHandlerTests
{
    private readonly InMemoryReviewRunRepository _repository = new();

    private RecordModelCallCommandHandler Handler => new(_repository, new FixedTimeProvider(TestReviewRuns.Now));

    [Fact]
    public async Task Model_call_is_saved()
    {
        var runId = await TestReviewRuns.SaveRunningRunAsync(_repository);

        await Handler.HandleAsync(
            new RecordModelCallCommand(runId, "code-reviewer", TimeSpan.FromSeconds(2), 1200, 80),
            CancellationToken.None);

        var run = await _repository.FindAsync(new ReviewRunId(runId), CancellationToken.None);
        Assert.NotNull(run);
        Assert.Equal(1, run.ModelCallCount);
        Assert.Equal(TimeSpan.FromSeconds(2), run.ModelTime);
        Assert.Equal(1200, run.InputTokenCount);
        Assert.Equal(80, run.OutputTokenCount);
    }
}
