using System.Text;
using System.Text.RegularExpressions;
using ReviewAgent.Domain.Rules;
using ReviewAgent.Infrastructure.Rules.Errors;

namespace ReviewAgent.Infrastructure.Rules;

/// <summary>
/// Reads a team's rules file (<c>.review/rules.md</c>). Every level-3 heading is a rule in the form
/// <c>### ARCH-01 – Title (error)</c>; its description runs until the next heading.
/// </summary>
public static partial class MarkdownRuleSetParser
{
    private const string RuleHeadingPrefix = "### ";

    public static RuleSet Parse(string markdown)
    {
        ArgumentNullException.ThrowIfNull(markdown);

        var rules = new List<Rule>();
        RuleHeading? current = null;
        var description = new StringBuilder();

        var lines = markdown.Split('\n');
        for (var index = 0; index < lines.Length; index++)
        {
            var line = lines[index].TrimEnd('\r');

            if (!line.StartsWith('#'))
            {
                if (current is not null)
                {
                    description.Append(line).Append('\n');
                }

                continue;
            }

            if (current is not null)
            {
                rules.Add(current.ToRule(description.ToString()));
                current = null;
                description.Clear();
            }

            if (line.StartsWith(RuleHeadingPrefix, StringComparison.Ordinal))
            {
                current = ParseHeading(line, index + 1);
            }
        }

        if (current is not null)
        {
            rules.Add(current.ToRule(description.ToString()));
        }

        return new RuleSet(rules);
    }

    private static RuleHeading ParseHeading(string line, int lineNumber)
    {
        var match = RuleHeadingPattern().Match(line);
        if (!match.Success)
        {
            throw new FormatException(RuleFileErrors.InvalidRuleHeading(lineNumber, line));
        }

        var severity = string.Equals(match.Groups["severity"].Value, "error", StringComparison.OrdinalIgnoreCase)
            ? Severity.Error
            : Severity.Warning;

        return new RuleHeading(new RuleId(match.Groups["id"].Value), match.Groups["title"].Value, severity);
    }

    // "### ARCH-01 – Controllers do not access the database directly (error)"; an en dash or a hyphen.
    [GeneratedRegex(
        @"^###\s+(?<id>[A-Za-z]+-[0-9]+)\s+[–-]\s+(?<title>.+?)\s+\((?<severity>error|warning)\)\s*$",
        RegexOptions.CultureInvariant | RegexOptions.IgnoreCase)]
    private static partial Regex RuleHeadingPattern();

    private sealed record RuleHeading(RuleId Id, string Title, Severity Severity)
    {
        public Rule ToRule(string description) => new(Id, Title, Severity, description);
    }
}
