using ReviewAgent.Application.Interfaces;
using ReviewAgent.Domain.Reviews;

namespace ReviewAgent.Application.Reviews.Commands;

/// <summary>Creates and saves a new review run; returns its ID.</summary>
public sealed class StartReviewHandler(IReviewRunRepository repository, TimeProvider timeProvider)
    : ICommandHandler<StartReview, Guid>
{
    public async Task<Guid> HandleAsync(StartReview command, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(command);

        var pullRequest = new PullRequestRef(
            command.RepositoryOwner,
            command.RepositoryName,
            command.PullRequestNumber);
        var run = ReviewRun.Start(ReviewRunId.New(), pullRequest, timeProvider.GetUtcNow());

        await repository.SaveAsync(run, cancellationToken);
        return run.Id.Value;
    }
}
