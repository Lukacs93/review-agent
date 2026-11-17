namespace ReviewAgent.Application.Reviews.Commands;

/// <summary>
/// The agent called a tool. Recorded so the review shows every step and the tool calls can be measured.
/// </summary>
public sealed record RecordToolCall(Guid RunId, string ToolName, string? Arguments);
