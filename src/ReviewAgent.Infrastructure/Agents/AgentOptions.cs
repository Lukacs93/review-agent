using System.ComponentModel.DataAnnotations;
using ReviewAgent.Infrastructure.Agents.Errors;

namespace ReviewAgent.Infrastructure.Agents;

/// <summary>
/// The <c>Agent</c> section of the configuration: which model the agent uses and where its tools are.
/// Any service with an OpenAI-compatible chat completions API works, for example Gemini or OpenAI.
/// </summary>
/// <remarks>
/// The <see cref="RangeAttribute"/> limits are deliberately wide safety bounds, not tuning values. The values
/// themselves come from the configuration and are tuned there; the bounds only reject settings that make no
/// sense or would let a review run or cost without limit, so a mistyped setting cannot switch the protection off.
/// </remarks>
internal sealed class AgentOptions : IValidatableObject
{
    public const string SectionName = "Agent";

    /// <summary>The base URL of the OpenAI-compatible API, for example Gemini's <c>.../v1beta/openai/</c>.</summary>
    [Required(ErrorMessage = AgentErrors.EndpointRequired)]
    public Uri? Endpoint { get; init; }

    [Required(ErrorMessage = AgentErrors.ModelRequired)]
    public string Model { get; init; } = string.Empty;

    /// <summary>
    /// Never in a file that goes to git: User Secrets in development, an environment variable or a Kubernetes
    /// Secret in production. Not required at startup, so the API runs without it; a review then fails and says so.
    /// </summary>
    public string? ApiKey { get; init; }

    /// <summary>The base address of the MCP server; the agent connects to its <c>/mcp</c> endpoint.</summary>
    [Required(ErrorMessage = AgentErrors.McpServerUrlRequired)]
    public Uri? McpServerUrl { get; init; }

    /// <summary>Where the team keeps its review rules in the repository, relative to the repository root.</summary>
    [Required(ErrorMessage = AgentErrors.TeamRulesPathRequired)]
    public string TeamRulesPath { get; init; } = ".review/rules.md";

    /// <summary>How many times the model may ask for tool calls in one review before it is stopped.</summary>
    [Range(1, 200, ErrorMessage = AgentErrors.MaxToolRoundsOutOfRange)]
    public int MaxToolRounds { get; init; } = 40;

    /// <summary>
    /// How long one attempt of one model request may take. When it runs out, the request is tried again.
    /// </summary>
    [Range(typeof(TimeSpan), "00:00:10", "00:30:00", ErrorMessage = AgentErrors.RequestTimeoutOutOfRange)]
    public TimeSpan RequestTimeout { get; init; } = TimeSpan.FromMinutes(2);

    /// <summary>
    /// How long one review may take, with all its model requests and tool calls, before it is failed.
    /// </summary>
    [Range(typeof(TimeSpan), "00:00:10", "01:00:00", ErrorMessage = AgentErrors.TimeoutOutOfRange)]
    public TimeSpan Timeout { get; init; } = TimeSpan.FromMinutes(5);

    /// <summary>The verifier agent, which double-checks the proposed comments.</summary>
    public VerifierOptions Verifier { get; init; } = new();

    public IEnumerable<ValidationResult> Validate(ValidationContext validationContext)
    {
        if (RequestTimeout >= Timeout)
        {
            yield return new ValidationResult(
                AgentErrors.RequestTimeoutNotShorter,
                [nameof(RequestTimeout), nameof(Timeout)]);
        }
    }
}
