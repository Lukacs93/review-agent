using System.Globalization;
using Microsoft.Extensions.AI;

namespace ReviewAgent.Infrastructure.Tests.Agents;

/// <summary>
/// A model that gives the prepared answers in order: tool calls first, then the summary. The real tool-calling loop
/// runs on top of it, so the tests exercise the same path as a real model.
/// </summary>
internal sealed class ScriptedChatClient(params ChatResponse[] responses) : IChatClient
{
    private readonly Queue<ChatResponse> _responses = new(responses);

    public static ChatResponse CallsTool(string name, Dictionary<string, object?>? arguments = null) =>
        new(new ChatMessage(ChatRole.Assistant, [new FunctionCallContent(Guid.NewGuid().ToString("N", CultureInfo.InvariantCulture), name, arguments)]));

    public static ChatResponse Answers(string text) => new(new ChatMessage(ChatRole.Assistant, text));

    public Task<ChatResponse> GetResponseAsync(
        IEnumerable<ChatMessage> messages,
        ChatOptions? options = null,
        CancellationToken cancellationToken = default) =>
        Task.FromResult(_responses.Dequeue());

    public IAsyncEnumerable<ChatResponseUpdate> GetStreamingResponseAsync(
        IEnumerable<ChatMessage> messages,
        ChatOptions? options = null,
        CancellationToken cancellationToken = default) =>
        throw new NotSupportedException();

    public object? GetService(Type serviceType, object? serviceKey = null) => null;

    public void Dispose()
    {
    }
}
