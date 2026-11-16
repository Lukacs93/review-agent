using ReviewAgent.Domain;
using ReviewAgent.Domain.Rules;
using ReviewAgent.Domain.Rules.Errors;
using ReviewAgent.Infrastructure.Rules;
using ReviewAgent.Infrastructure.Rules.Errors;

namespace ReviewAgent.Infrastructure.Tests.Rules;

public sealed class MarkdownRuleSetParserTests
{
    private const string RulesFile = """
        # Team rules – Orders service

        Every review comment must reference one of the rules below by its ID.

        ## Architecture

        ### ARCH-01 – Controllers do not access the database directly (error)

        Classes in `Controllers/` may only use application-layer handlers.

        ## Domain-Driven Design

        ### DDD-02 – Ubiquitous language (warning)

        Names follow the glossary.

        | Term | Do not use |
        | --- | --- |
        | `Cart` | Basket, Bag |
        """;

    [Fact]
    public void Every_level_three_heading_is_a_rule()
    {
        var rules = MarkdownRuleSetParser.Parse(RulesFile);

        Assert.Equal(new[] { new RuleId("ARCH-01"), new RuleId("DDD-02") }, rules.Ids);
    }

    [Fact]
    public void Heading_gives_the_title_and_the_severity()
    {
        var rules = MarkdownRuleSetParser.Parse(RulesFile);

        var architecture = rules.Find(new RuleId("ARCH-01"));
        var naming = rules.Find(new RuleId("DDD-02"));

        Assert.NotNull(architecture);
        Assert.NotNull(naming);
        Assert.Equal("Controllers do not access the database directly", architecture.Title);
        Assert.Equal(Severity.Error, architecture.Severity);
        Assert.Equal(Severity.Warning, naming.Severity);
    }

    [Fact]
    public void Description_runs_until_the_next_heading()
    {
        var rules = MarkdownRuleSetParser.Parse(RulesFile);

        var architecture = rules.Find(new RuleId("ARCH-01"));
        var naming = rules.Find(new RuleId("DDD-02"));

        Assert.Equal("Classes in `Controllers/` may only use application-layer handlers.", architecture?.Description);
        Assert.Contains("| `Cart` | Basket, Bag |", naming?.Description, StringComparison.Ordinal);
    }

    [Fact]
    public void Windows_line_endings_are_supported()
    {
        var rules = MarkdownRuleSetParser.Parse(RulesFile.ReplaceLineEndings("\r\n"));

        Assert.Equal(
            "Classes in `Controllers/` may only use application-layer handlers.",
            rules.Find(new RuleId("ARCH-01"))?.Description);
    }

    [Fact]
    public void Hyphen_is_accepted_instead_of_an_en_dash()
    {
        var rules = MarkdownRuleSetParser.Parse("### TEST-01 - New domain methods need unit tests (ERROR)");

        Assert.Equal(Severity.Error, Assert.Single(rules.Rules).Severity);
    }

    [Fact]
    public void Invalid_rule_heading_is_rejected_with_its_line_number()
    {
        const string rulesFile = "# Rules\n\n### ARCH-01 – Missing severity\n";

        var error = Assert.Throws<FormatException>(() => MarkdownRuleSetParser.Parse(rulesFile));

        Assert.Equal(RuleFileErrors.InvalidRuleHeading(3, "### ARCH-01 – Missing severity"), error.Message);
    }

    [Fact]
    public void File_without_rules_is_rejected()
    {
        var error = Assert.Throws<DomainException>(() => MarkdownRuleSetParser.Parse("# Rules\n\nNothing here yet."));

        Assert.Equal(RuleErrors.EmptyRuleSet, error.Message);
    }
}
