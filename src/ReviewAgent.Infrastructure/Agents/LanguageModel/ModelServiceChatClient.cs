using Microsoft.Extensions.AI;

namespace ReviewAgent.Infrastructure.Agents.LanguageModel;

/// <summary>
/// Marks the errors of the model calls as <see cref="ModelServiceException"/>, so a failure can be told apart from
/// one of the pull request's host or the MCP server, which throw the same kinds of exceptions (HTTP errors,
/// timeouts).
/// </summary>
// The innermost client, right around the model's: whatever fails here failed in the model call. A cancellation
// asked for by the caller (the run's time limit, the application stopping) is passed on as it is.
internal sealed class ModelServiceChatClient(IChatClient innerClient) : DelegatingChatClient(innerClient)
{
    public override async Task<ChatResponse> GetResponseAsync(
        IEnumerable<ChatMessage> messages,
        ChatOptions? options = null,
        CancellationToken cancellationToken = default)
    {
        try
        {
            return await base.GetResponseAsync(messages, options, cancellationToken);
        }
        catch (Exception exception) when (IsModelServiceError(exception, cancellationToken))
        {
            throw new ModelServiceException(exception.Message, exception);
        }
    }

    private static bool IsModelServiceError(Exception exception, CancellationToken cancellationToken) =>
        !(exception is OperationCanceledException && cancellationToken.IsCancellationRequested);
}
