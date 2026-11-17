using System.ClientModel;
using System.ClientModel.Primitives;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.Options;
using OpenAI;
using ReviewAgent.Infrastructure.Agents.Errors;

namespace ReviewAgent.Infrastructure.Agents.LanguageModel;

/// <summary>
/// A chat client for any OpenAI-compatible API: the official OpenAI SDK pointed at the configured endpoint.
/// Switching provider or model is a configuration change.
/// </summary>
/// <remarks>
/// The SDK sends its requests through our <see cref="HttpClient"/>, whose resilience handler retries and times out
/// the requests (see <c>AddReviewAgent</c>). The SDK's own retries are turned off, so a failing request is not
/// retried by both.
/// </remarks>
internal sealed class OpenAICompatibleChatClientFactory(
    IHttpClientFactory httpClientFactory,
    IOptions<AgentOptions> options) : IAgentChatClientFactory
{
    public const string HttpClientName = "Model";

    public IChatClient Create()
    {
        var agent = options.Value;
        if (string.IsNullOrWhiteSpace(agent.ApiKey))
        {
            throw new InvalidOperationException(AgentErrors.ApiKeyMissing);
        }

        var clientOptions = new OpenAIClientOptions
        {
            Endpoint = agent.Endpoint,
            Transport = new HttpClientPipelineTransport(httpClientFactory.CreateClient(HttpClientName)),
            RetryPolicy = new ClientRetryPolicy(maxRetries: 0),

            // The resilience handler's timeouts apply; this only keeps the SDK's 100-second default out of the way.
            NetworkTimeout = agent.Timeout,
        };

        var client = new OpenAIClient(new ApiKeyCredential(agent.ApiKey), clientOptions);

        return client.GetChatClient(agent.Model).AsIChatClient();
    }
}
