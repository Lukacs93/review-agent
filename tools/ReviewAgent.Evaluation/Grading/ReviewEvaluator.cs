using System.Globalization;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.AI.Evaluation;
using Microsoft.Extensions.AI.Evaluation.NLP;
using ReviewAgent.Application.Reviews.Queries.GetReviewRun;
using ReviewAgent.Domain.Reviews;
using ReviewAgent.Domain.Rules;
using ReviewAgent.Evaluation.Dataset;

namespace ReviewAgent.Evaluation.Grading;

/// <summary>
/// Grades one review run against its task's expected findings (see <see cref="ReviewGrader"/>), with code, no model.
/// The kept comments (the ones the verifier did not reject) are what the developer gets; the comments before the
/// verifier show what it changed. A failed run records its failure and no comments.
/// </summary>
internal sealed class ReviewEvaluator : IEvaluator
{
    private readonly F1Evaluator _ruleTextOverlap = new();

    public IReadOnlyCollection<string> EvaluationMetricNames => ReviewMetrics.Names;

    public async ValueTask<EvaluationResult> EvaluateAsync(
        IEnumerable<ChatMessage> messages,
        ChatResponse modelResponse,
        ChatConfiguration? chatConfiguration = null,
        IEnumerable<EvaluationContext>? additionalContext = null,
        CancellationToken cancellationToken = default)
    {
        var context = additionalContext?.OfType<ReviewRunContext>().FirstOrDefault()
            ?? throw new InvalidOperationException(EvaluationErrors.ContextMissing(ReviewRunContext.ContextName));
        var details = context.Review.Details;
        var expected = context.Task.Expected;
        var failed = details.Status == ReviewStatus.Failed;

        // A failed review gives the developer nothing, so its comments are not graded.
        IReadOnlyList<ReviewCommentDetails> comments = failed ? [] : details.Comments;
        var kept = comments.Where(comment => comment.RejectionReason is null).ToList();
        var afterVerifier = ReviewGrader.Grade(expected, kept);
        var beforeVerifier = ReviewGrader.Grade(expected, comments);
        var rejected = beforeVerifier.Comments.Where(graded => graded.Comment.RejectionReason is not null).ToList();

        // A rejected comment is the verifier's mistake only when no kept comment found the same finding: when one did,
        // the developer still gets it.
        var rejectedCorrect = rejected.Count(graded =>
            graded.FindingIndex is { } findingIndex && !afterVerifier.FoundFindings[findingIndex]);

        var metrics = new List<EvaluationMetric>
        {
            RunMetric(context, failed),
            FoundMetric(expected, afterVerifier.FoundFindings),
            new NumericMetric(ReviewMetrics.Required, expected.Count(finding => !finding.Optional)),
            new NumericMetric(ReviewMetrics.KeptComments, kept.Count),
            Count(ReviewMetrics.CorrectComments, afterVerifier, CommentVerdict.Hit),
            Count(ReviewMetrics.WrongPlace, afterVerifier, CommentVerdict.WrongPlace),
            Count(ReviewMetrics.WrongRule, afterVerifier, CommentVerdict.WrongRule),
            Count(ReviewMetrics.Duplicates, afterVerifier, CommentVerdict.Duplicate),
            FalseCommentsMetric(afterVerifier),
            new NumericMetric(ReviewMetrics.RejectedCorrect, rejectedCorrect),
            new NumericMetric(ReviewMetrics.RejectedIncorrect, rejected.Count - rejectedCorrect),
            new NumericMetric(
                ReviewMetrics.RuleTextOverlap,
                await AverageRuleTextOverlapAsync(kept, context.Rules, cancellationToken)),
            new NumericMetric(ReviewMetrics.ModelCalls, details.ModelCallCount),
            new NumericMetric(ReviewMetrics.InputTokens, details.InputTokenCount),
            new NumericMetric(ReviewMetrics.OutputTokens, details.OutputTokenCount),
            new NumericMetric(ReviewMetrics.DurationSeconds, context.Duration.TotalSeconds),
        };

        return new EvaluationResult(metrics);
    }

    private static StringMetric RunMetric(ReviewRunContext context, bool failed)
    {
        var details = context.Review.Details;
        var metric = new StringMetric(
            ReviewMetrics.Run,
            failed ? ReviewMetrics.Failed : ReviewMetrics.Completed,
            failed ? details.FailureReason : null);
        metric.AddOrUpdateMetadata(ReviewMetrics.CommitKey, context.Commit);
        metric.AddOrUpdateMetadata(ReviewMetrics.TaskFingerprintKey, context.Task.Fingerprint);
        if (details.FailureKind is { } kind)
        {
            metric.AddOrUpdateMetadata(ReviewMetrics.FailureKindKey, kind.ToString());
        }

        return metric;
    }

    private static NumericMetric FoundMetric(IReadOnlyList<ExpectedFinding> expected, IReadOnlyList<bool> found)
    {
        var requiredFound = Enumerable.Range(0, found.Count).Count(index => found[index] && !expected[index].Optional);
        var metric = new NumericMetric(ReviewMetrics.Found, requiredFound);
        for (var index = 0; index < found.Count; index++)
        {
            metric.AddOrUpdateMetadata(
                ReviewMetrics.FindingKey(index),
                found[index] ? bool.TrueString : bool.FalseString);
        }

        return metric;
    }

    private static NumericMetric Count(string name, GradedComments graded, CommentVerdict verdict) =>
        new(name, graded.Comments.Count(comment => comment.Verdict == verdict));

    private static NumericMetric FalseCommentsMetric(GradedComments graded)
    {
        var falseComments = graded.Comments.Where(comment => comment.Verdict == CommentVerdict.False).ToList();
        var metric = new NumericMetric(ReviewMetrics.FalseComments, falseComments.Count);
        var byRules = falseComments.GroupBy(comment => comment.Comment.RuleId, StringComparer.OrdinalIgnoreCase);
        foreach (var byRule in byRules)
        {
            metric.AddOrUpdateMetadata(byRule.Key, byRule.Count().ToString(CultureInfo.InvariantCulture));
        }

        return metric;
    }

    // The F1 score of each kept comment against its rule's text; a high score means the comment repeats the rule
    // instead of saying what is wrong in the code.
    private async Task<double?> AverageRuleTextOverlapAsync(
        List<ReviewCommentDetails> kept,
        RuleSet rules,
        CancellationToken cancellationToken)
    {
        var scores = new List<double>();
        foreach (var comment in kept)
        {
            var rule = rules.Find(new RuleId(comment.RuleId));
            if (rule is null)
            {
                continue;
            }

            var result = await _ruleTextOverlap.EvaluateAsync(
                messages: [],
                new ChatResponse(new ChatMessage(ChatRole.Assistant, comment.Body)),
                chatConfiguration: null,
                additionalContext: [new F1EvaluatorContext(rule.Description)],
                cancellationToken);
            if (result.Metrics.Values.OfType<NumericMetric>().Single().Value is { } score)
            {
                scores.Add(score);
            }
        }

        return scores.Count == 0 ? null : scores.Average();
    }
}
