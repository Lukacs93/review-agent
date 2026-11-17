using System.Globalization;
using ReviewAgent.Domain;
using ReviewAgent.Domain.Reviews;

namespace ReviewAgent.Application.Reviews.Agent;

/// <summary>
/// The value of the <see cref="ReviewAgentHeaderNames.ReviewedPullRequest"/> header: writing it (the agent) and
/// reading it (the MCP server) in one place, like ASP.NET Core's <c>MediaTypeHeaderValue</c>.
/// </summary>
public static class ReviewedPullRequestHeaderValue
{
    /// <summary>
    /// The header value: <c>owner/repository/number</c>, for example <c>Lukacs93/review-agent-demo/1</c>.
    /// </summary>
    public static string Format(PullRequestRef pullRequest)
    {
        ArgumentNullException.ThrowIfNull(pullRequest);

        return string.Create(
            CultureInfo.InvariantCulture,
            $"{pullRequest.Owner}/{pullRequest.Repository}/{pullRequest.Number}");
    }

    /// <summary>
    /// Reads a header value written by <see cref="Format"/>. Returns <see langword="null"/> when the value is not
    /// <c>owner/repository/number</c> or the names break GitHub's naming rules.
    /// </summary>
    public static PullRequestRef? ParseOrDefault(string? value)
    {
        if (value?.Split('/') is not [var owner, var repository, var numberText]
            || !int.TryParse(numberText, NumberStyles.None, CultureInfo.InvariantCulture, out var number))
        {
            return null;
        }

        try
        {
            return new PullRequestRef(owner, repository, number);
        }
        catch (DomainException)
        {
            return null;
        }
    }
}
