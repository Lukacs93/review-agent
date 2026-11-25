using ReviewAgent.Application.Interfaces;
using ReviewAgent.Domain.Reviews;

namespace ReviewAgent.Application.Reviews.Commands.FailReview;

public sealed class FailReviewCommandHandler(IReviewRunRepository repository, TimeProvider timeProvider)
    : ICommandHandler<FailReviewCommand>
{
    public async Task HandleAsync(FailReviewCommand command, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(command);

        var run = await repository.GetAsync(new ReviewRunId(command.RunId), cancellationToken);
        run.Fail(command.Reason, command.Kind, timeProvider.GetUtcNow());
        await repository.SaveAsync(run, cancellationToken);
    }
}
