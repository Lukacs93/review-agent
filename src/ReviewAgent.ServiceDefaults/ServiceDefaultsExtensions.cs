using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using OpenTelemetry;
using OpenTelemetry.Metrics;
using OpenTelemetry.Trace;

namespace ReviewAgent.ServiceDefaults;

/// <summary>
/// What the API and the MCP server set up the same way: OpenTelemetry logs, traces and metrics. Modelled on the
/// Aspire service defaults template.
/// </summary>
// Unlike the template, it adds neither a resilience handler nor service discovery to every HTTP client: each client
// here has its own deliberate handling (long timeouts for the model, no retried writes to GitHub, no retries for MCP
// calls), and a second, general handler would retry on top of it.
public static class ServiceDefaultsExtensions
{
    // The standard OpenTelemetry environment variable; Aspire sets it for the programs it starts.
    private const string OtlpEndpointVariable = "OTEL_EXPORTER_OTLP_ENDPOINT";

    // The health checks are called all the time and would fill the traces.
    private const string HealthPath = "/health";

    /// <summary>
    /// Sets up OpenTelemetry. <paramref name="telemetrySources"/> are the program's own sources of traces and
    /// metrics, for example the agents'. Sends everything over OTLP when the endpoint is configured; otherwise the
    /// logs still go to the console.
    /// </summary>
    public static TBuilder AddServiceDefaults<TBuilder>(this TBuilder builder, params string[] telemetrySources)
        where TBuilder : IHostApplicationBuilder
    {
        ArgumentNullException.ThrowIfNull(builder);
        ArgumentNullException.ThrowIfNull(telemetrySources);

        // Scopes carry values like the review run's ID into every log line written inside them.
        builder.Logging.AddOpenTelemetry(logging =>
        {
            logging.IncludeFormattedMessage = true;
            logging.IncludeScopes = true;
        });

        builder.Services.AddOpenTelemetry()
            .WithMetrics(metrics => metrics
                .AddAspNetCoreInstrumentation()
                .AddHttpClientInstrumentation()
                .AddRuntimeInstrumentation()
                .AddMeter(telemetrySources))
            .WithTracing(tracing => tracing
                .AddSource(builder.Environment.ApplicationName)
                .AddSource(telemetrySources)
                .AddAspNetCoreInstrumentation(aspNetCore =>
                    aspNetCore.Filter = context => !context.Request.Path.StartsWithSegments(HealthPath))
                .AddHttpClientInstrumentation());

        if (!string.IsNullOrWhiteSpace(builder.Configuration[OtlpEndpointVariable]))
        {
            builder.Services.AddOpenTelemetry().UseOtlpExporter();
        }

        return builder;
    }
}
