namespace ReviewAgent.Domain.Reviews;

/// <summary>
/// What a failure came from: the agent's own result, or the services it depends on. An evaluation counts the
/// agent's failures as results, and stops on the others, which say nothing about the agent.
/// </summary>
public enum ReviewFailureKind
{
    /// <summary>The model's API answered with an error or could not be reached, after the retries.</summary>
    ModelService,

    /// <summary>
    /// Anything else outside the agent: the pull request's host, the MCP server, the team's rules file, a stopped
    /// application or an error in the code.
    /// </summary>
    Infrastructure,

    /// <summary>The agent's own result: no summary, or no answer within the run's time limit.</summary>
    Agent,
}
