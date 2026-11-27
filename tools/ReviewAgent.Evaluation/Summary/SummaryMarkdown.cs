using System.Globalization;
using System.Text;

namespace ReviewAgent.Evaluation.Summary;

/// <summary>The summary as Markdown, for the console and the GitHub job summary.</summary>
internal static class SummaryMarkdown
{
    private const string NotMeasured = "-";

    public static string Write(EvaluationSummary summary)
    {
        ArgumentNullException.ThrowIfNull(summary);

        var text = new StringBuilder();
        var culture = CultureInfo.InvariantCulture;
        text.AppendLine(culture, $"## Evaluation \"{summary.Execution}\"");
        text.AppendLine();
        text.AppendLine(culture, $"Model: {summary.Model}, {summary.RunsPerTask} runs per task.");
        if (summary.Commits.Count > 1)
        {
            var commits = string.Join(
                ", ",
                summary.Commits.Select(commit => string.Create(culture, $"{commit.Key} ({commit.Value} runs)")));
            text.AppendLine();
            text.AppendLine(culture, $"**The runs were made with more than one commit:** {commits}.");
        }

        text.AppendLine();
        text.AppendLine("| Measure | Value |");
        text.AppendLine("| --- | --- |");
        text.AppendLine(culture, $"| Coverage | {Percent(summary.Coverage)} |");
        text.AppendLine(culture, $"| Precision | {Percent(summary.Precision)} |");
        text.AppendLine(culture, $"| Consistency (pass^{summary.RunsPerTask}) | {Percent(summary.Consistency)} |");
        var falseComments = Number(summary.FalseCommentsPerNegativeRun);
        var mistakes = string.Create(culture, $"{summary.WrongPlace} / {summary.WrongRule} / {summary.Duplicates}");
        var rejected = string.Create(culture, $"{summary.RejectedCorrect} / {summary.RejectedIncorrect}");
        var tokens = $"{Whole(summary.AverageInputTokens)} / {Whole(summary.AverageOutputTokens)}";
        text.AppendLine(culture, $"| False comments per clean pull request | {falseComments} |");
        text.AppendLine(culture, $"| Wrong place / wrong rule / duplicates | {mistakes} |");
        text.AppendLine(culture, $"| Verifier: rejected correct / rejected incorrect | {rejected} |");
        text.AppendLine(culture, $"| Rule text overlap (F1) | {Number(summary.RuleTextOverlap)} |");
        text.AppendLine(culture, $"| Completed / failed runs | {summary.CompletedRuns} / {summary.FailedRuns.Count} |");
        text.AppendLine(culture, $"| Time per review | {Number(summary.AverageDurationSeconds)} s |");
        text.AppendLine(culture, $"| Model calls per review | {Number(summary.AverageModelCalls)} |");
        text.AppendLine(culture, $"| Input / output tokens per review | {tokens} |");

        text.AppendLine();
        text.AppendLine("| Rule | Found | False comments |");
        text.AppendLine("| --- | --- | --- |");
        foreach (var rule in summary.Rules)
        {
            text.AppendLine(culture, $"| {rule.Rule} | {rule.Found} / {rule.Expected} | {rule.FalseComments} |");
        }

        text.AppendLine();
        text.AppendLine("| Task | Group | Found (runs) | False comments per run |");
        text.AppendLine("| --- | --- | --- | --- |");
        foreach (var task in summary.Tasks)
        {
            var findings = task.Findings.Count == 0
                ? NotMeasured
                : string.Join(
                    "<br>",
                    task.Findings.Select(finding => FindingText(finding, task.Runs)));
            text.AppendLine(
                culture,
                $"| {task.Id} | {task.Group} | {findings} | {Number(task.FalseCommentsPerRun)} |");
        }

        if (summary.FailedRuns.Count > 0)
        {
            text.AppendLine();
            text.AppendLine("Failed runs:");
            foreach (var failed in summary.FailedRuns)
            {
                text.AppendLine(culture, $"- {failed.TaskId} ({failed.Kind}): {failed.Reason}");
            }
        }

        return text.ToString();
    }

    private static string FindingText(FindingResult finding, int runs)
    {
        var place = string.Create(CultureInfo.InvariantCulture, $"{Path.GetFileName(finding.File)}:{finding.Line}");
        var optional = finding.Optional ? " (optional)" : string.Empty;
        var found = string.Create(CultureInfo.InvariantCulture, $"{finding.FoundIn}/{runs}");
        return $"{finding.Rule} `{place}`{optional}: {found}";
    }

    private static string Percent(double? value) =>
        value is null ? NotMeasured : value.Value.ToString("P0", CultureInfo.InvariantCulture);

    private static string Number(double? value) =>
        value is null ? NotMeasured : value.Value.ToString("0.##", CultureInfo.InvariantCulture);

    private static string Whole(double? value) =>
        value is null ? NotMeasured : value.Value.ToString("0", CultureInfo.InvariantCulture);
}
