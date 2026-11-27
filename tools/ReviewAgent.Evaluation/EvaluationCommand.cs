using System.Diagnostics;
using System.Globalization;
using System.Text.Json;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.AI.Evaluation.Reporting;
using Microsoft.Extensions.AI.Evaluation.Reporting.Formats.Html;
using Microsoft.Extensions.AI.Evaluation.Reporting.Storage;
using ReviewAgent.Application.Reviews.Queries.GetReviewRun;
using ReviewAgent.Domain.Reviews;
using ReviewAgent.Evaluation.Dataset;
using ReviewAgent.Evaluation.Grading;
using ReviewAgent.Evaluation.Measuring;
using ReviewAgent.Evaluation.Pulls;
using ReviewAgent.Evaluation.Services;
using ReviewAgent.Evaluation.Summary;

namespace ReviewAgent.Evaluation;

/// <summary>What the command line asked for.</summary>
/// <param name="Execution">
/// The measurement's name; a new one starts a measurement, an existing one continues it.
/// </param>
/// <param name="Runs">How many times each task is reviewed, in a new measurement.</param>
internal sealed record EvaluationArguments(string? Execution, int Runs, bool ContinueDespiteChanges);

/// <summary>
/// Runs a measurement: assembles and checks every task, reviews each the given number of times through the API,
/// grades and stores every run as soon as it ends, and adds the results up. It stops at the first failure that is
/// not the agent's (the model's service, the infrastructure); started again with the same name, it continues
/// where it stopped.
/// </summary>
internal static class EvaluationCommand
{
    public const int Success = 0;
    public const int InvalidArguments = 1;
    public const int InvalidTasks = 2;
    public const int ChangedSinceStart = 3;
    public const int Stopped = 4;
    public const int AlreadyRunning = 5;

    /// <summary>A task names its repository as <c>owner/name</c> on GitHub.</summary>
    private const string GitHubRoot = "https://github.com/";

    private const string DatasetPath = "evaluation/dataset.jsonl";
    private const string ResultsPath = "evaluation/results";

    // The start time, sortable, as the Microsoft.Extensions.AI.Evaluation samples name their executions.
    private const string ExecutionNameFormat = "yyyyMMdd'T'HHmmss";

    private static readonly JsonSerializerOptions _jsonOptions = new(ReviewApiClient.JsonOptions)
    {
        WriteIndented = true,
    };

    public static async Task<int> RunAsync(
        EvaluationArguments arguments,
        TextWriter output,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(arguments);
        ArgumentNullException.ThrowIfNull(output);

        var name = arguments.Execution
            ?? DateTimeOffset.UtcNow.ToString(ExecutionNameFormat, CultureInfo.InvariantCulture);
        if (arguments.Runs < 1)
        {
            await output.WriteLineAsync(EvaluationErrors.RunsNotPositive);
            return InvalidArguments;
        }

        if (!name.All(character => char.IsAsciiLetterOrDigit(character) || character is '-' or '_' or '.'))
        {
            await output.WriteLineAsync(EvaluationErrors.InvalidExecutionName(name));
            return InvalidArguments;
        }

        var root = RepositoryState.FindRoot(Directory.GetCurrentDirectory());
        var resultsDirectory = Path.GetFullPath(ResultsPath, root);
        using var executionLock = ExecutionLock.TryAcquire(resultsDirectory, name);
        if (executionLock is null)
        {
            await output.WriteLineAsync(EvaluationErrors.AlreadyRunning(name));
            return AlreadyRunning;
        }

        var commit = await RepositoryState.CommitAsync(root, cancellationToken);

        var execution = await ExecutionRecord.ReadAsync(resultsDirectory, name, cancellationToken);
        var isNew = execution is null;
        execution ??= new ExecutionRecord(
            name,
            DateTimeOffset.UtcNow,
            commit,
            arguments.Runs,
            AgentSettings.Read(root));
        if (!isNew && execution.Commit != commit && !arguments.ContinueDespiteChanges)
        {
            await output.WriteLineAsync(EvaluationErrors.CommitChanged(execution, commit));
            return ChangedSinceStart;
        }

        var tasks = await EvaluationDataset.LoadAsync(Path.GetFullPath(DatasetPath, root), cancellationToken);
        var workDirectory = Path.Combine(
            Path.GetTempPath(),
            "review-agent-evaluation-" + Guid.NewGuid().ToString("N", CultureInfo.InvariantCulture));
        try
        {
            var assembled = await new PullRequestAssembler(GitHubRoot)
                .AssembleAsync(tasks, workDirectory, cancellationToken);
            if (assembled.Problems.Count > 0)
            {
                await output.WriteLineAsync(EvaluationMessages.TasksNotRight);
                foreach (var problem in assembled.Problems)
                {
                    await output.WriteLineAsync($"- {problem}");
                }

                return InvalidTasks;
            }

            // A new measurement is recorded once its tasks are right, so correcting them does not count as a change.
            if (isNew)
            {
                await execution.SaveAsync(resultsDirectory, cancellationToken);
            }

            await output.WriteLineAsync(
                isNew ? EvaluationMessages.Started(execution) : EvaluationMessages.Continues(execution));

            var store = new DiskBasedResultStore(resultsDirectory);
            var pending = await PendingRunsAsync(store, execution, assembled.PullRequests, cancellationToken);
            var measurement = new Measurement(
                root,
                workDirectory,
                resultsDirectory,
                execution,
                commit,
                TotalRuns: assembled.PullRequests.Count * execution.Runs);
            var finished = pending.Count == 0
                || await measurement.RunAsync(assembled, pending, output, cancellationToken);

            // Also when the measurement stopped: the report shows the runs done so far.
            await WriteReportAsync(store, execution.Name, resultsDirectory, output, cancellationToken);
            if (!finished)
            {
                return Stopped;
            }

            var summary = await SummarizeAsync(store, execution, tasks, cancellationToken);
            await WriteSummaryAsync(summary, resultsDirectory, output, cancellationToken);
            return Success;
        }
        finally
        {
            await DeleteWorkDirectoryAsync(workDirectory, output);
        }
    }

    // The runs not done yet, one run of every task before the next run of any, so a measurement that stops early
    // has results for as many tasks as it can. A run made for another version of its task is done again.
    private static async Task<List<(AssembledPullRequest PullRequest, int Run)>> PendingRunsAsync(
        DiskBasedResultStore store,
        ExecutionRecord execution,
        IReadOnlyList<AssembledPullRequest> pullRequests,
        CancellationToken cancellationToken)
    {
        var pending = new List<(AssembledPullRequest, int)>();
        for (var run = 1; run <= execution.Runs; run++)
        {
            foreach (var pullRequest in pullRequests)
            {
                var result = await ReadAsync(store, execution.Name, pullRequest.Task.Id, run, cancellationToken);
                if (result is not null && FingerprintOf(result) == pullRequest.Task.Fingerprint)
                {
                    continue;
                }

                if (result is not null)
                {
                    await store.DeleteResultsAsync(
                        execution.Name,
                        pullRequest.Task.Id,
                        IterationName(run),
                        cancellationToken);
                }

                pending.Add((pullRequest, run));
            }
        }

        return pending;
    }

    private static async Task<EvaluationSummary> SummarizeAsync(
        DiskBasedResultStore store,
        ExecutionRecord execution,
        IReadOnlyList<EvaluationTask> tasks,
        CancellationToken cancellationToken)
    {
        var runsByTask = new Dictionary<string, IReadOnlyList<RunResult>>(StringComparer.Ordinal);
        foreach (var task in tasks)
        {
            var runs = new List<RunResult>();
            for (var run = 1; run <= execution.Runs; run++)
            {
                if (await ReadAsync(store, execution.Name, task.Id, run, cancellationToken) is { } result)
                {
                    runs.Add(RunResult.From(result, task.Expected.Count));
                }
            }

            runsByTask[task.Id] = runs;
        }

        return EvaluationSummary.From(execution, tasks, runsByTask);
    }

    private static async Task WriteSummaryAsync(
        EvaluationSummary summary,
        string resultsDirectory,
        TextWriter output,
        CancellationToken cancellationToken)
    {
        var summaryFile = Path.Combine(resultsDirectory, "summaries", $"{summary.Execution}.json");
        Directory.CreateDirectory(Path.GetDirectoryName(summaryFile)!);
        await using (var stream = File.Create(summaryFile))
        {
            await JsonSerializer.SerializeAsync(stream, summary, _jsonOptions, cancellationToken);
        }

        var markdown = SummaryMarkdown.Write(summary);
        await output.WriteLineAsync(markdown);
        await output.WriteLineAsync(EvaluationMessages.SummaryWritten(summaryFile));

        await StepSummary.AppendAsync(markdown, cancellationToken);
    }

    // Every run of the measurement in one page to open in a browser: each run's measures, and its review, comments
    // and run log.
    private static async Task WriteReportAsync(
        DiskBasedResultStore store,
        string execution,
        string resultsDirectory,
        TextWriter output,
        CancellationToken cancellationToken)
    {
        var results = new List<ScenarioRunResult>();
        await foreach (var result in store.ReadResultsAsync(execution, cancellationToken: cancellationToken))
        {
            results.Add(result);
        }

        if (results.Count == 0)
        {
            return;
        }

        var reportFile = Path.Combine(resultsDirectory, "reports", $"{execution}.html");
        Directory.CreateDirectory(Path.GetDirectoryName(reportFile)!);
        await new HtmlReportWriter(reportFile).WriteReportAsync(results, cancellationToken);
        await output.WriteLineAsync(EvaluationMessages.ReportWritten(reportFile));
    }

    private static async Task<ScenarioRunResult?> ReadAsync(
        DiskBasedResultStore store,
        string execution,
        string taskId,
        int run,
        CancellationToken cancellationToken)
    {
        await foreach (var result in store.ReadResultsAsync(execution, taskId, IterationName(run), cancellationToken))
        {
            return result;
        }

        return null;
    }

    private static string? FingerprintOf(ScenarioRunResult result) =>
        result.EvaluationResult.Metrics.TryGetValue(ReviewMetrics.Run, out var run)
        && run.Metadata is not null
        && run.Metadata.TryGetValue(ReviewMetrics.TaskFingerprintKey, out var fingerprint)
            ? fingerprint
            : null;

    private static string IterationName(int run) => run.ToString(CultureInfo.InvariantCulture);

    // The clones, the assembled pull requests and the services' data. The services are stopped by now, so nothing
    // holds their files; should the deletion still fail, the person is told where the folder is.
    private static async Task DeleteWorkDirectoryAsync(string workDirectory, TextWriter output)
    {
        try
        {
            TemporaryFolder.Delete(workDirectory);
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
        {
            await output.WriteLineAsync(EvaluationErrors.WorkDirectoryNotDeleted(workDirectory, exception.Message));
        }
    }

    /// <summary>The reviews of one measurement, run through the services it starts.</summary>
    private sealed record Measurement(
        string Root,
        string WorkDirectory,
        string ResultsDirectory,
        ExecutionRecord Execution,
        string Commit,
        int TotalRuns)
    {
        /// <summary>Runs the pending reviews; false when a failure that is not the agent's stopped them.</summary>
        public async Task<bool> RunAsync(
            AssembledTasks assembled,
            List<(AssembledPullRequest PullRequest, int Run)> pending,
            TextWriter output,
            CancellationToken cancellationToken)
        {
            var reporting = DiskBasedReportingConfiguration.Create(
                storageRootPath: ResultsDirectory,
                evaluators: [new ReviewEvaluator()],
                enableResponseCaching: false,
                executionName: Execution.Name);

            await using var services = await StartServicesAsync(assembled, pending, output, cancellationToken);
            if (services is null)
            {
                return false;
            }

            using var http = new HttpClient { BaseAddress = services.ApiAddress };
            var api = new ReviewApiClient(http);

            var done = TotalRuns - pending.Count;
            foreach (var (pullRequest, run) in pending)
            {
                var task = pullRequest.Task;
                await output.WriteLineAsync(EvaluationMessages.Reviewing(task.Id, run, done + 1, TotalRuns));

                var stopwatch = Stopwatch.StartNew();
                CompletedReview review;
                try
                {
                    review = await api.ReviewAsync(pullRequest.Number, cancellationToken);
                }
                catch (Exception exception) when (exception is HttpRequestException
                                                      || (exception is TaskCanceledException
                                                          && !cancellationToken.IsCancellationRequested))
                {
                    // The API did not answer, or not within the HTTP client's time limit.
                    var kind = nameof(ReviewFailureKind.Infrastructure);
                    await ReportStopAsync(output, task, run, kind, exception.Message, done, cancellationToken);
                    return false;
                }

                if (FailureNotCausedByTheAgent(review.Details) is { } failure)
                {
                    var kind = failure.Kind.ToString();
                    await ReportStopAsync(output, task, run, kind, failure.Reason, done, cancellationToken);
                    return false;
                }

                var context = new ReviewRunContext(task, review, pullRequest.Rules, Commit, stopwatch.Elapsed);
                await using (var scenarioRun = await reporting.CreateScenarioRunAsync(
                                 task.Id,
                                 IterationName(run),
                                 cancellationToken: cancellationToken))
                {
                    await scenarioRun.EvaluateAsync(
                        [new ChatMessage(ChatRole.User, $"{task.Title}\n\n{task.Description}")],
                        Transcript(review),
                        [context],
                        cancellationToken);
                }

                done++;
            }

            return true;
        }

        // The services failing to start is the infrastructure's failure, so the measurement stops before its first run.
        private async Task<ReviewServices?> StartServicesAsync(
            AssembledTasks assembled,
            List<(AssembledPullRequest PullRequest, int Run)> pending,
            TextWriter output,
            CancellationToken cancellationToken)
        {
            try
            {
                return await ReviewServices.StartAsync(
                    Root,
                    assembled.RootDirectory,
                    Path.Combine(WorkDirectory, "data"),
                    Path.Combine(ResultsDirectory, "logs", Execution.Name),
                    Execution.Settings,
                    cancellationToken);
            }
            catch (InvalidOperationException exception)
            {
                var (pullRequest, run) = pending[0];
                var kind = nameof(ReviewFailureKind.Infrastructure);
                var done = TotalRuns - pending.Count;
                await ReportStopAsync(output, pullRequest.Task, run, kind, exception.Message, done, cancellationToken);
                return null;
            }
        }

        // On the console, and on GitHub on the run's page too, where the log is otherwise the only place to see it.
        private async Task ReportStopAsync(
            TextWriter output,
            EvaluationTask task,
            int run,
            string kind,
            string? reason,
            int done,
            CancellationToken cancellationToken)
        {
            var message = EvaluationErrors.Stopped(
                new StoppedRun(Execution.Name, task.Id, run, kind, reason, done, TotalRuns));
            await output.WriteLineAsync(message);
            await StepSummary.AppendAsync(EvaluationMessages.StoppedSummary(message), cancellationToken);
        }

        // The review and its run log, stored with the run, so every run can be read afterwards.
        private static ChatResponse Transcript(CompletedReview review) =>
            new(
            [
                new ChatMessage(ChatRole.Assistant, JsonSerializer.Serialize(review.Details, _jsonOptions)),
                new ChatMessage(ChatRole.Assistant, JsonSerializer.Serialize(review.Log, _jsonOptions)),
            ]);

        // A failure of the model's service or of the infrastructure, in the review or in its verification: it says
        // nothing about the agent, so the measurement stops instead of counting it.
        private static (ReviewFailureKind Kind, string? Reason)? FailureNotCausedByTheAgent(ReviewRunDetails details)
        {
            if (details.FailureKind is { } kind && kind != ReviewFailureKind.Agent)
            {
                return (kind, details.FailureReason);
            }

            if (details.VerificationFailureKind is { } verificationKind && verificationKind != ReviewFailureKind.Agent)
            {
                return (verificationKind, details.VerificationFailureReason);
            }

            return null;
        }
    }
}
