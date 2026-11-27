using System.Globalization;

namespace ReviewAgent.Evaluation.Grading;

/// <summary>
/// The names of the metrics <see cref="ReviewEvaluator"/> records for each run, and the keys of their metadata.
/// </summary>
internal static class ReviewMetrics
{
    /// <summary>
    /// "Completed" or "Failed", with the failure's reason; its metadata says what the run was made with.
    /// </summary>
    public const string Run = "Run";

    /// <summary>How many required findings the kept comments found; its metadata tells each finding.</summary>
    public const string Found = "Found";

    public const string Required = "Required findings";
    public const string KeptComments = "Kept comments";

    /// <summary>The kept comments that found a finding, required or optional.</summary>
    public const string CorrectComments = "Correct comments";

    public const string WrongPlace = "Wrong place";
    public const string WrongRule = "Wrong rule";
    public const string Duplicates = "Duplicates";

    /// <summary>The kept comments the pull request did not call for; its metadata counts them by rule.</summary>
    public const string FalseComments = "False comments";

    /// <summary>
    /// The comments the verifier rejected that found a finding no kept comment found: its mistakes.
    /// </summary>
    public const string RejectedCorrect = "Rejected correct";

    /// <summary>The other comments the verifier rejected: what it filtered out.</summary>
    public const string RejectedIncorrect = "Rejected incorrect";

    /// <summary>How much a comment's words overlap with its rule's text, from 0 to 1, on average (F1).</summary>
    public const string RuleTextOverlap = "Rule text overlap";

    public const string ModelCalls = "Model calls";
    public const string InputTokens = "Input tokens";
    public const string OutputTokens = "Output tokens";
    public const string DurationSeconds = "Duration (s)";

    public const string Completed = "Completed";
    public const string Failed = "Failed";

    public const string CommitKey = "commit";
    public const string TaskFingerprintKey = "task-fingerprint";
    public const string FailureKindKey = "failure-kind";

    /// <summary>The metadata key of the expected finding at <paramref name="index"/>, on <see cref="Found"/>.</summary>
    public static string FindingKey(int index) =>
        string.Create(CultureInfo.InvariantCulture, $"finding-{index}");

    public static IReadOnlyCollection<string> Names { get; } =
    [
        Run, Found, Required, KeptComments, CorrectComments, WrongPlace, WrongRule, Duplicates, FalseComments,
        RejectedCorrect, RejectedIncorrect, RuleTextOverlap, ModelCalls, InputTokens, OutputTokens, DurationSeconds,
    ];
}
