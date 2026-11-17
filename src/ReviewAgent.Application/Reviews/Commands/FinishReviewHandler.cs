using ReviewAgent.Application.Interfaces;
using ReviewAgent.Domain.Reviews;

namespace ReviewAgent.Application.Reviews.Commands;

public sealed class FinishReviewHandler(IReviewRunRepository repository, TimeProvider timeProvider)
    : ICommandHandler<FinishReview>
{
    public async Task HandleAsync(FinishReview command, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(command);

        var run = await repository.GetAsync(new ReviewRunId(command.RunId), cancellationToken);
        run.Finish(command.Summary, timeProvider.GetUtcNow());
        await repository.SaveAsync(run, cancellationToken);
    }
}
