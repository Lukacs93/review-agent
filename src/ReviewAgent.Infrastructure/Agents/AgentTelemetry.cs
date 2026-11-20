using System.Diagnostics;

namespace ReviewAgent.Infrastructure.Agents;

/// <summary>The agents' traces, under <see cref="DependencyInjection.TelemetrySourceName"/>.</summary>
internal static class AgentTelemetry
{
    /// <summary>The name of the span that covers one review run, with the reviewer and the verifier in it.</summary>
    public const string ReviewActivityName = "review";

    /// <summary>The review run's ID on the span, so the trace of a run can be found by it.</summary>
    public const string RunIdTag = "review.run_id";

    public static readonly ActivitySource Source = new(DependencyInjection.TelemetrySourceName);
}
