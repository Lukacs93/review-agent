using System.Globalization;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;
using ReviewAgent.Application.Reviews.Queries.GetReviewRun;
using ReviewAgent.Application.Reviews.Queries.GetRunLog;
using ReviewAgent.Domain.Reviews;
using ReviewAgent.Evaluation.Pulls;

namespace ReviewAgent.Evaluation.Services;

/// <summary>A finished review: what the API returns for it, and its run log.</summary>
internal sealed record CompletedReview(ReviewRunDetails Details, IReadOnlyList<RunLogEntryDetails> Log);

/// <summary>Starts a review through the API and waits until the agents finish, as the CI script does.</summary>
internal sealed class ReviewApiClient(HttpClient http)
{
    // The review's duration is measured by this waiting, so it is only as precise as the interval. A check is one
    // request to the API on this machine, not to the model. The agent's own time limit (Agent:Timeout) ends a review
    // that hangs, so the waiting needs no limit of its own.
    private static readonly TimeSpan _pollInterval = TimeSpan.FromSeconds(1);

    public static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web)
    {
        Converters = { new JsonStringEnumConverter() },
    };

    public async Task<CompletedReview> ReviewAsync(int pullRequestNumber, CancellationToken cancellationToken)
    {
        var request = new StartReviewRequest(
            PullRequestAssembler.Owner,
            PullRequestAssembler.RepositoryName,
            pullRequestNumber);
        using var response = await http.PostAsJsonAsync(
            new Uri("api/reviews", UriKind.Relative),
            request,
            JsonOptions,
            cancellationToken);
        response.EnsureSuccessStatusCode();
        var started = await response.Content.ReadFromJsonAsync<StartedReview>(JsonOptions, cancellationToken)
            ?? throw new InvalidOperationException(EvaluationErrors.EmptyApiResponse);

        var runAddress = new Uri(
            string.Create(CultureInfo.InvariantCulture, $"api/reviews/{started.Id}"),
            UriKind.Relative);
        var details = await GetAsync<ReviewRunDetails>(runAddress, cancellationToken);
        while (details.Status == ReviewStatus.Running)
        {
            await Task.Delay(_pollInterval, cancellationToken);
            details = await GetAsync<ReviewRunDetails>(runAddress, cancellationToken);
        }

        var log = await GetAsync<List<RunLogEntryDetails>>(
            new Uri(string.Create(CultureInfo.InvariantCulture, $"api/reviews/{started.Id}/log"), UriKind.Relative),
            cancellationToken);
        return new CompletedReview(details, log);
    }

    private async Task<T> GetAsync<T>(Uri address, CancellationToken cancellationToken) =>
        await http.GetFromJsonAsync<T>(address, JsonOptions, cancellationToken)
        ?? throw new InvalidOperationException(EvaluationErrors.EmptyApiResponse);

    private sealed record StartReviewRequest(string RepositoryOwner, string RepositoryName, int PullRequestNumber);

    private sealed record StartedReview(Guid Id);
}
