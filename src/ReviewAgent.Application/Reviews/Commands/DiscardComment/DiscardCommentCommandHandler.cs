using ReviewAgent.Application.Interfaces;
using ReviewAgent.Domain.Reviews;

namespace ReviewAgent.Application.Reviews.Commands.DiscardComment;

public sealed class DiscardCommentCommandHandler(IReviewRunRepository repository, TimeProvider timeProvider)
    : ICommandHandler<DiscardCommentCommand>
{
    public async Task HandleAsync(DiscardCommentCommand command, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(command);

        var run = await repository.GetAsync(new ReviewRunId(command.RunId), cancellationToken);
        run.DiscardComment(new CommentId(command.CommentId), timeProvider.GetUtcNow());
        await repository.SaveAsync(run, cancellationToken);
    }
}
