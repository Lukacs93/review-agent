using ReviewAgent.Infrastructure;
using ReviewAgent.McpServer;
using ReviewAgent.McpServer.Tools;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddHttpContextAccessor();
// Only the read-only part of the Infrastructure layer: the pull request sources and the team rules setting. The
// server never publishes, so it gets neither the publishers nor the write token.
builder.Services.AddSourceControl();
builder.Services.AddScoped<ReviewedPullRequest>();
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
