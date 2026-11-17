// Local development orchestration: one start runs the MCP server and the API,
// and the Aspire dashboard shows their logs, state and links.
var builder = DistributedApplication.CreateBuilder(args);

var mcpServer = builder.AddProject<Projects.ReviewAgent_McpServer>("mcp-server")
    .WithHttpHealthCheck("/health");

builder.AddProject<Projects.ReviewAgent_Api>("api")
    .WithEnvironment("Agent__McpServerUrl", mcpServer.GetEndpoint("http"))
    .WaitFor(mcpServer)
    .WithUrlForEndpoint("http", url =>
    {
        url.Url = "/swagger";
        url.DisplayText = "Swagger";
    });

await builder.Build().RunAsync();
