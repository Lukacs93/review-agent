using Microsoft.Extensions.AI;
using ReviewAgent.Infrastructure.Agents.LanguageModel;

namespace ReviewAgent.Infrastructure.Tests.Agents.LanguageModel;

public sealed class ModelServiceChatClientTests
{
    private static ChatMessage[] Messages => [new(ChatRole.User, "Review the pull request.")];

    [Fact]
    public async Task Error_of_the_model_call_is_marked_as_the_model_services()
    {
        var error = new HttpRequestException("The model's API answered 503.");
        using var client = new ModelServiceChatClient(new FailingChatClient(error));

        var thrown = await Assert.ThrowsAsync<ModelServiceException>(
            () => client.GetResponseAsync(Messages, cancellationToken: CancellationToken.None));

        Assert.Same(error, thrown.InnerException);
    }

    // The run's time limit or the application stopping: not the model service's failure.
    [Fact]
    public async Task Cancellation_the_caller_asked_for_is_passed_on()
    {
        using var cancellation = new CancellationTokenSource();
        await cancellation.CancelAsync();
        using var client = new ModelServiceChatClient(
            new FailingChatClient(new OperationCanceledException(cancellation.Token)));

        await Assert.ThrowsAsync<OperationCanceledException>(
            () => client.GetResponseAsync(Messages, cancellationToken: cancellation.Token));
    }

    // The model's HTTP client gives up on a slow answer by cancelling the request itself.
    [Fact]
    public async Task Cancellation_the_caller_did_not_ask_for_is_marked_as_the_model_services()
    {
        var error = new TaskCanceledException("The request was canceled due to the configured HttpClient.Timeout.");
        using var client = new ModelServiceChatClient(new FailingChatClient(error));

        var thrown = await Assert.ThrowsAsync<ModelServiceException>(
            () => client.GetResponseAsync(Messages, cancellationToken: CancellationToken.None));

        Assert.Same(error, thrown.InnerException);
    }

    private sealed class FailingChatClient(Exception error) : IChatClient
    {
        public Task<ChatResponse> GetResponseAsync(
            IEnumerable<ChatMessage> messages,
            ChatOptions? options = null,
            CancellationToken cancellationToken = default) =>
            Task.FromException<ChatResponse>(error);

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
}
