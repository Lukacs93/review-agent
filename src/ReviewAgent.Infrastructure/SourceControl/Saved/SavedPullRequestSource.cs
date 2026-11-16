using System.Globalization;
using System.Text.Json;
using System.Text.Json.Serialization;
using ReviewAgent.Application.SourceControl;
using ReviewAgent.Application.SourceControl.Errors;
using ReviewAgent.Domain.Reviews;
using ReviewAgent.Infrastructure.SourceControl.Saved.Errors;

namespace ReviewAgent.Infrastructure.SourceControl.Saved;

/// <summary>
/// Reads pull requests saved to disk, so the agent can run offline and every run sees the same input.
/// Layout under the root directory: <c>{owner}/{repository}/{number}/</c> holds <c>pull-request.json</c>
/// (title, description, changed files with their diffs) and <c>repository/</c>, the files as they are in the
/// pull request.
/// </summary>
internal sealed class SavedPullRequestSource(string rootDirectory) : IPullRequestSource
{
    private const string PullRequestFileName = "pull-request.json";
    private const string RepositoryDirectoryName = "repository";

    private static readonly JsonSerializerOptions _jsonOptions = new(JsonSerializerDefaults.Web)
    {
        Converters = { new JsonStringEnumConverter() },
    };

    private readonly string _rootDirectory = Path.GetFullPath(rootDirectory);

    public async Task<PullRequestSnapshot> GetPullRequestAsync(
        PullRequestRef pullRequest,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(pullRequest);

        var file = Path.Combine(DirectoryOf(pullRequest), PullRequestFileName);
        if (!File.Exists(file))
        {
            throw new PullRequestNotFoundException(SourceControlErrors.PullRequestNotFound(pullRequest));
        }

        await using var stream = File.OpenRead(file);
        return await JsonSerializer.DeserializeAsync<PullRequestSnapshot>(stream, _jsonOptions, cancellationToken)
            ?? throw new InvalidOperationException(SavedPullRequestErrors.EmptyFile(file));
    }

    public async Task<string?> GetFileAsync(
        PullRequestRef pullRequest,
        string path,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(pullRequest);
        ArgumentNullException.ThrowIfNull(path);

        // The path comes from the agent, so it must not lead out of the saved repository ("../", absolute paths).
        var repositoryDirectory = Path.Combine(DirectoryOf(pullRequest), RepositoryDirectoryName);
        var file = Path.GetFullPath(Path.Combine(repositoryDirectory, path));
        if (!IsInside(file, repositoryDirectory))
        {
            throw new ArgumentException(SavedPullRequestErrors.PathOutsideRepository(path), nameof(path));
        }

        return File.Exists(file) ? await File.ReadAllTextAsync(file, cancellationToken) : null;
    }

    private string DirectoryOf(PullRequestRef pullRequest)
    {
        var directory = Path.GetFullPath(Path.Combine(
            _rootDirectory,
            pullRequest.Owner,
            pullRequest.Repository,
            pullRequest.Number.ToString(CultureInfo.InvariantCulture)));

        // The owner and repository names come from the request, so they must not lead out of the root either.
        if (!IsInside(directory, _rootDirectory))
        {
            throw new PullRequestNotFoundException(SourceControlErrors.PullRequestNotFound(pullRequest));
        }

        return directory;
    }

    private static bool IsInside(string path, string directory) =>
        path.StartsWith(
            Path.TrimEndingDirectorySeparator(directory) + Path.DirectorySeparatorChar,
            StringComparison.Ordinal);
}
