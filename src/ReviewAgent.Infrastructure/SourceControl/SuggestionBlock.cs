using System.Text.RegularExpressions;

namespace ReviewAgent.Infrastructure.SourceControl;

/// <summary>
/// The code block GitHub shows as a suggested change of the commented line, which the pull request's author can
/// apply with one click.
/// </summary>
internal static partial class SuggestionBlock
{
    // GitHub's language name for a suggested change.
    private const string Language = "suggestion";

    // A fenced code block needs at least three backticks (CommonMark).
    private const int MinimumFenceLength = 3;

    /// <summary>
    /// Builds the suggestion block around the replacement code; the code goes in as it is, line breaks as '\n'.
    /// </summary>
    public static string Build(string replacementCode)
    {
        ArgumentNullException.ThrowIfNull(replacementCode);

        var fence = FenceFor(replacementCode);
        return $"{fence}{Language}\n{replacementCode}\n{fence}";
    }

    // The fence must be longer than any run of backticks inside the block (CommonMark), so code that holds ```
    // cannot end the block early.
    private static string FenceFor(string code)
    {
        var longestBacktickRun = BacktickRuns().Matches(code)
            .Select(run => run.Length)
            .DefaultIfEmpty(0)
            .Max();

        return new string('`', Math.Max(MinimumFenceLength, longestBacktickRun + 1));
    }

    // One or more backticks in a row.
    [GeneratedRegex("`+")]
    private static partial Regex BacktickRuns();
}
