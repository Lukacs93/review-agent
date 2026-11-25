using ReviewAgent.Application.Reviews.Commands.FailVerification;
using ReviewAgent.Application.Tests.Fakes;
using ReviewAgent.Domain.Reviews;

namespace ReviewAgent.Application.Tests.Reviews.Commands.FailVerification;

public sealed class FailVerificationCommandHandlerTests
{
    private const string Reason = "The model could not be reached.";

    private readonly InMemoryReviewRunRepository _repository = new();

    private FailVerificationCommandHandler Handler => new(_repository, new FixedTimeProvider(TestReviewRuns.Now));

    [Fact]
    public async Task Failed_verification_is_saved_with_its_kind()
    {
        var (runId, _) = await TestReviewRuns.SaveRunningRunWithOneCommentAsync(_repository);

        await Handler.HandleAsync(
            new FailVerificationCommand(runId, Reason, ReviewFailureKind.ModelService),
            CancellationToken.None);

        var run = await _repository.FindAsync(new ReviewRunId(runId), CancellationToken.None);
        Assert.NotNull(run);
        Assert.Equal(Reason, run.VerificationFailureReason);
        Assert.Equal(ReviewFailureKind.ModelService, run.VerificationFailureKind);
    }
}
