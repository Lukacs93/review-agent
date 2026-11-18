using System.Globalization;

namespace ReviewAgent.Infrastructure.Agents.Errors;

/// <summary>
/// Configuration errors, and the failure reasons a review run is failed with. The reasons are shown to the
/// developer, so they say what to do.
/// </summary>
internal static class AgentErrors
{
    public const string EndpointRequired = "Agent:Endpoint is required: the base URL of an OpenAI-compatible API.";

    public const string ModelRequired = "Agent:Model is required: the name of the model the agent uses.";

    public const string McpServerUrlRequired = "Agent:McpServerUrl is required: the base address of the MCP server.";

    public const string TeamRulesPathRequired = "Agent:TeamRulesPath is required: where the team keeps its rules.";

    public const string MaxToolRoundsOutOfRange = "Agent:MaxToolRounds must be between 1 and 200.";

    public const string TimeoutOutOfRange = "Agent:Timeout must be between 10 seconds and 1 hour.";

    public const string RequestTimeoutOutOfRange = "Agent:RequestTimeout must be between 10 seconds and 30 minutes.";

    public const string RequestTimeoutNotShorter =
        "Agent:RequestTimeout must be shorter than Agent:Timeout: one model request is only a part of a review.";

    public const string ApiKeyMissing =
        "The model's API key is not set. Set Agent:ApiKey in User Secrets (development) "
        + "or in an environment variable (production), then start the review again.";

    public const string NoSummary =
        "The agent stopped without a summary, possibly because it reached the tool call limit (Agent:MaxToolRounds).";

    public const string UnreadableDiff =
        "A file's diff has no block header (\"@@ -a,b +c,d @@\"), so its line numbers cannot be read. "
        + "The pull request source returned a diff in an unexpected format.";

    public static string PromptMissing(string resourceName) =>
        $"The prompt '{resourceName}' is not built into the application. Check the EmbeddedResource item in "
        + "ReviewAgent.Infrastructure.csproj.";

    public static string PlaceholderMissing(string resourceName, string placeholder) =>
        $"The prompt '{resourceName}' has no '{placeholder}' placeholder, so the agent would get its task without "
        + "the data it works on. Put the placeholder back into the prompt file.";

    public static string VerificationFailed(string reason) =>
        $"Note: the comments were not double-checked, because the verification failed: {reason}";

    public static string TeamRulesNotFound(string path) =>
        $"The repository has no team rules file at '{path}', so there is nothing to review against.";

    public static string TimedOut(TimeSpan timeout) =>
        string.Create(CultureInfo.InvariantCulture, $"The review did not finish within {timeout} (Agent:Timeout).");

    public static string Failed(string message) => $"The review failed: {message}";
}
