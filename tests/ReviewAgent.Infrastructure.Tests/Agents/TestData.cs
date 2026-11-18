using ReviewAgent.Application.SourceControl;
using ReviewAgent.Domain.Rules;

namespace ReviewAgent.Infrastructure.Tests.Agents;

/// <summary>A pull request and team rules for the agent tests.</summary>
internal static class TestData
{
    public const string CommitSha = "9fceb02d0ae598e95dc970b74767f19372d61af8";

    public const string RulesMarkdown =
        """
        # Team rules

        ### ARCH-01 – Controllers do not access the database directly (error)
        Controllers call application handlers; they do not use the DbContext.

        ### TEST-01 – New behaviour has tests (warning)
        Every new endpoint or domain method has a test.
        """;

    public static RuleSet Rules { get; } = new(
    [
        new Rule(new RuleId("ARCH-01"), "Controllers do not access the database directly", Severity.Error, string.Empty),
        new Rule(new RuleId("TEST-01"), "New behaviour has tests", Severity.Warning, string.Empty),
    ]);

    public static PullRequestSnapshot PullRequest(string filePath, string patch) =>
        new(
            "Add order discounts",
            "Adds a discount endpoint.",
            "main",
            "feature/discounts",
            [new ChangedFile(filePath, FileChangeStatus.Modified, patch)],
            CommitSha);
}
