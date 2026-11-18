using System.Globalization;
using System.Text.Json;
using ReviewAgent.Application.Reviews.Publishing;
using ReviewAgent.Infrastructure.SourceControl.GitHub;

namespace ReviewAgent.Infrastructure.SourceControl.Saved;

/// <summary>
/// Publishes reviews of saved pull requests to disk, as the request that would create the review on GitHub, so
/// the whole workflow can be run without GitHub. Layout under the root directory:
/// <c>{owner}/{repository}/{number}/published-reviews/{run ID}.json</c>.
/// </summary>
internal sealed class SavedReviewPublisher(string rootDirectory) : IReviewPublisher
{
    private const string PublishedReviewsDirectoryName = "published-reviews";

    private static readonly JsonSerializerOptions _jsonOptions = new(GitHubReviewRequest.JsonOptions)
    {
        WriteIndented = true,
    };

    private readonly string _rootDirectory = Path.GetFullPath(rootDirectory);

    public Task<bool> IsPublishedAsync(ReviewPublication publication, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(publication);

        return Task.FromResult(File.Exists(FileOf(publication)));
    }

    public async Task PublishAsync(ReviewPublication publication, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(publication);

        var file = FileOf(publication);
        Directory.CreateDirectory(Path.GetDirectoryName(file)!);

        // Written to a temporary file first and then renamed, so a write that stops halfway never looks like a
        // published review.
        var temporaryFile = file + ".tmp";
        await using (var stream = File.Create(temporaryFile))
        {
            await JsonSerializer.SerializeAsync(
                stream,
                GitHubReviewRequest.From(publication),
                _jsonOptions,
                cancellationToken);
        }

        File.Move(temporaryFile, file);
    }

    // PullRequestRef only accepts GitHub-style names, so the owner and repository cannot lead out of the root.
    private string FileOf(ReviewPublication publication)
    {
        var pullRequest = publication.PullRequest;
        return Path.Combine(
            _rootDirectory,
            pullRequest.Owner,
            pullRequest.Repository,
            pullRequest.Number.ToString(CultureInfo.InvariantCulture),
            PublishedReviewsDirectoryName,
            publication.RunId.ToString("D", CultureInfo.InvariantCulture) + ".json");
    }
}
