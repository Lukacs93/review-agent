using System.Text.Json;
using Microsoft.Extensions.AI;
using ReviewAgent.Application.Interfaces;
using ReviewAgent.Application.Reviews.Commands;

namespace ReviewAgent.Infrastructure.Agents.Tools;

/// <summary>
/// Wraps a tool so every call the model makes is recorded on the review run before it runs: the run shows each
/// step, and the tool calls can be counted when the agent is evaluated.
/// </summary>
internal sealed class RecordedToolFunction(
    AIFunction innerFunction,
    Guid runId,
    ICommandHandler<RecordToolCall> recordToolCall) : DelegatingAIFunction(innerFunction)
{
    protected override async ValueTask<object?> InvokeCoreAsync(
        AIFunctionArguments arguments,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(arguments);

        var argumentsJson = arguments.Count == 0
            ? null
            : JsonSerializer.Serialize(new Dictionary<string, object?>(arguments));

        await recordToolCall.HandleAsync(new RecordToolCall(runId, Name, argumentsJson), cancellationToken);
        return await base.InvokeCoreAsync(arguments, cancellationToken);
    }
}
