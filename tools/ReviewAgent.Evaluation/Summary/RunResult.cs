using System.Globalization;
using Microsoft.Extensions.AI.Evaluation;
using Microsoft.Extensions.AI.Evaluation.Reporting;
using ReviewAgent.Evaluation.Grading;

namespace ReviewAgent.Evaluation.Summary;

/// <summary>One stored run, read back from the metrics <see cref="ReviewEvaluator"/> recorded.</summary>
internal sealed record RunResult(
    string TaskId,
    bool Completed,
    string? FailureReason,
    string? FailureKind,
    string? Commit,
    IReadOnlyList<bool> FoundFindings,
    IReadOnlyDictionary<string, int> FalseCommentsByRule,
    IReadOnlyDictionary<string, double?> Measures)
{
    public static RunResult From(ScenarioRunResult result, int findingCount)
    {
        ArgumentNullException.ThrowIfNull(result);

        var metrics = result.EvaluationResult.Metrics;
        var run = (StringMetric)metrics[ReviewMetrics.Run];
        var found = metrics[ReviewMetrics.Found];
        var falseComments = metrics[ReviewMetrics.FalseComments];

        return new RunResult(
            result.ScenarioName,
            run.Value == ReviewMetrics.Completed,
            run.Reason,
            MetadataOf(run, ReviewMetrics.FailureKindKey),
            MetadataOf(run, ReviewMetrics.CommitKey),
            Enumerable.Range(0, findingCount)
                .Select(index => MetadataOf(found, ReviewMetrics.FindingKey(index)) == bool.TrueString)
                .ToList(),
            (falseComments.Metadata ?? new Dictionary<string, string>())
                .ToDictionary(entry => entry.Key, entry => int.Parse(entry.Value, CultureInfo.InvariantCulture)),
            metrics.Values.OfType<NumericMetric>().ToDictionary(metric => metric.Name, metric => metric.Value));
    }

    /// <summary>A measure of the run; zero when it was not reported, as by an API that gives no token counts.</summary>
    public double Measure(string name) => Measures.GetValueOrDefault(name) ?? 0;

    private static string? MetadataOf(EvaluationMetric metric, string key) =>
        metric.Metadata is not null && metric.Metadata.TryGetValue(key, out var value) ? value : null;
}
