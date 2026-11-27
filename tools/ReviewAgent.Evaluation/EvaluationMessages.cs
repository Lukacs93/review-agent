using System.Globalization;
using ReviewAgent.Evaluation.Measuring;

namespace ReviewAgent.Evaluation;

/// <summary>What the tool tells the person running it while it works.</summary>
internal static class EvaluationMessages
{
    public const string TasksNotRight =
        "Some tasks are not right, so the measurement did not start. Correct them in the dataset:";

    public static string Started(ExecutionRecord execution)
    {
        ArgumentNullException.ThrowIfNull(execution);
        return string.Create(
            CultureInfo.InvariantCulture,
            $"Measurement \"{execution.Name}\" started: review-agent {execution.Commit}, "
            + $"{execution.Settings.Model}, {execution.Runs} runs per task.");
    }

    public static string Continues(ExecutionRecord execution)
    {
        ArgumentNullException.ThrowIfNull(execution);
        return string.Create(
            CultureInfo.InvariantCulture,
            $"Measurement \"{execution.Name}\" continues with the settings it started with: "
            + $"{execution.Settings.Model}, {execution.Runs} runs per task.");
    }

    public static string Reviewing(string taskId, int run, int number, int total) =>
        string.Create(CultureInfo.InvariantCulture, $"[{number}/{total}] {taskId}, run {run}");

    public static string StoppedSummary(string stopMessage) =>
        $"## The measurement stopped\n\n{stopMessage.Replace("\n", "\n\n", StringComparison.Ordinal)}";

    public static string SummaryWritten(string summaryFile) => $"Summary written to {summaryFile}.";

    public static string ReportWritten(string reportFile) => $"Report of every run, to open in a browser: {reportFile}";
}
