using ReviewAgent.Application.SourceControl;
using ReviewAgent.Domain.Rules;
using ReviewAgent.Evaluation.Dataset;

namespace ReviewAgent.Evaluation.Pulls;

/// <summary>A task's pull request, saved where the API reads it, with the team's rules of its base.</summary>
/// <param name="Number">The pull request's number under <see cref="PullRequestAssembler.Owner"/>.</param>
/// <param name="Directory">
/// The pull request's folder: <c>pull-request.json</c>, <c>repository/</c> and <c>base/</c>.
/// </param>
internal sealed record AssembledPullRequest(
    EvaluationTask Task,
    int Number,
    string Directory,
    IReadOnlyList<ChangedFile> Files,
    RuleSet Rules)
{
    /// <summary>The folder of the pull request's files, as they are after the change.</summary>
    public string RepositoryDirectory => Path.Combine(Directory, PullRequestAssembler.RepositoryDirectoryName);
}

/// <summary>The assembled pull requests, and what is wrong with the tasks that are not right.</summary>
/// <param name="RootDirectory">The folder the API reads the saved pull requests from.</param>
internal sealed record AssembledTasks(
    string RootDirectory,
    IReadOnlyList<AssembledPullRequest> PullRequests,
    IReadOnlyList<string> Problems);
