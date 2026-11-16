using System.Globalization;

namespace ReviewAgent.Infrastructure.Rules.Errors;

/// <summary>The messages of the <see cref="FormatException"/>s thrown when a rules file cannot be read.</summary>
internal static class RuleFileErrors
{
    public static string InvalidRuleHeading(int lineNumber, string line) =>
        string.Create(
            CultureInfo.InvariantCulture,
            $"Line {lineNumber}: '{line}' is not a valid rule heading. Expected '### ID – Title (error|warning)'.");
}
