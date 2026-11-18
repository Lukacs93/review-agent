namespace ReviewAgent.Api.Reviews;

/// <summary>
/// The URL paths of the review endpoints, in one place. They are part of the API's public contract:
/// the UI calls them, so a change here needs the same change in the UI.
/// </summary>
internal static class ReviewRoutes
{
    /// <summary>The group every review endpoint lives under.</summary>
    public const string Reviews = "/api/reviews";

    public const string StartReview = "/";
    public const string GetReviewRun = "/{runId:guid}";
    public const string RequestPublishing = "/{runId:guid}/publish";

    /// <summary>The group of the decisions on one comment.</summary>
    public const string Comment = "/{runId:guid}/comments/{commentId:guid}";

    public const string ApproveComment = "/approve";
    public const string EditComment = "/edit";
    public const string DiscardComment = "/discard";

    /// <summary>The name of the endpoint that returns one run; the Location header of a started run points to it.</summary>
    public const string GetReviewRunName = "GetReviewRun";
}
