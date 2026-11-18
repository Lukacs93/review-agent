using System.Globalization;
using Microsoft.Extensions.AI;

namespace ReviewAgent.Infrastructure.Tests.Agents;

/// <summary>
/// A model that gives the prepared answers in order: tool calls first, then the summary. The real tool-calling loop
/// runs on top of it, so the tests exercise the same path as a real model. A step can read the messages it gets,
/// for example to take an ID from the task.
/// </summary>
internal sealed class ScriptedChatClient(params Func<IReadOnlyList<ChatMessage>, ChatResponse>[] steps) : IChatClient
{
    private readonly Queue<Func<IReadOnlyList<ChatMessage>, ChatResponse>> _steps = new(steps);

    public static Func<IReadOnlyList<ChatMessage>, ChatResponse> CallsTool(
        string name,
        Dictionary<string, object?>? arguments = null) =>
        _ => ToolCall(name, arguments);

    public static Func<IReadOnlyList<ChatMessage>, ChatResponse> CallsToolWith(
        string name,
        Func<IReadOnlyList<ChatMessage>, Dictionary<string, object?>> arguments) =>
        messages => ToolCall(name, arguments(messages));

    public static Func<IReadOnlyList<ChatMessage>, ChatResponse> Answers(string text) =>
        _ => new ChatResponse(new ChatMessage(ChatRole.Assistant, text));

    public static Func<IReadOnlyList<ChatMessage>, ChatResponse> Fails(Exception exception) =>
        _ => throw exception;

    public Task<ChatResponse> GetResponseAsync(
        IEnumerable<ChatMessage> messages,
        ChatOptions? options = null,
        CancellationToken cancellationToken = default) =>
        Task.FromResult(_steps.Dequeue()([.. messages]));

    public IAsyncEnumerable<ChatResponseUpdate> GetStreamingResponseAsync(
        IEnumerable<ChatMessage> messages,
        ChatOptions? options = null,
        CancellationToken cancellationToken = default) =>
        throw new NotSupportedException();

    public object? GetService(Type serviceType, object? serviceKey = null) => null;

    public void Dispose()
    {
    }

    private static ChatResponse ToolCall(string name, Dictionary<string, object?>? arguments) =>
        new(new ChatMessage(
            ChatRole.Assistant,
            [new FunctionCallContent(Guid.NewGuid().ToString("N", CultureInfo.InvariantCulture), name, arguments)]));
}
