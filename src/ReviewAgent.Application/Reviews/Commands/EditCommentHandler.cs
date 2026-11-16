using ReviewAgent.Application.Interfaces;
using ReviewAgent.Domain.Reviews;

namespace ReviewAgent.Application.Reviews.Commands;

public sealed class EditCommentHandler(IReviewRunRepository repository, TimeProvider timeProvider)
    : ICommandHandler<EditComment>
{
    public async Task HandleAsync(EditComment command, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(command);

        var run = await repository.GetAsync(new ReviewRunId(command.RunId), cancellationToken);
        run.EditComment(new CommentId(command.CommentId), command.Body, timeProvider.GetUtcNow());
        await repository.SaveAsync(run, cancellationToken);
    }
}
