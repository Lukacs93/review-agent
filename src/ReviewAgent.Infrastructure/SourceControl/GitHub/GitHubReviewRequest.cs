using System.Globalization;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using ReviewAgent.Application.Reviews.Publishing;

namespace ReviewAgent.Infrastructure.SourceControl.GitHub;

/// <summary>
/// The body of <c>POST /repos/{owner}/{repo}/pulls/{number}/reviews</c>: one review with the summary and every
/// comment, created by GitHub in one step. The summary carries a hidden marker with the run's ID, so the review
/// can be found on the pull request later.
/// </summary>
internal sealed record GitHubReviewRequest(
    string? CommitId,
    string Body,
    string Event,
    IReadOnlyList<GitHubReviewCommentRequest> Comments)
{
    // A review that only comments: it neither approves nor requests changes, so the decision stays with people.
    private const string CommentOnlyEvent = "COMMENT";

    // The comment is on the line of the new version of the file, as the agent saw it in the diff.
    private const string NewVersionSide = "RIGHT";

    /// <summary>GitHub's field names: <c>commit_id</c>, <c>event</c>; a missing commit is left out.</summary>
    public static JsonSerializerOptions JsonOptions { get; } = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseLower,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
    };

    public static GitHubReviewRequest From(ReviewPublication publication)
    {
        ArgumentNullException.ThrowIfNull(publication);

        return new GitHubReviewRequest(
            publication.ReviewedCommitSha,
            $"{publication.Summary}\n\n{MarkerOf(publication.RunId)}",
            CommentOnlyEvent,
            publication.Comments.Select(ToComment).ToList());
    }

    /// <summary>An HTML comment: GitHub does not show it, but returns it in the review's body.</summary>
    // Stored data, like the event names: the reviews already published carry this exact form, so changing it would
    // make them unrecognisable, and a retried publishing would publish them again.
    public static string MarkerOf(Guid runId) =>
        $"<!-- review-agent:run={runId.ToString("D", CultureInfo.InvariantCulture)} -->";

    private static GitHubReviewCommentRequest ToComment(CommentToPublish comment)
    {
        var body = new StringBuilder($"**{comment.Rule}**: {comment.Body}");
        if (comment.ReplacementCode is not null)
        {
            body.Append("\n\n").Append(SuggestionBlock.Build(comment.ReplacementCode));
        }
        else if (!string.IsNullOrWhiteSpace(comment.SuggestedFix))
        {
            body.Append("\n\nSuggested fix: ").Append(comment.SuggestedFix);
        }

        return new GitHubReviewCommentRequest(comment.FilePath, comment.Line, NewVersionSide, body.ToString());
    }
}

/// <summary>One comment of <see cref="GitHubReviewRequest"/>, on a line of a changed file.</summary>
internal sealed record GitHubReviewCommentRequest(string Path, int Line, string Side, string Body);
