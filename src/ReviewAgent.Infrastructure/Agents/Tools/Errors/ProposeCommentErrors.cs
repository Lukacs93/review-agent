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

    public const string LineCodeRequired =
        Prefix + "give the code of the line, as get_diff shows it after the line number and the '+' or space.";

    public static string CodeNotInDiff(string path, string code) =>
        $"{Prefix}'{code}' is not an added or unchanged line in the diff of '{path}'. Quote the code of the line "
        + "exactly as get_diff shows it.";

    public static string CodeOnSeveralLines(string path, string code, IEnumerable<int> lines) =>
        string.Create(
            CultureInfo.InvariantCulture,
            $"{Prefix}'{code}' is on several lines of the diff of '{path}' ({string.Join(", ", lines)}). "
            + $"Give the line number of the one you mean.");

    public static string UnknownRule(string ruleId, IEnumerable<string> knownRuleIds) =>
        $"{Prefix}'{ruleId}' is not a team rule. Use one of: {string.Join(", ", knownRuleIds)}.";

    public static string Duplicate(string path, int line, string ruleId) =>
        string.Create(
            CultureInfo.InvariantCulture,
            $"{Prefix}a comment on line {line} of '{path}' for {ruleId} was already proposed.");
}
