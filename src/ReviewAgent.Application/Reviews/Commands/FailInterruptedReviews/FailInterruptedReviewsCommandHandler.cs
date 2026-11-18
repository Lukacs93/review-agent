using ReviewAgent.Application.Interfaces;
using ReviewAgent.Application.Reviews.Errors;

namespace ReviewAgent.Application.Reviews.Commands.FailInterruptedReviews;

/// <summary>Fails every running review with the reason that it was interrupted; returns how many it failed.</summary>
public sealed class FailInterruptedReviewsCommandHandler(IReviewRunRepository repository, TimeProvider timeProvider)
    : ICommandHandler<FailInterruptedReviewsCommand, int>
{
    public async Task<int> HandleAsync(FailInterruptedReviewsCommand command, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(command);

        var runningIds = await repository.FindRunningIdsAsync(cancellationToken);
        foreach (var runId in runningIds)
        {
            var run = await repository.GetAsync(runId, cancellationToken);
            run.Fail(InterruptedReviewErrors.Interrupted, timeProvider.GetUtcNow());
            await repository.SaveAsync(run, cancellationToken);
        }

        return runningIds.Count;
    }
}
