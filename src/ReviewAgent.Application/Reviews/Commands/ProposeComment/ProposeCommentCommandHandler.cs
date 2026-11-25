using ReviewAgent.Application.Interfaces;
using ReviewAgent.Domain.Reviews;
using ReviewAgent.Domain.Rules;

namespace ReviewAgent.Application.Reviews.Commands.ProposeComment;

/// <summary>Records a proposed comment; returns its ID.</summary>
public sealed class ProposeCommentCommandHandler(IReviewRunRepository repository, TimeProvider timeProvider)
    : ICommandHandler<ProposeCommentCommand, Guid>
{
    public async Task<Guid> HandleAsync(ProposeCommentCommand command, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(command);

        var run = await repository.GetAsync(new ReviewRunId(command.RunId), cancellationToken);
        var commentId = run.ProposeComment(
            new CodeLocation(command.FilePath, command.Line),
            new RuleId(command.RuleId),
            command.Severity,
            command.Body,
            command.ReplacementCode,
            timeProvider.GetUtcNow());

        await repository.SaveAsync(run, cancellationToken);
        return commentId.Value;
    }
}
