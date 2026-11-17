using System.ComponentModel;
using Microsoft.Extensions.AI;
using ReviewAgent.Application.Interfaces;
using ReviewAgent.Application.Reviews.Commands.ProposeComment;
using ReviewAgent.Application.SourceControl;
using ReviewAgent.Domain;
using ReviewAgent.Domain.Rules;
using ReviewAgent.Infrastructure.Agents.Tools.Errors;

namespace ReviewAgent.Infrastructure.Agents.Tools;

/// <summary>
/// The tool the model proposes review comments with. The code checks every proposal before it is saved: the file
/// is changed, the line is in its diff, and the rule exists. The severity comes from the team's rule, not from the
/// model. A proposal that fails a check is not saved; the model gets the reason, so it can correct it.
/// </summary>
internal sealed class ProposeCommentTool
{
    public const string Name = "propose_comment";

    private const string Description =
        "Proposes a review comment on one line of a changed file, for code that breaks a team rule. "
        + "A developer approves each comment before it is published. Returns whether the comment was accepted.";

    private readonly Guid _runId;
    private readonly Dictionary<string, IReadOnlySet<int>> _diffLinesByPath;
    private readonly RuleSet _rules;
    private readonly ICommandHandler<ProposeCommentCommand, Guid> _proposeComment;
    private readonly HashSet<(string Path, int Line, RuleId Rule)> _proposed = [];

    public ProposeCommentTool(
        Guid runId,
        PullRequestSnapshot pullRequest,
        RuleSet rules,
        ICommandHandler<ProposeCommentCommand, Guid> proposeComment)
    {
        ArgumentNullException.ThrowIfNull(pullRequest);

        _runId = runId;
        _diffLinesByPath = pullRequest.Files.ToDictionary(
            file => file.Path,
            file => DiffLines.InNewFile(file.Patch),
            StringComparer.Ordinal);
        _rules = rules;
        _proposeComment = proposeComment;
    }

    public AIFunction AsAIFunction() => AIFunctionFactory.Create(ProposeAsync, Name, Description);

    public async Task<string> ProposeAsync(
        [Description("Path of a changed file, as listed by get_pull_request.")] string path,
        [Description("Line number in the new version of the file. It must be a line shown in the file's diff.")]
        int line,
        [Description("ID of the team rule the code breaks, for example ARCH-01.")] string ruleId,
        [Description("The comment for the developer: what is wrong and why, in one or two sentences.")] string body,
        [Description("Optional: the corrected code, or a short description of the fix.")] string? suggestedFix = null,
        CancellationToken cancellationToken = default)
    {
        if (!_diffLinesByPath.TryGetValue(path, out var diffLines))
        {
            return ProposeCommentErrors.FileNotChanged(path);
        }

        if (!diffLines.Contains(line))
        {
            return ProposeCommentErrors.LineNotInDiff(path, line);
        }

        var rule = FindRule(ruleId);
        if (rule is null)
        {
            return ProposeCommentErrors.UnknownRule(ruleId, _rules.Ids.Select(id => id.Value));
        }

        if (string.IsNullOrWhiteSpace(body))
        {
            return ProposeCommentErrors.BodyRequired;
        }

        if (!_proposed.Add((path, line, rule.Id)))
        {
            return ProposeCommentErrors.Duplicate(path, line, rule.Id.Value);
        }

        await _proposeComment.HandleAsync(
            new ProposeCommentCommand(_runId, path, line, rule.Id.Value, rule.Severity, body, suggestedFix),
            cancellationToken);

        return "Comment proposed.";
    }

    private Rule? FindRule(string ruleId)
    {
        try
        {
            return _rules.Find(new RuleId(ruleId));
        }
        catch (DomainException)
        {
            // An empty rule ID: not a rule.
            return null;
        }
    }
}
