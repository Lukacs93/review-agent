using System.Globalization;
using System.Net.Http.Json;
using Microsoft.Extensions.Logging;
using ReviewAgent.Application.Reviews.Publishing;
using ReviewAgent.Domain.Reviews;
using ReviewAgent.Infrastructure.SourceControl.GitHub.Errors;

namespace ReviewAgent.Infrastructure.SourceControl.GitHub;

/// <summary>
/// Publishes reviews through the GitHub REST API, as one review per run with the comments on their lines. The
/// <see cref="HttpClient"/> is set up like the pull request source's, but its token needs write access to pull
/// requests.
/// </summary>
internal sealed partial class GitHubReviewPublisher(HttpClient httpClient, ILogger<GitHubReviewPublisher> logger)
    : IReviewPublisher
{
    public async Task<bool> IsPublishedAsync(ReviewPublication publication, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(publication);

        var marker = GitHubReviewRequest.MarkerOf(publication.RunId);
        Uri? pageUri = ReviewsUri(publication.PullRequest);
        while (pageUri is not null)
        {
            using var response = await httpClient.GetAsync(pageUri, cancellationToken);
            await EnsureSuccessAsync(response, cancellationToken);

            var reviews = await response.Content.ReadFromJsonAsync<List<GitHubReview>>(cancellationToken) ?? [];
            if (reviews.Any(review => review.Body is not null
                && review.Body.Contains(marker, StringComparison.Ordinal)))
            {
                return true;
            }

            pageUri = GitHubPagination.NextPageUri(response, httpClient.BaseAddress!);
        }

        return false;
    }

    public async Task PublishAsync(ReviewPublication publication, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(publication);

        using var response = await httpClient.PostAsJsonAsync(
            ReviewsUri(publication.PullRequest),
            GitHubReviewRequest.From(publication),
            GitHubReviewRequest.JsonOptions,
            cancellationToken);
        await EnsureSuccessAsync(response, cancellationToken);

        var pullRequest = publication.PullRequest;
        LogPublished(
            logger,
            publication.RunId,
            pullRequest.Owner,
            pullRequest.Repository,
            pullRequest.Number,
            publication.Comments.Count);
    }

    /// <summary>
    /// Throws with GitHub's own error message when the request failed. The whole answer goes to the log, so the
    /// details can be looked up there.
    /// </summary>
    private async Task EnsureSuccessAsync(HttpResponseMessage response, CancellationToken cancellationToken)
    {
        if (response.IsSuccessStatusCode)
        {
            return;
        }

        var statusCode = (int)response.StatusCode;
        var answer = await response.Content.ReadAsStringAsync(cancellationToken);
        LogErrorAnswer(logger, statusCode, answer);

        var message = GitHubErrorAnswer.MessageOf(answer);
        var errorMessage = message is null
            ? GitHubErrors.RequestFailedWithoutMessage(statusCode)
            : GitHubErrors.RequestFailed(statusCode, message);
        throw new HttpRequestException(errorMessage, inner: null, response.StatusCode);
    }

    // Owner and repository are safe in a URL as they are: PullRequestRef only accepts GitHub-style names.
    private static Uri ReviewsUri(PullRequestRef pullRequest) =>
        new(
            string.Create(
                CultureInfo.InvariantCulture,
                $"repos/{pullRequest.Owner}/{pullRequest.Repository}/pulls/{pullRequest.Number}/reviews"),
            UriKind.Relative);

    [LoggerMessage(
        Level = LogLevel.Information,
        Message = "Published the review of run {RunId} on {Owner}/{Repository}#{Number}, comments: {CommentCount}.")]
    private static partial void LogPublished(
        ILogger logger,
        Guid runId,
        string owner,
        string repository,
        int number,
        int commentCount);

    [LoggerMessage(Level = LogLevel.Warning, Message = "GitHub answered {StatusCode}: {Answer}")]
    private static partial void LogErrorAnswer(ILogger logger, int statusCode, string answer);
}
