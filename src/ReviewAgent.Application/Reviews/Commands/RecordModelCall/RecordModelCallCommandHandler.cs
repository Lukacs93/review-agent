using ReviewAgent.Application.Interfaces;
using ReviewAgent.Domain.Reviews;

namespace ReviewAgent.Application.Reviews.Commands.RecordModelCall;

public sealed class RecordModelCallCommandHandler(IReviewRunRepository repository, TimeProvider timeProvider)
    : ICommandHandler<RecordModelCallCommand>
{
    public async Task HandleAsync(RecordModelCallCommand command, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(command);

        var run = await repository.GetAsync(new ReviewRunId(command.RunId), cancellationToken);
        run.RecordModelCall(
            command.AgentName,
            command.Duration,
            command.InputTokens,
            command.OutputTokens,
            timeProvider.GetUtcNow());
        await repository.SaveAsync(run, cancellationToken);
    }
}
