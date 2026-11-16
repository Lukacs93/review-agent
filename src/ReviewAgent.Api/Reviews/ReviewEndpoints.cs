using Microsoft.AspNetCore.Http.HttpResults;
using ReviewAgent.Application.Interfaces;
using ReviewAgent.Application.Reviews.Commands;
using ReviewAgent.Application.Reviews.Queries;

namespace ReviewAgent.Api.Reviews;

/// <summary>
/// The HTTP endpoints of the review workflow. Each one turns the request into a command or query and calls
/// its handler; errors become problem details in <see cref="Errors.ReviewExceptionHandler"/>.
/// </summary>
internal static class ReviewEndpoints
{
    public static IEndpointRouteBuilder MapReviewEndpoints(this IEndpointRouteBuilder endpoints)
    {
        var reviews = endpoints.MapGroup(ReviewRoutes.Reviews).WithTags("Reviews");

        reviews.MapPost(ReviewRoutes.StartReview, StartReviewAsync);
        reviews.MapGet(ReviewRoutes.GetReviewRun, GetReviewRunAsync).WithName(ReviewRoutes.GetReviewRunName);

        var comment = reviews.MapGroup(ReviewRoutes.Comment);
        comment.MapPost(ReviewRoutes.ApproveComment, ApproveCommentAsync);
        comment.MapPost(ReviewRoutes.EditComment, EditCommentAsync);
        comment.MapPost(ReviewRoutes.DiscardComment, DiscardCommentAsync);

        return endpoints;
    }

    private static async Task<CreatedAtRoute<StartReviewResponse>> StartReviewAsync(
        StartReviewRequest request,
        ICommandHandler<StartReview, Guid> handler,
        CancellationToken cancellationToken)
    {
        var command = new StartReview(request.RepositoryOwner, request.RepositoryName, request.PullRequestNumber);
        var runId = await handler.HandleAsync(command, cancellationToken);

        return TypedResults.CreatedAtRoute(
            new StartReviewResponse(runId),
            ReviewRoutes.GetReviewRunName,
            new { runId });
    }

    private static async Task<Results<Ok<ReviewRunDetails>, NotFound>> GetReviewRunAsync(
        Guid runId,
        IQueryHandler<GetReviewRun, ReviewRunDetails?> handler,
        CancellationToken cancellationToken)
    {
        var details = await handler.HandleAsync(new GetReviewRun(runId), cancellationToken);

        return details is null ? TypedResults.NotFound() : TypedResults.Ok(details);
    }

    private static async Task<NoContent> ApproveCommentAsync(
        Guid runId,
        Guid commentId,
        ICommandHandler<ApproveComment> handler,
        CancellationToken cancellationToken)
    {
        await handler.HandleAsync(new ApproveComment(runId, commentId), cancellationToken);
        return TypedResults.NoContent();
    }

    private static async Task<NoContent> EditCommentAsync(
        Guid runId,
        Guid commentId,
        EditCommentRequest request,
        ICommandHandler<EditComment> handler,
        CancellationToken cancellationToken)
    {
        await handler.HandleAsync(new EditComment(runId, commentId, request.Body), cancellationToken);
        return TypedResults.NoContent();
    }

    private static async Task<NoContent> DiscardCommentAsync(
        Guid runId,
        Guid commentId,
        ICommandHandler<DiscardComment> handler,
        CancellationToken cancellationToken)
    {
        await handler.HandleAsync(new DiscardComment(runId, commentId), cancellationToken);
        return TypedResults.NoContent();
    }
}
