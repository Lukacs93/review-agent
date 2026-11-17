using ReviewAgent.Application.Interfaces;
using ReviewAgent.Domain.Reviews;

namespace ReviewAgent.Application.Reviews.Commands.RejectComment;

public sealed class RejectCommentCommandHandler(IReviewRunRepository repository, TimeProvider timeProvider)
    : ICommandHandler<RejectCommentCommand>
{
    public async Task HandleAsync(RejectCommentCommand command, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(command);

        var run = await repository.GetAsync(new ReviewRunId(command.RunId), cancellationToken);
        run.RejectComment(new CommentId(command.CommentId), command.Reason, timeProvider.GetUtcNow());
        await repository.SaveAsync(run, cancellationToken);
    }
}
