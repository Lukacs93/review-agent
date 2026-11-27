using ReviewAgent.Application.SourceControl;
using ReviewAgent.Domain.Rules;
using ReviewAgent.Evaluation.Dataset;

namespace ReviewAgent.Evaluation.Pulls;

/// <summary>
/// Checks a task once its pull request is assembled, before any review starts: a mistake in the dataset stops the
/// measurement at once, instead of counting as the agent's miss after many model calls.
/// </summary>
internal static class TaskValidator
{
    public static IReadOnlyList<string> Validate(AssembledPullRequest pullRequest)
    {
        ArgumentNullException.ThrowIfNull(pullRequest);

        var task = pullRequest.Task;
        var problems = new List<string>();

        var requiredCount = task.Expected.Count(finding => !finding.Optional);
        if (task.Group == TaskGroup.Positive && requiredCount != 1)
        {
            problems.Add(EvaluationErrors.PositiveTaskNeedsOneFinding(task.Id, requiredCount));
        }

        if (task.Group == TaskGroup.Negative && task.Expected.Count > 0)
        {
            problems.Add(EvaluationErrors.NegativeTaskHasFindings(task.Id));
        }

        foreach (var finding in task.Expected)
        {
            problems.AddRange(Validate(pullRequest, finding));
        }

        return problems;
    }

    private static List<string> Validate(AssembledPullRequest pullRequest, ExpectedFinding finding)
    {
        var taskId = pullRequest.Task.Id;
        var problems = new List<string>();

        if (!pullRequest.Rules.Contains(new RuleId(finding.Rule)))
        {
            problems.Add(EvaluationErrors.UnknownRule(taskId, finding.Rule));
        }

        if (finding.Lines.Count == 0)
        {
            problems.Add(EvaluationErrors.FindingWithoutLines(taskId, finding.Rule, finding.File));
        }

        var file = pullRequest.Files.FirstOrDefault(changed => changed.Path == finding.File);
        if (file is null)
        {
            problems.Add(EvaluationErrors.FileNotChanged(taskId, finding.File));
            return problems;
        }

        // The agent can only comment on the lines of the diff, so an expected line outside it could never be found.
        var diffLines = DiffLines.Number(file.Patch)
            .Where(line => line.NewFileLine is not null)
            .Select(line => line.NewFileLine!.Value)
            .ToHashSet();
        var content = File.ReadAllLines(Path.Combine(pullRequest.RepositoryDirectory, finding.File));

        foreach (var line in finding.Lines)
        {
            if (!diffLines.Contains(line.Number))
            {
                problems.Add(EvaluationErrors.LineNotInDiff(taskId, finding.File, line.Number));
                continue;
            }

            var actualCode = content[line.Number - 1].Trim();
            if (actualCode != line.Code)
            {
                problems.Add(EvaluationErrors.CodeDiffers(taskId, finding.File, line.Number, line.Code, actualCode));
            }
        }

        return problems;
    }
}
