using ReviewAgent.Application.Interfaces;
using ReviewAgent.Domain.Reviews;

namespace ReviewAgent.Application.Reviews.Commands;

public sealed class DiscardCommentHandler(IReviewRunRepository repository, TimeProvider timeProvider)
    : ICommandHandler<DiscardComment>
{
    public async Task HandleAsync(DiscardComment command, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(command);

        var run = await repository.GetAsync(new ReviewRunId(command.RunId), cancellationToken);
        run.DiscardComment(new CommentId(command.CommentId), timeProvider.GetUtcNow());
        await repository.SaveAsync(run, cancellationToken);
    }
}
