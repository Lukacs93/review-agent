using Microsoft.AspNetCore.Mvc.Testing;
using ModelContextProtocol.Client;
using ModelContextProtocol.Protocol;
using ReviewAgent.McpServer.Errors;

namespace ReviewAgent.McpServer.Tests.Tools;

/// <summary>
/// Runs the MCP server in memory and calls its tools through a real MCP client, the way the agent will,
/// on the sample pull request.
/// </summary>
public sealed class PullRequestToolsTests(WebApplicationFactory<Program> factory)
    : IClassFixture<WebApplicationFactory<Program>>
{
    // The header the agent sends; written out here because it is the server's public contract.
    private const string PullRequestHeader = "X-Review-Pull-Request";
    private const string SamplePullRequest = "Lukacs93/review-agent-demo/1";

    // The second sample, whose rules include business rules with a template.
    private const string BusinessRulesPullRequest = "Lukacs93/review-agent-demo/2";

    [Fact]
    public async Task Server_offers_the_review_tools()
    {
        await using var client = await ConnectAsync(SamplePullRequest);

        var tools = await client.ListToolsAsync(cancellationToken: CancellationToken.None);

        string[] expectedTools = ["get_diff", "get_file", "get_pull_request", "get_team_rules"];
        Assert.Equal(expectedTools, tools.Select(tool => tool.Name).Order(StringComparer.Ordinal));
    }

    [Fact]
    public async Task Pull_request_lists_the_changed_files()
    {
        await using var client = await ConnectAsync(SamplePullRequest);

        var result = await CallAsync(client, "get_pull_request");

        AssertSucceeded(result);
        var text = TextOf(result);
        Assert.Contains("Title: Add order discounts and shipping", text, StringComparison.Ordinal);
        Assert.Contains("- src/Orders.Api/Domain/Order.cs (Modified)", text, StringComparison.Ordinal);
        Assert.Contains("- src/Orders.Api/Application/Basket.cs (Added)", text, StringComparison.Ordinal);
    }

    [Fact]
    public async Task Diff_of_a_changed_file_is_returned()
    {
        await using var client = await ConnectAsync(SamplePullRequest);

        var result = await CallAsync(client, "get_diff", ("path", "src/Orders.Api/Domain/Order.cs"));

        AssertSucceeded(result);
        Assert.Contains("+    public void MarkAsShipped(DateTimeOffset now)", TextOf(result), StringComparison.Ordinal);
    }

    // The model reads the line of a comment from the diff instead of counting it.
    [Fact]
    public async Task Diff_lines_carry_their_line_number_in_the_new_file()
    {
        await using var client = await ConnectAsync(SamplePullRequest);

        var result = await CallAsync(client, "get_diff", ("path", "src/Orders.Api/Domain/Order.cs"));

        AssertSucceeded(result);
        var text = TextOf(result);
        Assert.Contains("13 | +    public decimal DiscountAmount { get; set; }", text, StringComparison.Ordinal);
        Assert.Contains("   | @@ -9,6 +9,8 @@", text, StringComparison.Ordinal);
    }

    [Fact]
    public async Task Diff_of_an_unchanged_file_is_an_error()
    {
        const string path = "src/Orders.Api/Domain/OrderLine.cs";
        await using var client = await ConnectAsync(SamplePullRequest);

        var result = await CallAsync(client, "get_diff", ("path", path));

        Assert.True(result.IsError);
        Assert.Contains(ToolErrors.FileNotChanged(path), TextOf(result), StringComparison.Ordinal);
    }

    [Fact]
    public async Task Any_file_of_the_repository_can_be_read()
    {
        await using var client = await ConnectAsync(SamplePullRequest);

        var result = await CallAsync(client, "get_file", ("path", "tests/Orders.Domain.Tests/OrderTests.cs"));

        AssertSucceeded(result);
        Assert.Contains("class OrderTests", TextOf(result), StringComparison.Ordinal);
    }

    [Fact]
    public async Task File_lines_carry_their_line_number()
    {
        await using var client = await ConnectAsync(SamplePullRequest);

        var result = await CallAsync(client, "get_file", ("path", "src/Orders.Api/Domain/Order.cs"));

        AssertSucceeded(result);
        var text = TextOf(result);
        Assert.StartsWith(" 1 | namespace Orders.Api.Domain;", text, StringComparison.Ordinal);
        Assert.Contains("13 |     public decimal DiscountAmount { get; set; }", text, StringComparison.Ordinal);
    }

    [Fact]
    public async Task Missing_file_is_an_error()
    {
        const string path = "src/Missing.cs";
        await using var client = await ConnectAsync(SamplePullRequest);

        var result = await CallAsync(client, "get_file", ("path", path));

        Assert.True(result.IsError);
        Assert.Contains(ToolErrors.FileNotFound(path), TextOf(result), StringComparison.Ordinal);
    }

    [Fact]
    public async Task File_outside_the_repository_is_rejected()
    {
        await using var client = await ConnectAsync(SamplePullRequest);

        var result = await CallAsync(client, "get_file", ("path", "../pull-request.json"));

        Assert.True(result.IsError);
        Assert.Contains("outside the repository", TextOf(result), StringComparison.Ordinal);
    }

    [Fact]
    public async Task Team_rules_are_returned()
    {
        await using var client = await ConnectAsync(SamplePullRequest);

        var result = await CallAsync(client, "get_team_rules");

        AssertSucceeded(result);
        Assert.Contains("### ARCH-01", TextOf(result), StringComparison.Ordinal);
    }

    // The first sample's pull request still has the older rules, without SEC-01; the branch it merges into has it.
    [Fact]
    public async Task Team_rules_come_from_the_branch_the_pull_request_merges_into()
    {
        await using var client = await ConnectAsync(SamplePullRequest);

        var result = await CallAsync(client, "get_team_rules");

        AssertSucceeded(result);
        Assert.Contains("### SEC-01", TextOf(result), StringComparison.Ordinal);
    }

    [Fact]
    public async Task Without_the_pull_request_header_tools_fail()
    {
        await using var client = await ConnectAsync(pullRequest: null);

        var result = await CallAsync(client, "get_pull_request");

        Assert.True(result.IsError);
        Assert.Contains(ToolErrors.PullRequestHeaderMissing, TextOf(result), StringComparison.Ordinal);
    }

    [Fact]
    public async Task Invalid_pull_request_header_is_rejected()
    {
        const string invalid = "../review-agent-demo/1";
        await using var client = await ConnectAsync(invalid);

        var result = await CallAsync(client, "get_pull_request");

        Assert.True(result.IsError);
        Assert.Contains(ToolErrors.PullRequestHeaderInvalid(invalid), TextOf(result), StringComparison.Ordinal);
    }

    [Fact]
    public async Task Business_rules_are_among_the_team_rules()
    {
        await using var client = await ConnectAsync(BusinessRulesPullRequest);

        var result = await CallAsync(client, "get_team_rules");

        AssertSucceeded(result);
        var text = TextOf(result);
        Assert.Contains("### SEC-01", text, StringComparison.Ordinal);
        Assert.Contains("### NAME-01", text, StringComparison.Ordinal);
        Assert.Contains("### BIZ-01", text, StringComparison.Ordinal);
        Assert.Contains(
            ".OrderByDescending(order => tiers.IsPriority(order.CustomerId))",
            text,
            StringComparison.Ordinal);
    }

    private async Task<McpClient> ConnectAsync(string? pullRequest)
    {
        var httpClient = factory.CreateClient();
        var headers = new Dictionary<string, string>();
        if (pullRequest is not null)
        {
            headers[PullRequestHeader] = pullRequest;
        }

        var transport = new HttpClientTransport(
            new HttpClientTransportOptions
            {
                Endpoint = new Uri(httpClient.BaseAddress!, "/mcp"),
                TransportMode = HttpTransportMode.StreamableHttp,
                AdditionalHeaders = headers,
            },
            httpClient,
            ownsHttpClient: true);

        return await McpClient.CreateAsync(transport, cancellationToken: CancellationToken.None);
    }

    private static async Task<CallToolResult> CallAsync(
        McpClient client,
        string tool,
        params (string Name, object? Value)[] arguments) =>
        await client.CallToolAsync(
            tool,
            arguments.ToDictionary(argument => argument.Name, argument => argument.Value),
            cancellationToken: CancellationToken.None);

    // The protocol leaves isError out when a call succeeds, so a missing value means success.
    private static void AssertSucceeded(CallToolResult result) => Assert.False(result.IsError ?? false);

    // An error's text includes the tool's message, possibly after a prefix the SDK adds.
    private static string TextOf(CallToolResult result) =>
        Assert.Single(result.Content.OfType<TextContentBlock>()).Text;
}
