using System.Diagnostics;
using Microsoft.Extensions.AI;
using ReviewAgent.Application.Interfaces;
using ReviewAgent.Application.Reviews.Commands.RecordModelCall;

namespace ReviewAgent.Infrastructure.Agents.LanguageModel;

/// <summary>
/// Records every model call of an agent on the review run: how long it took and how many tokens it used, as the
/// model's API reports them. The run's statistics and the evaluation are built from these records.
/// </summary>
// Placed inside the function-invoking client, so each round of the tool loop is one model call. The agents only
// ask for whole answers, not streamed ones, so only those are recorded.
internal sealed class RecordedChatClient(
    IChatClient innerClient,
    Guid runId,
    ICommandHandler<RecordModelCallCommand> recordModelCall,
    string agentName) : DelegatingChatClient(innerClient)
{
    public override async Task<ChatResponse> GetResponseAsync(
        IEnumerable<ChatMessage> messages,
        ChatOptions? options = null,
        CancellationToken cancellationToken = default)
    {
        var started = Stopwatch.GetTimestamp();
        var response = await base.GetResponseAsync(messages, options, cancellationToken);
        var duration = Stopwatch.GetElapsedTime(started);

        await recordModelCall.HandleAsync(
            new RecordModelCallCommand(
                runId,
                agentName,
                duration,
                response.Usage?.InputTokenCount,
                response.Usage?.OutputTokenCount),
            cancellationToken);
        return response;
    }
}
