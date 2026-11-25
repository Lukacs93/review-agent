using Microsoft.AspNetCore.Http.HttpResults;
using ReviewAgent.Application.Interfaces;
using ReviewAgent.Application.Reviews.Commands.ApproveComment;
using ReviewAgent.Application.Reviews.Commands.DiscardComment;
using ReviewAgent.Application.Reviews.Commands.EditComment;
using ReviewAgent.Application.Reviews.Commands.RequestPublishing;
using ReviewAgent.Application.Reviews.Commands.StartReview;
using ReviewAgent.Application.Reviews.Queries.GetReviewRun;
using ReviewAgent.Application.Reviews.Queries.GetRunLog;

namespace ReviewAgent.Api.Reviews;

/// <summary>
/// The HTTP endpoints of the review workflow. Each one turns the request into a command or query and calls
/// its handler; errors become problem details in <see cref="Errors.ApplicationExceptionHandler"/>.
/// </summary>
internal static class ReviewEndpoints
{
    public static IEndpointRouteBuilder MapReviewEndpoints(this IEndpointRouteBuilder endpoints)
    {
        var reviews = endpoints.MapGroup(ReviewRoutes.Reviews).WithTags("Reviews");

        reviews.MapPost(ReviewRoutes.StartReview, StartReviewAsync);
        reviews.MapGet(ReviewRoutes.GetReviewRun, GetReviewRunAsync).WithName(ReviewRoutes.GetReviewRunName);
        reviews.MapGet(ReviewRoutes.GetRunLog, GetRunLogAsync);
        reviews.MapPost(ReviewRoutes.RequestPublishing, RequestPublishingAsync);

        var comment = reviews.MapGroup(ReviewRoutes.Comment);
        comment.MapPost(ReviewRoutes.ApproveComment, ApproveCommentAsync);
        comment.MapPost(ReviewRoutes.EditComment, EditCommentAsync);
        comment.MapPost(ReviewRoutes.DiscardComment, DiscardCommentAsync);

        return endpoints;
    }

    private static async Task<CreatedAtRoute<StartReviewResponse>> StartReviewAsync(
        StartReviewRequest request,
        ICommandHandler<StartReviewCommand, Guid> handler,
        CancellationToken cancellationToken)
    {
        var command = new StartReviewCommand(
            request.RepositoryOwner,
            request.RepositoryName,
            request.PullRequestNumber);
        var runId = await handler.HandleAsync(command, cancellationToken);

        return TypedResults.CreatedAtRoute(
            new StartReviewResponse(runId),
            ReviewRoutes.GetReviewRunName,
            new { runId });
    }

    private static async Task<Results<Ok<ReviewRunDetails>, NotFound>> GetReviewRunAsync(
        Guid runId,
        IQueryHandler<GetReviewRunQuery, ReviewRunDetails?> handler,
        CancellationToken cancellationToken)
    {
        var details = await handler.HandleAsync(new GetReviewRunQuery(runId), cancellationToken);

        return details is null ? TypedResults.NotFound() : TypedResults.Ok(details);
    }

    // The log lines written while the run was running, oldest first; an empty list when there are none.
    private static async Task<Results<Ok<IReadOnlyList<RunLogEntryDetails>>, NotFound>> GetRunLogAsync(
        Guid runId,
        IQueryHandler<GetRunLogQuery, IReadOnlyList<RunLogEntryDetails>?> handler,
        CancellationToken cancellationToken)
    {
        var lines = await handler.HandleAsync(new GetRunLogQuery(runId), cancellationToken);

        return lines is null ? TypedResults.NotFound() : TypedResults.Ok(lines);
    }

    // 202 Accepted: publishing runs in the background; the run shows when it is done, or why it failed.
    private static async Task<AcceptedAtRoute> RequestPublishingAsync(
        Guid runId,
        ICommandHandler<RequestPublishingCommand> handler,
        CancellationToken cancellationToken)
    {
        await handler.HandleAsync(new RequestPublishingCommand(runId), cancellationToken);
        return TypedResults.AcceptedAtRoute(ReviewRoutes.GetReviewRunName, new { runId });
    }

    private static async Task<NoContent> ApproveCommentAsync(
        Guid runId,
        Guid commentId,
        ICommandHandler<ApproveCommentCommand> handler,
        CancellationToken cancellationToken)
    {
        await handler.HandleAsync(new ApproveCommentCommand(runId, commentId), cancellationToken);
        return TypedResults.NoContent();
    }

    private static async Task<NoContent> EditCommentAsync(
        Guid runId,
        Guid commentId,
        EditCommentRequest request,
        ICommandHandler<EditCommentCommand> handler,
        CancellationToken cancellationToken)
    {
        await handler.HandleAsync(
            new EditCommentCommand(runId, commentId, request.Body, request.ReplacementCode),
            cancellationToken);
        return TypedResults.NoContent();
    }

    private static async Task<NoContent> DiscardCommentAsync(
        Guid runId,
        Guid commentId,
        ICommandHandler<DiscardCommentCommand> handler,
        CancellationToken cancellationToken)
    {
        await handler.HandleAsync(new DiscardCommentCommand(runId, commentId), cancellationToken);
        return TypedResults.NoContent();
    }
}
