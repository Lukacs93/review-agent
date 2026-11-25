using ReviewAgent.Application.Interfaces;
using ReviewAgent.Domain.Reviews;

namespace ReviewAgent.Application.Reviews.Commands.FailVerification;

public sealed class FailVerificationCommandHandler(IReviewRunRepository repository, TimeProvider timeProvider)
    : ICommandHandler<FailVerificationCommand>
{
    public async Task HandleAsync(FailVerificationCommand command, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(command);

        var run = await repository.GetAsync(new ReviewRunId(command.RunId), cancellationToken);
        run.FailVerification(command.Reason, command.Kind, timeProvider.GetUtcNow());
        await repository.SaveAsync(run, cancellationToken);
    }
}
