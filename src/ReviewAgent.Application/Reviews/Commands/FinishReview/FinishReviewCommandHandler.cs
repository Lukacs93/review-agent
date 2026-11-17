using ReviewAgent.Application.Interfaces;
using ReviewAgent.Domain.Reviews;

namespace ReviewAgent.Application.Reviews.Commands.FinishReview;

public sealed class FinishReviewCommandHandler(IReviewRunRepository repository, TimeProvider timeProvider)
    : ICommandHandler<FinishReviewCommand>
{
    public async Task HandleAsync(FinishReviewCommand command, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(command);

        var run = await repository.GetAsync(new ReviewRunId(command.RunId), cancellationToken);
        run.Finish(command.Summary, timeProvider.GetUtcNow());
        await repository.SaveAsync(run, cancellationToken);
    }
}
