using ReviewAgent.Application.Interfaces;
using ReviewAgent.Domain.Reviews;

namespace ReviewAgent.Application.Reviews.Commands;

public sealed class RecordToolCallHandler(IReviewRunRepository repository, TimeProvider timeProvider)
    : ICommandHandler<RecordToolCall>
{
    public async Task HandleAsync(RecordToolCall command, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(command);

        var run = await repository.GetAsync(new ReviewRunId(command.RunId), cancellationToken);
        run.RecordToolCall(command.ToolName, command.Arguments, timeProvider.GetUtcNow());
        await repository.SaveAsync(run, cancellationToken);
    }
}
