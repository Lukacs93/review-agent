using System.Globalization;

namespace ReviewAgent.Infrastructure.Agents.Tools.Errors;

/// <summary>
/// What <c>propose_comment</c> answers when it does not accept a comment. The model reads these, so each one says
/// what to correct.
/// </summary>
internal static class ProposeCommentErrors
{
    private const string Prefix = "Not proposed: ";

    public const string BodyRequired = Prefix + "the comment body is empty.";

    public static string FileNotChanged(string path) =>
        $"{Prefix}'{path}' is not changed in this pull request. Use a path listed by get_pull_request.";

    public static string LineNotInDiff(string path, int line) =>
        string.Create(
            CultureInfo.InvariantCulture,
            $"{Prefix}line {line} of '{path}' is not in the diff. Use a line number of the new file from the "
            + $"@@ headers of get_diff (an added or unchanged line shown in the diff).");

    public static string UnknownRule(string ruleId, IEnumerable<string> knownRuleIds) =>
        $"{Prefix}'{ruleId}' is not a team rule. Use one of: {string.Join(", ", knownRuleIds)}.";

    public static string Duplicate(string path, int line, string ruleId) =>
        string.Create(
            CultureInfo.InvariantCulture,
            $"{Prefix}a comment on line {line} of '{path}' for {ruleId} was already proposed.");
}
