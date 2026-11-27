using System.Globalization;
using Microsoft.Extensions.Configuration;

namespace ReviewAgent.Evaluation.Measuring;

/// <summary>
/// The model and the agent's settings that change its results. Recorded when a measurement starts, given to the
/// API the evaluation starts, and given again when the measurement continues, so every run uses the same ones.
/// </summary>
internal sealed record AgentSettings(
    Uri Endpoint,
    string Model,
    bool VerifierEnabled,
    int MaxToolRounds,
    TimeSpan RequestTimeout,
    TimeSpan Timeout,
    int? RequestsPerMinute)
{
    private const string AgentSection = "Agent";

    /// <summary>
    /// The settings the API would run with: its <c>appsettings.json</c>, overridden by environment variables
    /// (<c>Agent__Model</c> and the others), as in the API. The model's key is not read: the API reads it itself.
    /// </summary>
    public static AgentSettings Read(string repositoryRoot)
    {
        var agent = new ConfigurationBuilder()
            .AddJsonFile(Path.Combine(repositoryRoot, "src", "ReviewAgent.Api", "appsettings.json"), optional: false)
            .AddEnvironmentVariables()
            .Build()
            .GetSection(AgentSection);

        return new AgentSettings(
            agent.GetValue<Uri>("Endpoint") ?? throw SettingMissing("Endpoint"),
            agent.GetValue<string>("Model") ?? throw SettingMissing("Model"),
            agent.GetValue<bool>("Verifier:Enabled"),
            agent.GetValue<int>("MaxToolRounds"),
            agent.GetValue<TimeSpan>("RequestTimeout"),
            agent.GetValue<TimeSpan>("Timeout"),
            agent.GetValue<int?>("RequestsPerMinute"));
    }

    private static InvalidOperationException SettingMissing(string key) =>
        new(EvaluationErrors.SettingMissing($"{AgentSection}:{key}"));

    /// <summary>The environment variables that give these settings to the API.</summary>
    public IReadOnlyDictionary<string, string> ToEnvironment() => new Dictionary<string, string>
    {
        ["Agent__Endpoint"] = Endpoint.ToString(),
        ["Agent__Model"] = Model,
        ["Agent__Verifier__Enabled"] = VerifierEnabled ? "true" : "false",
        ["Agent__MaxToolRounds"] = MaxToolRounds.ToString(CultureInfo.InvariantCulture),
        ["Agent__RequestTimeout"] = RequestTimeout.ToString("c", CultureInfo.InvariantCulture),
        ["Agent__Timeout"] = Timeout.ToString("c", CultureInfo.InvariantCulture),

        // Empty when the measurement started without a limit, so the API's own setting does not put one back.
        ["Agent__RequestsPerMinute"] = RequestsPerMinute?.ToString(CultureInfo.InvariantCulture) ?? string.Empty,
    };
}
