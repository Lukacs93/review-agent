namespace ReviewAgent.Evaluation.Dataset;

/// <summary>
/// A rule violation the review should find. A comment finds it when its rule and file match and its line is one
/// of <see cref="Lines"/>: a violation can rightly be pointed out on more than one line, but it counts once.
/// </summary>
/// <param name="Optional">
/// A violation a comment may point out but does not have to, for example one the existing code makes too; missing
/// it is no mistake, and pointing it out is no false comment.
/// </param>
/// <param name="Reason">Why the violation is optional.</param>
internal sealed record ExpectedFinding(
    string Rule,
    string File,
    IReadOnlyList<ExpectedLine> Lines,
    bool Optional,
    string? Reason);

/// <summary>A line a comment on the violation may be on, and its code, checked before the measurement starts.</summary>
internal sealed record ExpectedLine(int Number, string Code);
