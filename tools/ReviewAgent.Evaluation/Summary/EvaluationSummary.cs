using ReviewAgent.Evaluation.Dataset;
using ReviewAgent.Evaluation.Grading;
using ReviewAgent.Evaluation.Measuring;

namespace ReviewAgent.Evaluation.Summary;

/// <summary>
/// The measurement's results added up: the numbers the README and the job summary show. Coverage, precision and the
/// averages come from the completed runs; a failed run counts apart, and as a miss in the consistency, since the
/// developer gets nothing from it.
/// </summary>
/// <param name="Coverage">The required findings the kept comments found, out of all of them.</param>
/// <param name="Precision">The kept comments that found a finding, out of all kept comments.</param>
/// <param name="Consistency">The required findings found in every run of their task (pass^k).</param>
/// <param name="FalseCommentsPerNegativeRun">The average number of comments on a pull request that needs none.</param>
/// <param name="RuleTextOverlap">How much the comments repeat their rule's text, from 0 to 1 (F1), on average.</param>
/// <param name="Commits">review-agent's commits the runs were made with, and how many runs each.</param>
internal sealed record EvaluationSummary(
    string Execution,
    string Model,
    int RunsPerTask,
    IReadOnlyDictionary<string, int> Commits,
    int CompletedRuns,
    IReadOnlyList<FailedRun> FailedRuns,
    double? Coverage,
    double? Precision,
    double? Consistency,
    double? FalseCommentsPerNegativeRun,
    int WrongPlace,
    int WrongRule,
    int Duplicates,
    int RejectedCorrect,
    int RejectedIncorrect,
    double? RuleTextOverlap,
    double? AverageDurationSeconds,
    double? AverageModelCalls,
    double? AverageInputTokens,
    double? AverageOutputTokens,
    IReadOnlyList<RuleResult> Rules,
    IReadOnlyList<TaskResult> Tasks)
{
    public static EvaluationSummary From(
        ExecutionRecord execution,
        IReadOnlyList<EvaluationTask> tasks,
        IReadOnlyDictionary<string, IReadOnlyList<RunResult>> runsByTask)
    {
        ArgumentNullException.ThrowIfNull(execution);
        ArgumentNullException.ThrowIfNull(tasks);
        ArgumentNullException.ThrowIfNull(runsByTask);

        var allRuns = tasks.SelectMany(task => runsByTask.GetValueOrDefault(task.Id) ?? []).ToList();
        var completed = allRuns.Where(run => run.Completed).ToList();
        var negativeRuns = tasks.Where(task => task.Group == TaskGroup.Negative)
            .SelectMany(task => runsByTask.GetValueOrDefault(task.Id) ?? [])
            .Where(run => run.Completed)
            .ToList();
        var taskResults = tasks.Select(task => TaskResult.From(task, runsByTask.GetValueOrDefault(task.Id) ?? []))
            .ToList();

        return new EvaluationSummary(
            execution.Name,
            execution.Settings.Model,
            execution.Runs,
            allRuns.GroupBy(run => run.Commit ?? string.Empty, StringComparer.Ordinal)
                .ToDictionary(group => group.Key, group => group.Count(), StringComparer.Ordinal),
            completed.Count,
            allRuns.Where(run => !run.Completed)
                .Select(run => new FailedRun(run.TaskId, run.FailureKind, run.FailureReason))
                .ToList(),
            Ratio(Sum(completed, ReviewMetrics.Found), Sum(completed, ReviewMetrics.Required)),
            Ratio(Sum(completed, ReviewMetrics.CorrectComments), Sum(completed, ReviewMetrics.KeptComments)),
            ShareFoundInEveryRun(taskResults, execution.Runs),
            Average(negativeRuns, ReviewMetrics.FalseComments),
            (int)Sum(completed, ReviewMetrics.WrongPlace),
            (int)Sum(completed, ReviewMetrics.WrongRule),
            (int)Sum(completed, ReviewMetrics.Duplicates),
            (int)Sum(completed, ReviewMetrics.RejectedCorrect),
            (int)Sum(completed, ReviewMetrics.RejectedIncorrect),
            AverageOfReported(completed, ReviewMetrics.RuleTextOverlap),
            Average(completed, ReviewMetrics.DurationSeconds),
            Average(completed, ReviewMetrics.ModelCalls),
            AverageOfReported(completed, ReviewMetrics.InputTokens),
            AverageOfReported(completed, ReviewMetrics.OutputTokens),
            RuleResult.From(tasks, runsByTask),
            taskResults);
    }

    // Only tasks with all their runs done count; a failed run counts as not finding anything.
    private static double? ShareFoundInEveryRun(List<TaskResult> tasks, int runsPerTask)
    {
        var findings = tasks.Where(task => task.Runs == runsPerTask)
            .SelectMany(task => task.Findings)
            .Where(finding => !finding.Optional)
            .ToList();
        return Ratio(findings.Count(finding => finding.FoundIn == runsPerTask), findings.Count);
    }

    private static double Sum(List<RunResult> runs, string measure) => runs.Sum(run => run.Measure(measure));

    private static double? Average(List<RunResult> runs, string measure) =>
        runs.Count == 0 ? null : runs.Average(run => run.Measure(measure));

    // Leaves out the runs that did not report the measure (no comments to score, no token counts from the API).
    private static double? AverageOfReported(List<RunResult> runs, string measure)
    {
        var reported = runs.Select(run => run.Measures.GetValueOrDefault(measure))
            .OfType<double>()
            .ToList();
        return reported.Count == 0 ? null : reported.Average();
    }

    private static double? Ratio(double part, double whole) => whole == 0 ? null : part / whole;
}

/// <summary>A run that failed, and why.</summary>
internal sealed record FailedRun(string TaskId, string? Kind, string? Reason);

/// <summary>How often the required findings of one rule were found, and the false comments citing it.</summary>
internal sealed record RuleResult(string Rule, int Expected, int Found, int FalseComments)
{
    public static IReadOnlyList<RuleResult> From(
        IReadOnlyList<EvaluationTask> tasks,
        IReadOnlyDictionary<string, IReadOnlyList<RunResult>> runsByTask)
    {
        var counts = new SortedDictionary<string, (int Expected, int Found, int FalseComments)>(
            StringComparer.OrdinalIgnoreCase);
        foreach (var task in tasks)
        {
            var completed = (runsByTask.GetValueOrDefault(task.Id) ?? []).Where(run => run.Completed).ToList();
            for (var index = 0; index < task.Expected.Count; index++)
            {
                var finding = task.Expected[index];
                if (finding.Optional)
                {
                    continue;
                }

                var current = counts.GetValueOrDefault(finding.Rule);
                var findingIndex = index;
                counts[finding.Rule] = (
                    current.Expected + completed.Count,
                    current.Found + completed.Count(run => run.FoundFindings[findingIndex]),
                    current.FalseComments);
            }

            foreach (var (rule, count) in completed.SelectMany(run => run.FalseCommentsByRule))
            {
                var current = counts.GetValueOrDefault(rule);
                counts[rule] = (current.Expected, current.Found, current.FalseComments + count);
            }
        }

        return counts.Select(entry =>
                new RuleResult(entry.Key, entry.Value.Expected, entry.Value.Found, entry.Value.FalseComments))
            .ToList();
    }
}

/// <summary>One task's results: how many of its runs found each expected finding.</summary>
internal sealed record TaskResult(
    string Id,
    TaskGroup Group,
    int Runs,
    int FailedRuns,
    IReadOnlyList<FindingResult> Findings,
    double? FalseCommentsPerRun)
{
    public static TaskResult From(EvaluationTask task, IReadOnlyList<RunResult> runs)
    {
        ArgumentNullException.ThrowIfNull(task);
        ArgumentNullException.ThrowIfNull(runs);

        var completed = runs.Where(run => run.Completed).ToList();
        var findings = task.Expected
            .Select((finding, index) => new FindingResult(
                finding.Rule,
                finding.File,
                finding.Lines[0].Number,
                finding.Optional,
                completed.Count(run => run.FoundFindings[index])))
            .ToList();
        return new TaskResult(
            task.Id,
            task.Group,
            runs.Count,
            runs.Count - completed.Count,
            findings,
            completed.Count == 0 ? null : completed.Average(run => run.Measure(ReviewMetrics.FalseComments)));
    }
}

/// <summary>An expected finding, and in how many of its task's runs it was found.</summary>
/// <param name="Line">The first of the lines a comment may point it out on.</param>
internal sealed record FindingResult(string Rule, string File, int Line, bool Optional, int FoundIn);
