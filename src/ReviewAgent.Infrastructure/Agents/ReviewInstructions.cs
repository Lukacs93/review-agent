using ReviewAgent.Infrastructure.Agents.Errors;

namespace ReviewAgent.Infrastructure.Agents;

/// <summary>
/// What the model is told, kept as text files in <c>Agents/Prompts</c> and built into the assembly, so they can
/// be read and edited as text and always ship with the code. The team's rules are not here: the model reads
/// them from the reviewed repository through a tool.
/// </summary>
internal static class ReviewInstructions
{
    public const string AgentName = "code-reviewer";

    private const string SystemPromptResource = "Prompts.review-agent-system.md";
    private const string TaskPromptResource = "Prompts.review-agent-task.md";

    private static readonly Lazy<string> _systemPrompt = new(() => Load(SystemPromptResource));
    private static readonly Lazy<string> _taskPrompt = new(() => Load(TaskPromptResource));

    /// <summary>The standing instructions: the agent's role, steps and rules. Sent as the system message.</summary>
    public static string SystemPrompt => _systemPrompt.Value;

    /// <summary>The task of one review. Sent as the first user message.</summary>
    public static string TaskPrompt => _taskPrompt.Value;

    private static string Load(string resourceName)
    {
        using var stream = typeof(ReviewInstructions).Assembly.GetManifestResourceStream(resourceName)
            ?? throw new InvalidOperationException(AgentErrors.PromptMissing(resourceName));
        using var reader = new StreamReader(stream);
        return reader.ReadToEnd().Trim();
    }
}
