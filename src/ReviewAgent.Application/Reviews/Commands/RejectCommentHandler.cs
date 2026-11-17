using ReviewAgent.Application.Interfaces;
using ReviewAgent.Domain.Reviews;

namespace ReviewAgent.Application.Reviews.Commands;

public sealed class RejectCommentHandler(IReviewRunRepository repository, TimeProvider timeProvider)
    : ICommandHandler<RejectComment>
{
    public async Task HandleAsync(RejectComment command, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(command);

        var run = await repository.GetAsync(new ReviewRunId(command.RunId), cancellationToken);
        run.RejectComment(new CommentId(command.CommentId), command.Reason, timeProvider.GetUtcNow());
        await repository.SaveAsync(run, cancellationToken);
    }
}
