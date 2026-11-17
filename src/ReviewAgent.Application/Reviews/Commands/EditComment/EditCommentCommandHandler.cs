using ReviewAgent.Application.Interfaces;
using ReviewAgent.Domain.Reviews;

namespace ReviewAgent.Application.Reviews.Commands.EditComment;

public sealed class EditCommentCommandHandler(IReviewRunRepository repository, TimeProvider timeProvider)
    : ICommandHandler<EditCommentCommand>
{
    public async Task HandleAsync(EditCommentCommand command, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(command);

        var run = await repository.GetAsync(new ReviewRunId(command.RunId), cancellationToken);
        run.EditComment(new CommentId(command.CommentId), command.Body, timeProvider.GetUtcNow());
        await repository.SaveAsync(run, cancellationToken);
    }
}
