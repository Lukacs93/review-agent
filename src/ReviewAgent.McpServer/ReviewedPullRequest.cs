using ModelContextProtocol;
using ReviewAgent.Application.Reviews;
using ReviewAgent.Domain.Reviews;
using ReviewAgent.McpServer.Errors;

namespace ReviewAgent.McpServer;

/// <summary>
/// The pull request under review. The agent sets it in a header when it connects, and the tools take no
/// repository or pull request arguments: the model can read this pull request only, never another repository.
/// </summary>
internal sealed class ReviewedPullRequest(IHttpContextAccessor httpContextAccessor)
{
    public PullRequestRef Get()
    {
        var headers = httpContextAccessor.HttpContext?.Request.Headers;
        var value = headers?[ReviewAgentHeaderNames.ReviewedPullRequest].ToString();
        if (string.IsNullOrWhiteSpace(value))
        {
            throw new McpException(ToolErrors.PullRequestHeaderMissing);
        }

        return ReviewedPullRequestHeaderValue.ParseOrDefault(value)
            ?? throw new McpException(ToolErrors.PullRequestHeaderInvalid(value));
    }
}
