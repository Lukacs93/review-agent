using Microsoft.Extensions.AI.Evaluation;
using ReviewAgent.Domain.Rules;
using ReviewAgent.Evaluation.Dataset;
using ReviewAgent.Evaluation.Services;

namespace ReviewAgent.Evaluation.Grading;

/// <summary>
/// What <see cref="ReviewEvaluator"/> needs about one run: the task, the finished review and the team's rules.
/// </summary>
/// <param name="commit">review-agent's commit the run was made with.</param>
/// <param name="duration">How long the review took, from start to finish.</param>
internal sealed class ReviewRunContext(
    EvaluationTask task,
    CompletedReview review,
    RuleSet rules,
    string commit,
    TimeSpan duration)
    : EvaluationContext(ContextName, $"Task {task.Id}, review {review.Details.Id}")
{
    public const string ContextName = "Review run";

    public EvaluationTask Task { get; } = task;

    public CompletedReview Review { get; } = review;

    public RuleSet Rules { get; } = rules;

    public string Commit { get; } = commit;

    public TimeSpan Duration { get; } = duration;
}
