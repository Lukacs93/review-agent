using System.Globalization;
using ModelContextProtocol;
using ReviewAgent.Domain;
using ReviewAgent.Domain.Reviews;
using ReviewAgent.McpServer.Errors;

namespace ReviewAgent.McpServer;

/// <summary>
/// The pull request under review. The agent sets it in a header when it connects, and the tools take no
/// repository or pull request arguments: the model can read this pull request only, never another repository.
/// </summary>
internal sealed class ReviewedPullRequest(IHttpContextAccessor httpContextAccessor)
{
    /// <summary>The header that carries the pull request, as <c>owner/repository/number</c>.</summary>
    public const string HeaderName = "X-Review-Pull-Request";

    public PullRequestRef Get()
    {
        var value = httpContextAccessor.HttpContext?.Request.Headers[HeaderName].ToString();
        if (string.IsNullOrWhiteSpace(value))
        {
            throw new McpException(ToolErrors.PullRequestHeaderMissing);
        }

        // The value is "owner/repository/number", for example "Lukacs93/review-agent-demo/1".
        if (value.Split('/') is not [var owner, var repository, var numberText]
            || !int.TryParse(numberText, NumberStyles.None, CultureInfo.InvariantCulture, out var number))
        {
            throw new McpException(ToolErrors.PullRequestHeaderInvalid(value));
        }

        try
        {
            return new PullRequestRef(owner, repository, number);
        }
        catch (DomainException exception)
        {
            throw new McpException(ToolErrors.PullRequestHeaderInvalid(value), exception);
        }
    }
}
