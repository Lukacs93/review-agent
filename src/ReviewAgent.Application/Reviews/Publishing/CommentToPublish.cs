using ReviewAgent.Domain.Reviews;

namespace ReviewAgent.Application.Reviews.Publishing;

/// <summary>An approved comment, with the text the developer approved: the proposed one or their edit.</summary>
public sealed record CommentToPublish(string FilePath, int Line, string Rule, string Body, string? SuggestedFix)
{
    public static CommentToPublish From(ReviewComment comment)
    {
        ArgumentNullException.ThrowIfNull(comment);

        return new CommentToPublish(
            comment.Location.FilePath,
            comment.Location.Line,
            comment.Rule.Value,
            comment.Body,
            comment.SuggestedFix);
    }
}
