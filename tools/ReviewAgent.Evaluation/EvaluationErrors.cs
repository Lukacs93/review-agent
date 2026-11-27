using System.Globalization;
using ReviewAgent.Evaluation.Measuring;

namespace ReviewAgent.Evaluation;

/// <summary>Why a task or a measurement cannot go on. Shown to the person running it, so they say what to do.</summary>
internal static class EvaluationErrors
{
    public const string RunsNotPositive = "--runs must be at least 1.";

    public const string GitNotStarted = "git could not be started. Install git and put it on the PATH.";

    public const string EmptyApiResponse = "The API answered without a body.";

    public static string InvalidExecutionName(string name) =>
        $"The measurement name '{name}' may only contain letters, digits, '-', '_' and '.', as it names a folder.";

    public static string AlreadyRunning(string name) =>
        $"The measurement \"{name}\" is already running in another process.";

    public static string EmptyTaskRecord(string path) => $"The dataset '{path}' has an empty task record.";

    public static string GitFailed(string arguments, string error) => $"git {arguments} failed: {error}";

    public static string TaskNotAssembled(string taskId, string reason) =>
        $"{taskId}: the pull request could not be assembled: {reason}";

    public static string DuplicateTaskId(string taskId) => $"{taskId}: more than one task has this ID.";

    public static string PositiveTaskNeedsOneFinding(string taskId, int count) =>
        string.Create(
            CultureInfo.InvariantCulture,
            $"{taskId}: a positive task breaks exactly one rule, but it has {count} required findings.");

    public static string NegativeTaskHasFindings(string taskId) =>
        $"{taskId}: a negative task needs no comment, but it has expected findings.";

    public static string UnknownRule(string taskId, string rule) =>
        $"{taskId}: the rule {rule} is not in the team's rules of the task's base commit.";

    public static string FindingWithoutLines(string taskId, string rule, string file) =>
        $"{taskId}: the expected {rule} finding in {file} has no lines.";

    public static string FileNotChanged(string taskId, string file) =>
        $"{taskId}: {file} is not changed by the pull request, so no comment can be on it.";

    public static string LineNotInDiff(string taskId, string file, int line) =>
        string.Create(
            CultureInfo.InvariantCulture,
            $"{taskId}: {file}:{line} is not in the diff, so the agent cannot comment on it.");

    public static string CodeDiffers(string taskId, string file, int line, string expected, string actual) =>
        string.Create(
            CultureInfo.InvariantCulture,
            $"{taskId}: {file}:{line} is '{actual}', but the task expects '{expected}'.");

    public static string SettingMissing(string key) =>
        $"{key} is not set in src/ReviewAgent.Api/appsettings.json or in the environment.";

    public static string RepositoryRootNotFound(string directory) =>
        $"No review-agent repository (ReviewAgent.slnx) in '{directory}' or above it. Run the tool inside the "
        + "repository.";

    public static string BuildFailed(string project, string logFile) =>
        $"Building {project} failed; see {logFile}.";

    public static string ProcessNotStarted(string fileName) => $"'{fileName}' could not be started.";

    public static string ServiceNotHealthy(Uri healthAddress, string logFile) =>
        $"The service at {healthAddress} did not become healthy; see {logFile}.";

    public static string WorkDirectoryNotDeleted(string workDirectory, string reason) =>
        $"The temporary folder {workDirectory} could not be deleted ({reason}); delete it by hand.";

    public static string ContextMissing(string contextName) =>
        $"The evaluator needs the '{contextName}' context to grade a run.";

    public static string CommitChanged(ExecutionRecord execution, string currentCommit)
    {
        ArgumentNullException.ThrowIfNull(execution);
        return $"The measurement \"{execution.Name}\" was started with review-agent {execution.Commit} and "
            + $"{execution.Settings.Model}.\nIt now runs {currentCommit}: the commit changed.\n"
            + "Start a new measurement, or continue with --continue-despite-changes if the change does not affect "
            + "the results.";
    }

    public static string Stopped(StoppedRun stopped)
    {
        ArgumentNullException.ThrowIfNull(stopped);
        return string.Create(
            CultureInfo.InvariantCulture,
            $"The review of {stopped.TaskId} (run {stopped.Run}) failed, and not because of the agent "
            + $"({stopped.Kind}): {stopped.Reason}\n{stopped.Done} of {stopped.Total} runs are done. Continue later "
            + $"with --execution {stopped.Execution}; the runs already done are kept.");
    }
}

/// <summary>Where a measurement stopped on a failure of the model's service or of the infrastructure.</summary>
internal sealed record StoppedRun(
    string Execution,
    string TaskId,
    int Run,
    string Kind,
    string? Reason,
    int Done,
    int Total);
