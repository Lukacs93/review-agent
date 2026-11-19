using System.Globalization;
using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using ReviewAgent.Application.SourceControl;
using ReviewAgent.Application.SourceControl.Errors;
using ReviewAgent.Domain.Reviews;
using ReviewAgent.Infrastructure.SourceControl.GitHub.Errors;

namespace ReviewAgent.Infrastructure.SourceControl.GitHub;

/// <summary>
/// Reads pull requests through the GitHub REST API, read-only. The <see cref="HttpClient"/> comes from the
/// HTTP client factory with the base address, the headers and a resilience handler (retries, timeouts) set up.
/// </summary>
internal sealed class GitHubPullRequestSource(HttpClient httpClient) : IPullRequestSource
{
    // Makes the contents endpoint return the file itself instead of a JSON description.
    private const string RawContentMediaType = "application/vnd.github.raw+json";

    private const string JsonMediaType = "application/vnd.github+json";
    private const string ApiVersionHeader = "X-GitHub-Api-Version";
    private const string ApiVersion = "2022-11-28";

    // GitHub rejects requests without a User-Agent.
    private const string UserAgentName = "review-agent";

    /// <summary>
    /// Sets up a client the HTTP client factory creates for GitHub: the address, the headers and the token. The
    /// source gets the read token, the publisher the write token.
    /// </summary>
    public static void ConfigureClient(HttpClient client, Uri apiUrl, string? token)
    {
        ArgumentNullException.ThrowIfNull(client);
        ArgumentNullException.ThrowIfNull(apiUrl);

        // Without the trailing slash a relative "repos/..." would replace the last part of the address.
        var address = apiUrl.ToString();
        client.BaseAddress = new Uri(address.EndsWith('/') ? address : address + "/");

        client.DefaultRequestHeaders.UserAgent.Add(new ProductInfoHeaderValue(UserAgentName, "1.0"));
        client.DefaultRequestHeaders.Accept.Add(new MediaTypeWithQualityHeaderValue(JsonMediaType));
        client.DefaultRequestHeaders.Add(ApiVersionHeader, ApiVersion);

        if (!string.IsNullOrWhiteSpace(token))
        {
            client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);
        }
    }

    public async Task<PullRequestSnapshot> GetPullRequestAsync(
        PullRequestRef pullRequest,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(pullRequest);

        var details = await GetJsonAsync<GitHubPullRequest>(PullRequestUri(pullRequest), cancellationToken)
            ?? throw new PullRequestNotFoundException(SourceControlErrors.PullRequestNotFound(pullRequest));

        var files = await GetChangedFilesAsync(pullRequest, cancellationToken);

        return new PullRequestSnapshot(
            details.Title,
            details.Body ?? string.Empty,
            details.Base.Ref,
            details.Head.Ref,
            files,
            details.Head.Sha);
    }

    public async Task<string?> GetFileAsync(
        PullRequestRef pullRequest,
        string path,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(pullRequest);
        ArgumentNullException.ThrowIfNull(path);

        using var request = new HttpRequestMessage(HttpMethod.Get, ContentUri(pullRequest, path));
        request.Headers.Accept.Add(new MediaTypeWithQualityHeaderValue(RawContentMediaType));

        using var response = await httpClient.SendAsync(request, cancellationToken);
        if (response.StatusCode == HttpStatusCode.NotFound)
        {
            return null;
        }

        response.EnsureSuccessStatusCode();
        return await response.Content.ReadAsStringAsync(cancellationToken);
    }

    private async Task<List<ChangedFile>> GetChangedFilesAsync(
        PullRequestRef pullRequest,
        CancellationToken cancellationToken)
    {
        var files = new List<ChangedFile>();
        Uri? pageUri = FilesUri(pullRequest);
        while (pageUri is not null)
        {
            using var response = await httpClient.GetAsync(pageUri, cancellationToken);
            if (response.StatusCode == HttpStatusCode.NotFound)
            {
                return files;
            }

            response.EnsureSuccessStatusCode();

            var page = await response.Content.ReadFromJsonAsync<List<GitHubChangedFile>>(cancellationToken) ?? [];
            files.AddRange(page.Select(ToChangedFile));

            pageUri = GitHubPagination.NextPageUri(response, httpClient.BaseAddress!);
        }

        return files;
    }

    /// <summary>Reads a JSON answer; <see langword="null"/> when GitHub answers 404 Not Found.</summary>
    private async Task<T?> GetJsonAsync<T>(Uri uri, CancellationToken cancellationToken)
    {
        using var response = await httpClient.GetAsync(uri, cancellationToken);
        if (response.StatusCode == HttpStatusCode.NotFound)
        {
            return default;
        }

        response.EnsureSuccessStatusCode();
        return await response.Content.ReadFromJsonAsync<T>(cancellationToken);
    }

    private static ChangedFile ToChangedFile(GitHubChangedFile file) =>
        new(file.FileName, ToStatus(file.Status), file.Patch ?? string.Empty);

    // GitHub also reports "copied", "changed" and "unchanged"; for a review they all mean the file was touched.
    private static FileChangeStatus ToStatus(string status) => status switch
    {
        "added" => FileChangeStatus.Added,
        "removed" => FileChangeStatus.Removed,
        "renamed" => FileChangeStatus.Renamed,
        _ => FileChangeStatus.Modified,
    };

    private static Uri PullRequestUri(PullRequestRef pullRequest) =>
        Relative(string.Create(
            CultureInfo.InvariantCulture,
            $"{RepositoryPath(pullRequest)}/pulls/{pullRequest.Number}"));

    private static Uri FilesUri(PullRequestRef pullRequest) =>
        Relative(string.Create(
            CultureInfo.InvariantCulture,
            $"{RepositoryPath(pullRequest)}/pulls/{pullRequest.Number}/files"));

    // "refs/pull/{number}/head" is the pull request's latest commit, also when it comes from a fork.
    private static Uri ContentUri(PullRequestRef pullRequest, string path) =>
        Relative(string.Create(
            CultureInfo.InvariantCulture,
            $"{RepositoryPath(pullRequest)}/contents/{EscapePath(path)}?ref=refs/pull/{pullRequest.Number}/head"));

    // Owner and repository are safe in a URL as they are: PullRequestRef only accepts GitHub-style names.
    private static string RepositoryPath(PullRequestRef pullRequest) =>
        $"repos/{pullRequest.Owner}/{pullRequest.Repository}";

    /// <summary>
    /// The path comes from the agent and goes into the URL, so it is checked and escaped part by part:
    /// a "." or ".." part could otherwise point the request at another API endpoint.
    /// </summary>
    private static string EscapePath(string path)
    {
        var parts = path.Split('/');
        if (path.Length == 0 || path.Contains('\\', StringComparison.Ordinal)
            || parts.Any(part => part is "" or "." or ".."))
        {
            throw new ArgumentException(GitHubErrors.InvalidPath(path), nameof(path));
        }

        return string.Join('/', parts.Select(Uri.EscapeDataString));
    }

    private static Uri Relative(string relativeUri) => new(relativeUri, UriKind.Relative);
}
