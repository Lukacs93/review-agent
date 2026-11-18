using ReviewAgent.Infrastructure;
using ReviewAgent.McpServer;
using ReviewAgent.McpServer.Tools;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddHttpContextAccessor();
builder.Services.AddSourceControl();
builder.Services.AddScoped<ReviewedPullRequest>();
// The "ReviewTools" section of the configuration, bound to ReviewToolsOptions and checked against its attributes
// when the server starts, so a missing setting stops it right away.
builder.Services.AddOptions<ReviewToolsOptions>()
    .BindConfiguration(ReviewToolsOptions.SectionName)
    .ValidateDataAnnotations()
    .ValidateOnStart();
builder.Services.AddHealthChecks();

// Stateless HTTP (the SDK's default): every tool call is its own HTTP request, so the tools can read the
// reviewed pull request from that request's headers.
builder.Services.AddMcpServer()
    .WithHttpTransport()
    .WithTools<PullRequestTools>();

var app = builder.Build();

app.MapHealthChecks("/health");
app.MapMcp("/mcp");

await app.RunAsync();
