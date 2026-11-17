using ReviewAgent.Application.Interfaces;
using ReviewAgent.Domain.Reviews;
using ReviewAgent.Domain.Rules;

namespace ReviewAgent.Application.Reviews.Commands;

/// <summary>Records a proposed comment; returns its ID.</summary>
public sealed class ProposeCommentHandler(IReviewRunRepository repository, TimeProvider timeProvider)
    : ICommandHandler<ProposeComment, Guid>
{
    public async Task<Guid> HandleAsync(ProposeComment command, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(command);

        var run = await repository.GetAsync(new ReviewRunId(command.RunId), cancellationToken);
        var commentId = run.ProposeComment(
            new CodeLocation(command.FilePath, command.Line),
            new RuleId(command.RuleId),
            command.Severity,
            command.Body,
            command.SuggestedFix,
            timeProvider.GetUtcNow());

        await repository.SaveAsync(run, cancellationToken);
        return commentId.Value;
    }
}
