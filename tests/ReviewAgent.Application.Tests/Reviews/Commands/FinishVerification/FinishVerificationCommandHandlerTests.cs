using ReviewAgent.Application.Reviews.Commands.FinishVerification;
using ReviewAgent.Application.Tests.Fakes;
using ReviewAgent.Domain.Reviews;

namespace ReviewAgent.Application.Tests.Reviews.Commands.FinishVerification;

public sealed class FinishVerificationCommandHandlerTests
{
    private readonly InMemoryReviewRunRepository _repository = new();

    private FinishVerificationCommandHandler Handler => new(_repository, new FixedTimeProvider(TestReviewRuns.Now));

    [Fact]
    public async Task Verification_is_saved()
    {
        var (runId, _) = await TestReviewRuns.SaveRunningRunWithOneCommentAsync(_repository);

        await Handler.HandleAsync(new FinishVerificationCommand(runId), CancellationToken.None);

        var run = await _repository.FindAsync(new ReviewRunId(runId), CancellationToken.None);
        Assert.NotNull(run);
        Assert.True(run.IsVerified);
    }
}
