using System.Globalization;
using System.Text.Json;
using System.Text.Json.Serialization;
using ReviewAgent.Application.SourceControl;
using ReviewAgent.Domain;
using ReviewAgent.Evaluation.Dataset;
using ReviewAgent.Infrastructure.Rules;

namespace ReviewAgent.Evaluation.Pulls;

/// <summary>
/// Builds a saved pull request for each task: the task's repository at its base commit with the patch applied, in
/// the layout the API reads in saved mode (<c>{owner}/{repository}/{number}/</c> with <c>pull-request.json</c>,
/// <c>repository/</c> and <c>base/</c>). Only the pull request goes there: the expected findings stay in the
/// dataset, out of the agent's reach. Every task is then checked (see <see cref="TaskValidator"/>), before any
/// review starts.
/// </summary>
/// <param name="cloneSourceRoot">
/// Where the repositories are cloned from: <c>https://github.com/</c>, followed by the task's <c>owner/name</c>.
/// </param>
internal sealed class PullRequestAssembler(string cloneSourceRoot)
{
    /// <summary>The owner the assembled pull requests are saved under.</summary>
    public const string Owner = "evaluation";

    /// <summary>The repository the assembled pull requests are saved under; a task's number is its position.</summary>
    public const string RepositoryName = "tasks";

    public const string PullRequestFileName = "pull-request.json";
    public const string RepositoryDirectoryName = "repository";
    public const string BaseDirectoryName = "base";

    /// <summary>The API's default place of the team's rules file (<c>PullRequests:TeamRulesPath</c>).</summary>
    public const string RulesPath = ".review/rules.md";

    /// <summary>The demo repository's branch the tasks are made from.</summary>
    private const string BaseBranch = "main";

    private const string GitDirectoryName = ".git";
    private const string ClonesDirectoryName = "clones";

    // A file's diff starts with header lines (diff --git, index, ---, +++); its changes start at the first line
    // that is a block header. The saved format, like GitHub's, holds the changes only.
    private const string BlockHeaderLineStart = "\n@@";

    private static readonly JsonSerializerOptions _jsonOptions = new(JsonSerializerDefaults.Web)
    {
        Converters = { new JsonStringEnumConverter() },
        WriteIndented = true,
    };

    public async Task<AssembledTasks> AssembleAsync(
        IReadOnlyList<EvaluationTask> tasks,
        string workDirectory,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(tasks);

        var root = Path.Combine(workDirectory, "pull-requests");
        var clones = new Dictionary<string, string>(StringComparer.Ordinal);
        var pullRequests = new List<AssembledPullRequest>();
        var problems = DuplicateIds(tasks);

        for (var index = 0; index < tasks.Count; index++)
        {
            var task = tasks[index];
            try
            {
                var clone = await CloneAsync(task.Repository, workDirectory, clones, cancellationToken);
                var pullRequest = await AssembleAsync(task, number: index + 1, clone, root, cancellationToken);
                var taskProblems = TaskValidator.Validate(pullRequest);
                if (taskProblems.Count == 0)
                {
                    pullRequests.Add(pullRequest);
                }

                problems.AddRange(taskProblems);
            }
            catch (Exception exception) when (exception is InvalidOperationException or FormatException
                                                  or DomainException)
            {
                problems.Add(EvaluationErrors.TaskNotAssembled(task.Id, exception.Message));
            }
        }

        return new AssembledTasks(root, pullRequests, problems);
    }

    private static List<string> DuplicateIds(IReadOnlyList<EvaluationTask> tasks) =>
        tasks.GroupBy(task => task.Id, StringComparer.Ordinal)
            .Where(group => group.Count() > 1)
            .Select(group => EvaluationErrors.DuplicateTaskId(group.Key))
            .ToList();

    // One clone per repository; core.autocrlf=false keeps the files as they are in the repository on every system,
    // so the patches apply and the expected code matches on Windows too.
    private async Task<string> CloneAsync(
        string repository,
        string workDirectory,
        Dictionary<string, string> clones,
        CancellationToken cancellationToken)
    {
        if (clones.TryGetValue(repository, out var existing))
        {
            return existing;
        }

        var clone = Path.Combine(
            workDirectory,
            ClonesDirectoryName,
            clones.Count.ToString(CultureInfo.InvariantCulture));
        Directory.CreateDirectory(clone);
        await Git.RunAsync(
            clone,
            cancellationToken,
            "clone",
            "--quiet",
            "--no-checkout",
            "--config",
            "core.autocrlf=false",
            cloneSourceRoot + repository,
            ".");
        clones.Add(repository, clone);
        return clone;
    }

    private static async Task<AssembledPullRequest> AssembleAsync(
        EvaluationTask task,
        int number,
        string clone,
        string root,
        CancellationToken cancellationToken)
    {
        await Git.RunAsync(clone, cancellationToken, "checkout", "--quiet", "--force", task.BaseCommit);
        await Git.RunAsync(clone, cancellationToken, "clean", "--quiet", "--force", "-d", "-x");
        await Git.RunWithInputAsync(clone, task.Patch, cancellationToken, "apply", "--index", "-");

        var files = await ChangedFilesAsync(task, clone, cancellationToken);
        var directory = Path.Combine(root, Owner, RepositoryName, number.ToString(CultureInfo.InvariantCulture));
        CopyWorkingTree(clone, Path.Combine(directory, RepositoryDirectoryName));

        var rulesMarkdown = await Git.RunAsync(
            clone,
            cancellationToken,
            "show",
            $"{task.BaseCommit}:{RulesPath}");
        var rulesFile = Path.Combine(directory, BaseDirectoryName, RulesPath);
        Directory.CreateDirectory(Path.GetDirectoryName(rulesFile)!);
        await File.WriteAllTextAsync(rulesFile, rulesMarkdown, cancellationToken);

        var snapshot = new PullRequestSnapshot(task.Title, task.Description, BaseBranch, task.HeadBranch, files);
        await using (var stream = File.Create(Path.Combine(directory, PullRequestFileName)))
        {
            await JsonSerializer.SerializeAsync(stream, snapshot, _jsonOptions, cancellationToken);
        }

        return new AssembledPullRequest(task, number, directory, files, MarkdownRuleSetParser.Parse(rulesMarkdown));
    }

    private static async Task<List<ChangedFile>> ChangedFilesAsync(
        EvaluationTask task,
        string clone,
        CancellationToken cancellationToken)
    {
        // One line per file: a status letter, a tab, the path.
        var statusLines = await Git.RunAsync(
            clone,
            cancellationToken,
            "diff",
            "--cached",
            "--name-status",
            "--no-renames",
            task.BaseCommit);

        var files = new List<ChangedFile>();
        foreach (var statusLine in statusLines.Split('\n', StringSplitOptions.RemoveEmptyEntries))
        {
            var parts = statusLine.Split('\t');
            var path = parts[1];
            var diff = await Git.RunAsync(clone, cancellationToken, "diff", "--cached", task.BaseCommit, "--", path);
            files.Add(new ChangedFile(path, StatusOf(parts[0]), ChangesOf(diff)));
        }

        return files;
    }

    private static FileChangeStatus StatusOf(string letter) => letter switch
    {
        "A" => FileChangeStatus.Added,
        "D" => FileChangeStatus.Removed,
        _ => FileChangeStatus.Modified,
    };

    private static string ChangesOf(string diff)
    {
        var lineBreakBeforeFirstBlock = diff.IndexOf(BlockHeaderLineStart, StringComparison.Ordinal);
        return lineBreakBeforeFirstBlock < 0
            ? string.Empty
            : diff[(lineBreakBeforeFirstBlock + 1)..].TrimEnd('\n');
    }

    private static void CopyWorkingTree(string clone, string target)
    {
        foreach (var file in Directory.EnumerateFiles(clone, "*", SearchOption.AllDirectories))
        {
            var relativePath = Path.GetRelativePath(clone, file);
            if (relativePath.Split(Path.DirectorySeparatorChar)[0] == GitDirectoryName)
            {
                continue;
            }

            var targetFile = Path.Combine(target, relativePath);
            Directory.CreateDirectory(Path.GetDirectoryName(targetFile)!);
            File.Copy(file, targetFile);
        }
    }
}
