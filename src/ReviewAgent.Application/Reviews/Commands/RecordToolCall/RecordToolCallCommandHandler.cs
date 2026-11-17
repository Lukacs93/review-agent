using ReviewAgent.Application.Interfaces;
using ReviewAgent.Domain.Reviews;

namespace ReviewAgent.Application.Reviews.Commands.RecordToolCall;

public sealed class RecordToolCallCommandHandler(IReviewRunRepository repository, TimeProvider timeProvider)
    : ICommandHandler<RecordToolCallCommand>
{
    public async Task HandleAsync(RecordToolCallCommand command, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(command);

        var run = await repository.GetAsync(new ReviewRunId(command.RunId), cancellationToken);
        run.RecordToolCall(command.ToolName, command.Arguments, timeProvider.GetUtcNow());
        await repository.SaveAsync(run, cancellationToken);
    }
}
