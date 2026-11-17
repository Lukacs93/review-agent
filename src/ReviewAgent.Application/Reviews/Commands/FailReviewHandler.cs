using ReviewAgent.Application.Interfaces;
using ReviewAgent.Domain.Reviews;

namespace ReviewAgent.Application.Reviews.Commands;

public sealed class FailReviewHandler(IReviewRunRepository repository, TimeProvider timeProvider)
    : ICommandHandler<FailReview>
{
    public async Task HandleAsync(FailReview command, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(command);

        var run = await repository.GetAsync(new ReviewRunId(command.RunId), cancellationToken);
        run.Fail(command.Reason, timeProvider.GetUtcNow());
        await repository.SaveAsync(run, cancellationToken);
    }
}
