using ReviewAgent.Application.Reviews.Queries.GetReviewRun;
using ReviewAgent.Evaluation.Dataset;

namespace ReviewAgent.Evaluation.Grading;

/// <summary>What a comment is worth, compared with the expected findings.</summary>
internal enum CommentVerdict
{
    /// <summary>Finds an expected finding: same rule and file, on one of its lines.</summary>
    Hit,

    /// <summary>The rule and file of a finding not found yet, on another line.</summary>
    WrongPlace,

    /// <summary>On a line of a required finding not found yet, with another rule.</summary>
    WrongRule,

    /// <summary>The rule and file of a finding another comment already found.</summary>
    Duplicate,

    /// <summary>Anything else: a comment the pull request did not call for.</summary>
    False,
}

/// <summary>A comment and its verdict.</summary>
/// <param name="FindingIndex">For a hit, the position of the expected finding it found.</param>
internal sealed record GradedComment(ReviewCommentDetails Comment, CommentVerdict Verdict, int? FindingIndex);

/// <summary>The verdict of each comment, and for each expected finding, in order, whether a comment found it.</summary>
internal sealed record GradedComments(IReadOnlyList<GradedComment> Comments, IReadOnlyList<bool> FoundFindings);

/// <summary>
/// Compares a review's comments with the expected findings. Every comment gets exactly one verdict, checked in this
/// order: hit, wrong place, wrong rule, duplicate, false. Every finding is found by at most one comment. The hits are
/// matched first, so the order of the comments does not decide which one is the hit and which one the duplicate.
/// </summary>
internal static class ReviewGrader
{
    public static GradedComments Grade(
        IReadOnlyList<ExpectedFinding> expected,
        IReadOnlyList<ReviewCommentDetails> comments)
    {
        ArgumentNullException.ThrowIfNull(expected);
        ArgumentNullException.ThrowIfNull(comments);

        var found = new bool[expected.Count];
        var verdicts = new CommentVerdict?[comments.Count];
        var findingIndexes = new int?[comments.Count];

        for (var commentIndex = 0; commentIndex < comments.Count; commentIndex++)
        {
            var comment = comments[commentIndex];
            var findingIndex = FindIndex(
                expected,
                (finding, index) => !found[index] && SameRuleAndFile(finding, comment) && OnLine(finding, comment));
            if (findingIndex >= 0)
            {
                found[findingIndex] = true;
                verdicts[commentIndex] = CommentVerdict.Hit;
                findingIndexes[commentIndex] = findingIndex;
            }
        }

        for (var commentIndex = 0; commentIndex < comments.Count; commentIndex++)
        {
            verdicts[commentIndex] ??= VerdictOfMiss(expected, found, comments[commentIndex]);
        }

        var graded = comments
            .Select((comment, index) => new GradedComment(comment, verdicts[index]!.Value, findingIndexes[index]))
            .ToList();
        return new GradedComments(graded, found);
    }

    private static CommentVerdict VerdictOfMiss(
        IReadOnlyList<ExpectedFinding> expected,
        bool[] found,
        ReviewCommentDetails comment)
    {
        if (FindIndex(expected, (finding, index) => !found[index] && SameRuleAndFile(finding, comment)) >= 0)
        {
            return CommentVerdict.WrongPlace;
        }

        if (FindIndex(
                expected,
                (finding, index) => !found[index] && !finding.Optional && SameFile(finding, comment)
                    && OnLine(finding, comment)) >= 0)
        {
            return CommentVerdict.WrongRule;
        }

        if (FindIndex(expected, (finding, index) => found[index] && SameRuleAndFile(finding, comment)) >= 0)
        {
            return CommentVerdict.Duplicate;
        }

        return CommentVerdict.False;
    }

    // The required findings first, so a comment that fits both finds the required one.
    private static int FindIndex(
        IReadOnlyList<ExpectedFinding> expected,
        Func<ExpectedFinding, int, bool> matches)
    {
        foreach (var optional in new[] { false, true })
        {
            for (var index = 0; index < expected.Count; index++)
            {
                if (expected[index].Optional == optional && matches(expected[index], index))
                {
                    return index;
                }
            }
        }

        return -1;
    }

    private static bool SameRuleAndFile(ExpectedFinding finding, ReviewCommentDetails comment) =>
        SameFile(finding, comment) && string.Equals(finding.Rule, comment.RuleId, StringComparison.OrdinalIgnoreCase);

    private static bool SameFile(ExpectedFinding finding, ReviewCommentDetails comment) =>
        string.Equals(finding.File, comment.FilePath, StringComparison.Ordinal);

    private static bool OnLine(ExpectedFinding finding, ReviewCommentDetails comment) =>
        finding.Lines.Any(line => line.Number == comment.Line);
}
