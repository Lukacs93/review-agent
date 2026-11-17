namespace ReviewAgent.Application.Reviews;

/// <summary>
/// The names of our own HTTP headers, like ASP.NET Core's <c>HeaderNames</c>. Both the agent and the MCP server
/// use them, so a typo cannot break the connection silently.
/// </summary>
public static class ReviewAgentHeaderNames
{
    /// <summary>
    /// The pull request under review, sent by the agent to the MCP server. The agent's code sets it, not the
    /// model, so the model's tools can only read this one pull request. Its value: see
    /// <see cref="ReviewedPullRequestHeaderValue"/>.
    /// </summary>
    public const string ReviewedPullRequest = "X-Review-Pull-Request";
}
