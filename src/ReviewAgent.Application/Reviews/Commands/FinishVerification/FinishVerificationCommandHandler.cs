using ReviewAgent.Application.Interfaces;
using ReviewAgent.Domain.Reviews;

namespace ReviewAgent.Application.Reviews.Commands.FinishVerification;

public sealed class FinishVerificationCommandHandler(IReviewRunRepository repository, TimeProvider timeProvider)
    : ICommandHandler<FinishVerificationCommand>
{
    public async Task HandleAsync(FinishVerificationCommand command, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(command);

        var run = await repository.GetAsync(new ReviewRunId(command.RunId), cancellationToken);
        run.FinishVerification(command.Summary, timeProvider.GetUtcNow());
        await repository.SaveAsync(run, cancellationToken);
    }
}
