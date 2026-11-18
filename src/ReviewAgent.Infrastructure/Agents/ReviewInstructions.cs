using System.Globalization;
using ReviewAgent.Domain.Reviews;
using ReviewAgent.Infrastructure.Agents.Errors;

namespace ReviewAgent.Infrastructure.Agents;

/// <summary>
/// What the models are told, kept as text files in <c>Agents/Prompts</c> and built into the assembly, so they can
/// be read and edited as text and always ship with the code. The team's rules are not here: the models read
/// them from the reviewed repository through a tool.
/// </summary>
internal static class ReviewInstructions
{
    public const string ReviewerName = "code-reviewer";

    public const string VerifierName = "verifier";

    private const string SystemPromptResource = "Prompts.review-agent-system.md";
    private const string TaskPromptResource = "Prompts.review-agent-task.md";
    private const string VerifierSystemPromptResource = "Prompts.verifier-agent-system.md";
    private const string VerifierTaskPromptResource = "Prompts.verifier-agent-task.md";

    // Where the verifier's task prompt lists the proposed comments.
    private const string CommentsPlaceholder = "{comments}";

    private static readonly Lazy<string> _systemPrompt = new(() => Load(SystemPromptResource));
    private static readonly Lazy<string> _taskPrompt = new(() => Load(TaskPromptResource));
    private static readonly Lazy<string> _verifierSystemPrompt = new(() => Load(VerifierSystemPromptResource));
    private static readonly Lazy<string> _verifierTaskPrompt = new(() => Load(VerifierTaskPromptResource));

    /// <summary>The standing instructions: the agent's role, steps and rules. Sent as the system message.</summary>
    public static string SystemPrompt => _systemPrompt.Value;

    /// <summary>The task of one review. Sent as the first user message.</summary>
    public static string TaskPrompt => _taskPrompt.Value;

    /// <summary>The verifier agent's standing instructions. Sent as its system message.</summary>
    public static string VerifierSystemPrompt => _verifierSystemPrompt.Value;

    /// <summary>The verifier agent's task: the proposed comments to double-check, each with its ID.</summary>
    public static string VerifierTaskPrompt(IEnumerable<ReviewComment> comments)
    {
        ArgumentNullException.ThrowIfNull(comments);

        // Joined with '\n' rather than AppendLine, whose line ending depends on the operating system.
        var lines = comments.SelectMany(comment => new[]
        {
            string.Create(CultureInfo.InvariantCulture, $"- id: {comment.Id.Value}"),
            string.Create(
                CultureInfo.InvariantCulture,
                $"  file: {comment.Location.FilePath}, line: {comment.Location.Line}"),
            string.Create(CultureInfo.InvariantCulture, $"  rule: {comment.Rule}"),
            string.Create(CultureInfo.InvariantCulture, $"  comment: {comment.ProposedBody}"),
        });

        var commentList = string.Join('\n', lines);
        return FillPlaceholder(_verifierTaskPrompt.Value, VerifierTaskPromptResource, CommentsPlaceholder, commentList);
    }

    /// <summary>
    /// Puts the value in place of the placeholder. A prompt file edited without its placeholder fails here, instead
    /// of sending the task without its data.
    /// </summary>
    internal static string FillPlaceholder(string prompt, string resourceName, string placeholder, string value)
    {
        if (!prompt.Contains(placeholder, StringComparison.Ordinal))
        {
            throw new InvalidOperationException(AgentErrors.PlaceholderMissing(resourceName, placeholder));
        }

        return prompt.Replace(placeholder, value, StringComparison.Ordinal);
    }

    private static string Load(string resourceName)
    {
        using var stream = typeof(ReviewInstructions).Assembly.GetManifestResourceStream(resourceName)
            ?? throw new InvalidOperationException(AgentErrors.PromptMissing(resourceName));
        using var reader = new StreamReader(stream);
        return reader.ReadToEnd().Trim();
    }
}
