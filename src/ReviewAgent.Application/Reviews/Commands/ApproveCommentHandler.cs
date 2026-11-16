using ReviewAgent.Application.Interfaces;
using ReviewAgent.Domain.Reviews;

namespace ReviewAgent.Application.Reviews.Commands;

public sealed class ApproveCommentHandler(IReviewRunRepository repository, TimeProvider timeProvider)
    : ICommandHandler<ApproveComment>
{
    public async Task HandleAsync(ApproveComment command, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(command);

        var run = await repository.GetAsync(new ReviewRunId(command.RunId), cancellationToken);
        run.ApproveComment(new CommentId(command.CommentId), timeProvider.GetUtcNow());
        await repository.SaveAsync(run, cancellationToken);
    }
}
