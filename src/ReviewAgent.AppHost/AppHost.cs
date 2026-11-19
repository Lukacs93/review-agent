// Local development orchestration: one start runs the MCP server and the API,
// and the Aspire dashboard shows their logs, state and links.
var builder = DistributedApplication.CreateBuilder(args);

// The settings both programs need, given in one place so the two always agree. Each one is passed only when it is
// set in the AppHost's configuration ("Parameters" section: user secrets, appsettings or environment variables);
// otherwise the programs keep their own defaults (saved pull requests, public GitHub, .review/rules.md).
var pullRequestSource = OptionalParameter("pull-request-source");
var gitHubApiUrl = OptionalParameter("github-api-url");
var teamRulesPath = OptionalParameter("team-rules-path");
var gitHubReadToken = OptionalParameter("github-read-token", secret: true);
var gitHubWriteToken = OptionalParameter("github-write-token", secret: true);

var mcpServer = builder.AddProject<Projects.ReviewAgent_McpServer>("mcp-server")
    .WithHttpHealthCheck("/health");
PassSharedSettings(mcpServer);

var api = builder.AddProject<Projects.ReviewAgent_Api>("api")
    .WithEnvironment("Agent__McpServerUrl", mcpServer.GetEndpoint("http"))
    .WaitFor(mcpServer)
    .WithUrlForEndpoint("http", url =>
    {
        url.Url = "/swagger";
        url.DisplayText = "Swagger";
    });
PassSharedSettings(api);

// Only the API publishes, so only the API gets the token that can write.
Pass(api, "PullRequests__GitHub__WriteToken", gitHubWriteToken);

await builder.Build().RunAsync();

// Creates the parameter only when the AppHost's configuration has a value for it. Aspire asks for a parameter
// without a value on its dashboard and does not start the programs that use it until it gets one; that would
// block every start in saved mode, where the GitHub tokens are not needed.
IResourceBuilder<ParameterResource>? OptionalParameter(string name, bool secret = false) =>
    string.IsNullOrEmpty(builder.Configuration["Parameters:" + name])
        ? null
        : builder.AddParameter(name, secret);

void PassSharedSettings(IResourceBuilder<ProjectResource> project)
{
    Pass(project, "PullRequests__Source", pullRequestSource);
    Pass(project, "PullRequests__GitHub__ApiUrl", gitHubApiUrl);
    Pass(project, "PullRequests__TeamRulesPath", teamRulesPath);
    Pass(project, "PullRequests__GitHub__ReadToken", gitHubReadToken);
}

// The environment variable overrides the program's own configuration ("__" stands for ":").
static void Pass(
    IResourceBuilder<ProjectResource> project,
    string environmentVariable,
    IResourceBuilder<ParameterResource>? parameter)
{
    if (parameter is not null)
    {
        project.WithEnvironment(environmentVariable, parameter);
    }
}
