using System.Text.Json;
using Microsoft.Extensions.AI;
using ReviewAgent.Infrastructure.Agents.Tools.Errors;

namespace ReviewAgent.Infrastructure.Agents.Tools;

/// <summary>
/// Answers a tool call whose arguments do not fit the tool's parameters (a required one missing, one of the wrong
/// type) with what is wrong, instead of failing it, so the model can correct the call. The tool itself is not run.
/// </summary>
/// <remarks>
/// As the MCP specification says for tools: input validation errors are tool results the model can learn from,
/// while other errors stay hidden from it. The tools check the content of their arguments themselves and answer
/// with the problem; only the arguments' binding to the parameters fails with an exception, which ends here.
/// </remarks>
internal sealed class ArgumentErrorReportingFunction(AIFunction innerFunction) : DelegatingAIFunction(innerFunction)
{
    protected override async ValueTask<object?> InvokeCoreAsync(
        AIFunctionArguments arguments,
        CancellationToken cancellationToken)
    {
        try
        {
            return await base.InvokeCoreAsync(arguments, cancellationToken);
        }
        catch (Exception exception) when (exception is ArgumentException or JsonException)
        {
            return ToolCallErrors.ArgumentsDoNotFit(Name, exception.Message);
        }
    }
}
