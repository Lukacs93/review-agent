using System.Text.Json.Serialization;
using ReviewAgent.Api.Agent;
using ReviewAgent.Api.Errors;
using ReviewAgent.Api.Publishing;
using ReviewAgent.Api.PullRequests;
using ReviewAgent.Api.Reviews;
using ReviewAgent.Application;
using ReviewAgent.Infrastructure;
using ReviewAgent.ServiceDefaults;

var builder = WebApplication.CreateBuilder(args);

// The agents' traces and metrics (model calls, tool calls, review runs) come from the Infrastructure layer.
builder.AddServiceDefaults(ReviewAgent.Infrastructure.DependencyInjection.TelemetrySourceName);

builder.Services.AddOpenApi();
builder.Services.AddProblemDetails();
builder.Services.AddExceptionHandler<ApplicationExceptionHandler>();
builder.Services.AddHealthChecks();

// Enums travel as text ("AwaitingApproval"), not numbers, so the UI does not depend on their order.
builder.Services.ConfigureHttpJsonOptions(options =>
    options.SerializerOptions.Converters.Add(new JsonStringEnumConverter()));

builder.Services.AddApplication();
builder.Services.AddInfrastructure();
builder.Services.AddReviewWorker();
builder.Services.AddPublishingWorker();

var app = builder.Build();

if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();

    // Swagger UI at /swagger shows the description that MapOpenApi produces and lets you try every endpoint.
    app.UseSwaggerUI(options => options.SwaggerEndpoint("/openapi/v1.json", "Review Agent API"));
}

app.UseExceptionHandler();
app.UseStatusCodePages();

app.MapHealthChecks("/health");
app.MapReviewEndpoints();
app.MapPullRequestEndpoints();

await app.Services.MigrateDatabaseAsync(app.Logger, app.Lifetime.ApplicationStopping);
await app.Services.FailInterruptedReviewsAsync(app.Logger, app.Lifetime.ApplicationStopping);
await app.Services.ResumeInterruptedPublishingAsync(app.Logger, app.Lifetime.ApplicationStopping);
await app.RunAsync();
