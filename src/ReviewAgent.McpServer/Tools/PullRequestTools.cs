using System.ComponentModel;
using System.Text;
using Microsoft.Extensions.Options;
using ModelContextProtocol;
using ModelContextProtocol.Server;
using ReviewAgent.Application.SourceControl;
using ReviewAgent.McpServer.Errors;

namespace ReviewAgent.McpServer.Tools;

/// <summary>
/// The tools the review agent uses to read the pull request under review. All of them only read. The model decides
/// when to call which one from the names and descriptions below, so those are written for the model.
/// </summary>
[McpServerToolType]
internal sealed class PullRequestTools(
    IPullRequestSource source,
    ReviewedPullRequest reviewedPullRequest,
    IOptions<TeamRulesOptions> teamRules)
{
    [McpServerTool(Name = "get_pull_request", ReadOnly = true)]
    [Description(
        "Returns the pull request under review: its title, description, branches and the list of changed files "
        + "with their change status. Call this first.")]
    public async Task<string> GetPullRequestAsync(CancellationToken cancellationToken)
    {
        var snapshot = await GetSnapshotAsync(cancellationToken);

        var text = new StringBuilder()
            .Append("Title: ").AppendLine(snapshot.Title)
            .Append("Merges ").Append(snapshot.HeadBranch).Append(" into ").AppendLine(snapshot.BaseBranch)
            .AppendLine()
            .AppendLine("Description:")
            .AppendLine(string.IsNullOrWhiteSpace(snapshot.Description) ? "(none)" : snapshot.Description)
            .AppendLine()
            .AppendLine("Changed files:");

        foreach (var file in snapshot.Files)
        {
            text.Append("- ").Append(file.Path).Append(" (").Append(file.Status).AppendLine(")");
        }

        return text.ToString();
    }

    [McpServerTool(Name = "get_diff", ReadOnly = true)]
    [Description(
        "Returns the changes of one changed file in unified diff format, with line numbers: each added (+) or "
        + "unchanged line starts with its line number in the new file, before the '|'. Removed lines (-) and block "
        + "headers (@@) have none. Use that number as the line of a review comment.")]
    public async Task<string> GetDiffAsync(
        [Description("Path of a changed file, as listed by get_pull_request.")] string path,
        CancellationToken cancellationToken)
    {
        var snapshot = await GetSnapshotAsync(cancellationToken);

        var file = snapshot.Files.FirstOrDefault(changed => string.Equals(changed.Path, path, StringComparison.Ordinal))
            ?? throw new McpException(ToolErrors.FileNotChanged(path));

        return file.Patch.Length > 0 ? NumberedText.Diff(file.Patch) : throw new McpException(ToolErrors.NoDiff(path));
    }

    [McpServerTool(Name = "get_file", ReadOnly = true)]
    [Description(
        "Returns the full content of any file in the repository as it is in the pull request, changed or not, with "
        + "its line number before the '|' on each line. Use it for context: the surrounding code, existing tests, "
        + "or how something is used elsewhere.")]
    public async Task<string> GetFileAsync(
        [Description("Path relative to the repository root, for example src/Orders.Api/Domain/Order.cs.")] string path,
        CancellationToken cancellationToken)
    {
        var content = await ReadFileAsync(RepositoryVersion.PullRequest, path, cancellationToken)
            ?? throw new McpException(ToolErrors.FileNotFound(path));
        return NumberedText.File(content);
    }

    [McpServerTool(Name = "get_team_rules", ReadOnly = true)]
    [Description(
        "Returns the team's review rules, as they are on the branch the pull request merges into. Every review "
        + "comment must reference one of these rules by its ID, for example ARCH-01.")]
    public async Task<string> GetTeamRulesAsync(CancellationToken cancellationToken)
    {
        var rulesPath = teamRules.Value.TeamRulesPath;
        return await ReadFileAsync(RepositoryVersion.Base, rulesPath, cancellationToken)
            ?? throw new McpException(ToolErrors.TeamRulesNotFound(rulesPath));
    }

    private async Task<PullRequestSnapshot> GetSnapshotAsync(CancellationToken cancellationToken)
    {
        try
        {
            return await source.GetPullRequestAsync(reviewedPullRequest.Get(), cancellationToken);
        }
        catch (PullRequestNotFoundException exception)
        {
            throw new McpException(exception.Message, exception);
        }
    }

    private async Task<string?> ReadFileAsync(
        RepositoryVersion version,
        string path,
        CancellationToken cancellationToken)
    {
        try
        {
            return await source.GetFileAsync(reviewedPullRequest.Get(), version, path, cancellationToken);
        }
        catch (ArgumentException exception)
        {
            // An invalid path from the model: tell it why, so it can correct the path.
            throw new McpException(exception.Message, exception);
        }
    }
}
