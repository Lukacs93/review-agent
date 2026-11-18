using ReviewAgent.Application.Reviews.Commands.FinishVerification;
using ReviewAgent.Application.Tests.Fakes;
using ReviewAgent.Domain.Reviews;

namespace ReviewAgent.Application.Tests.Reviews.Commands.FinishVerification;

public sealed class FinishVerificationCommandHandlerTests
{
    private const string VerifierSummary = "Kept 1 comment and rejected 0.";

    private readonly InMemoryReviewRunRepository _repository = new();

    private FinishVerificationCommandHandler Handler => new(_repository, new FixedTimeProvider(TestReviewRuns.Now));

    [Fact]
    public async Task Verifier_summary_is_saved()
    {
        var (runId, _) = await TestReviewRuns.SaveRunningRunWithOneCommentAsync(_repository);

        await Handler.HandleAsync(new FinishVerificationCommand(runId, VerifierSummary), CancellationToken.None);

        var run = await _repository.FindAsync(new ReviewRunId(runId), CancellationToken.None);
        Assert.NotNull(run);
        Assert.Equal(VerifierSummary, run.VerifierSummary);
    }
}
