using System.ComponentModel;
using System.Globalization;
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
/// is changed, the quoted code is on a line of its diff, and the rule exists. The severity comes from the team's
/// rule, not from the model. A proposal that fails a check is not saved; the model gets the reason, so it can
/// correct it.
/// </summary>
/// <remarks>
/// The line is found by the code the model quotes, not only by the number it gives, as Qodo PR-Agent does: a model
/// often gives a number one line off. When the quoted code is on the given line, that line is used; when it is on
/// exactly one other line of the diff, that line is used and the model is told; otherwise the comment is not saved.
/// </remarks>
internal sealed class ProposeCommentTool
{
    public const string Name = "propose_comment";

    private const string Description =
        "Proposes a review comment on one line of a changed file, for code that breaks a team rule. "
        + "A developer approves each comment before it is published. Returns whether the comment was accepted.";

    private readonly Guid _runId;
    // For each changed file: the code of each added or unchanged line of its diff, by its line number in the new
    // file. The code is the diff line without its first character, the '+' or ' ' that marks it.
    private readonly Dictionary<string, Dictionary<int, string>> _codeByLineByPath;
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
        _codeByLineByPath = pullRequest.Files.ToDictionary(
            file => file.Path,
            file => CodeByLine(file.Patch),
            StringComparer.Ordinal);
        _rules = rules;
        _proposeComment = proposeComment;
    }

    public AIFunction AsAIFunction() => AIFunctionFactory.Create(ProposeAsync, Name, Description);

    public async Task<string> ProposeAsync(
        [Description("Path of a changed file, as listed by get_pull_request.")] string path,
        [Description(
            "The line number get_diff shows before the line, for example 13 for '13 | +    public decimal ...'.")]
        int line,
        [Description(
            "The code of that line, as get_diff shows it after the number and the '+' or space, for example "
            + "'public decimal DiscountAmount { get; set; }'.")]
        string lineCode,
        [Description("ID of the team rule the code breaks, for example ARCH-01.")] string ruleId,
        [Description("The comment for the developer: what is wrong and why, in one or two sentences.")] string body,
        [Description("Optional: the corrected code, or a short description of the fix.")] string? suggestedFix = null,
        CancellationToken cancellationToken = default)
    {
        if (!_codeByLineByPath.TryGetValue(path, out var codeByLine))
        {
            return ProposeCommentErrors.FileNotChanged(path);
        }

        if (string.IsNullOrWhiteSpace(lineCode))
        {
            return ProposeCommentErrors.LineCodeRequired;
        }

        var quotedCode = lineCode.Trim();
        var linesWithTheCode = codeByLine
            .Where(codeOnLine => string.Equals(codeOnLine.Value.Trim(), quotedCode, StringComparison.Ordinal))
            .Select(codeOnLine => codeOnLine.Key)
            .Order()
            .ToList();

        if (linesWithTheCode.Count == 0)
        {
            return ProposeCommentErrors.CodeNotInDiff(path, quotedCode);
        }

        // The given line when the code is on it; otherwise the only line the code is on.
        int commentLine;
        if (linesWithTheCode.Contains(line))
        {
            commentLine = line;
        }
        else if (linesWithTheCode.Count == 1)
        {
            commentLine = linesWithTheCode.Single();
        }
        else
        {
            return ProposeCommentErrors.CodeOnSeveralLines(path, quotedCode, linesWithTheCode);
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

        if (!_proposed.Add((path, commentLine, rule.Id)))
        {
            return ProposeCommentErrors.Duplicate(path, commentLine, rule.Id.Value);
        }

        await _proposeComment.HandleAsync(
            new ProposeCommentCommand(_runId, path, commentLine, rule.Id.Value, rule.Severity, body, suggestedFix),
            cancellationToken);

        return commentLine == line ? "Comment proposed." : ProposedOnAnotherLine(commentLine, line);
    }

    // The comment was saved, but not on the given line: the model is told where, so it can check.
    private static string ProposedOnAnotherLine(int commentLine, int givenLine) =>
        string.Create(
            CultureInfo.InvariantCulture,
            $"Comment proposed on line {commentLine}: the quoted code is on line {commentLine}, not {givenLine}.");

    private static Dictionary<int, string> CodeByLine(string patch)
    {
        var codeByLine = new Dictionary<int, string>();
        foreach (var diffLine in DiffLines.Number(patch))
        {
            if (diffLine.NewFileLine.HasValue)
            {
                // Without the first character, the '+' or ' ' that marks an added or unchanged line.
                codeByLine[diffLine.NewFileLine.Value] = diffLine.Text.Substring(1);
            }
        }

        return codeByLine;
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
