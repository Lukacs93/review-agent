using ReviewAgent.Application.Interfaces;
using ReviewAgent.Domain.Reviews;

namespace ReviewAgent.Application.Reviews.Commands.ApproveComment;

public sealed class ApproveCommentCommandHandler(IReviewRunRepository repository, TimeProvider timeProvider)
    : ICommandHandler<ApproveCommentCommand>
{
    public async Task HandleAsync(ApproveCommentCommand command, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(command);

        var run = await repository.GetAsync(new ReviewRunId(command.RunId), cancellationToken);
        run.ApproveComment(new CommentId(command.CommentId), timeProvider.GetUtcNow());
        await repository.SaveAsync(run, cancellationToken);
    }
}
