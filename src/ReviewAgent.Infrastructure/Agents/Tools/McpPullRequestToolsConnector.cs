using Microsoft.Extensions.AI;
using Microsoft.Extensions.Options;
using ModelContextProtocol.Client;
using ReviewAgent.Application.Reviews;
using ReviewAgent.Domain.Reviews;

namespace ReviewAgent.Infrastructure.Agents.Tools;

/// <summary>
/// Connects to our MCP server and returns its tools. The pull request under review is sent in a header that the
/// code sets, so the model's tools can only read this pull request.
/// </summary>
internal sealed class McpPullRequestToolsConnector(
    IHttpClientFactory httpClientFactory,
    IOptions<AgentOptions> options) : IPullRequestToolsConnector
{
    public const string HttpClientName = "McpServer";

    private const string McpPath = "mcp";

    public async Task<PullRequestToolsConnection> ConnectAsync(
        PullRequestRef pullRequest,
        CancellationToken cancellationToken)
    {
        var transport = new HttpClientTransport(
            new HttpClientTransportOptions
            {
                Endpoint = new Uri(options.Value.McpServerUrl!, McpPath),
                TransportMode = HttpTransportMode.StreamableHttp,
                AdditionalHeaders = new Dictionary<string, string>
                {
                    [ReviewAgentHeaderNames.ReviewedPullRequest] = ReviewedPullRequestHeaderValue.Format(pullRequest),
                },
            },
            httpClientFactory.CreateClient(HttpClientName),
            ownsHttpClient: true);

        var client = await McpClient.CreateAsync(transport, cancellationToken: cancellationToken);
        try
        {
            var tools = await client.ListToolsAsync(cancellationToken: cancellationToken);
            return new PullRequestToolsConnection([.. tools], client);
        }
        catch
        {
            await client.DisposeAsync();
            throw;
        }
    }
}
