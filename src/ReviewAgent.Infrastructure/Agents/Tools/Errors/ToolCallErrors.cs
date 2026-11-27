namespace ReviewAgent.Infrastructure.Agents.Tools.Errors;

/// <summary>What a tool answers when it cannot run a call; the model reads it, so it says what to correct.</summary>
internal static class ToolCallErrors
{
    public static string ArgumentsDoNotFit(string toolName, string reason) =>
        $"The call was not run: its arguments do not fit the parameters of {toolName} ({reason}). Call it again "
        + "with every required argument, each of the right type.";
}
