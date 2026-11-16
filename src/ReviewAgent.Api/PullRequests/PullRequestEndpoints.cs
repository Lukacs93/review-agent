using Microsoft.AspNetCore.Http.HttpResults;
using ReviewAgent.Application.Interfaces;
using ReviewAgent.Application.SourceControl;
using ReviewAgent.Application.SourceControl.Queries;

namespace ReviewAgent.Api.PullRequests;

/// <summary>
/// Shows a pull request as the agent will see it, from the configured source. Lets you check the source by hand,
/// and the UI uses it to show the diffs next to the comments.
/// </summary>
internal static class PullRequestEndpoints
{
    public static IEndpointRouteBuilder MapPullRequestEndpoints(this IEndpointRouteBuilder endpoints)
    {
        endpoints.MapGroup(PullRequestRoutes.PullRequests)
            .WithTags("Pull requests")
            .MapGet(PullRequestRoutes.GetPullRequest, GetPullRequestAsync);

        return endpoints;
    }

    private static async Task<Ok<PullRequestSnapshot>> GetPullRequestAsync(
        string repositoryOwner,
        string repositoryName,
        int pullRequestNumber,
        IQueryHandler<GetPullRequest, PullRequestSnapshot> handler,
        CancellationToken cancellationToken)
    {
        var query = new GetPullRequest(repositoryOwner, repositoryName, pullRequestNumber);
        return TypedResults.Ok(await handler.HandleAsync(query, cancellationToken));
    }
}
