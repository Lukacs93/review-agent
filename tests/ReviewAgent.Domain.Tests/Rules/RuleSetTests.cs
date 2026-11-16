using ReviewAgent.Domain.Rules;
using ReviewAgent.Domain.Rules.Errors;

namespace ReviewAgent.Domain.Tests.Rules;

public sealed class RuleSetTests
{
    private static Rule Architecture => new(new RuleId("ARCH-01"), "No database access in controllers", Severity.Error, "");

    private static Rule Naming => new(new RuleId("DDD-02"), "Ubiquitous language", Severity.Warning, "Use Cart, not Basket.");

    [Fact]
    public void Rule_is_found_by_its_id()
    {
        var rules = new RuleSet([Architecture, Naming]);

        var rule = rules.Find(new RuleId("ddd-02"));

        Assert.NotNull(rule);
        Assert.Equal(Severity.Warning, rule.Severity);
        Assert.Equal("Use Cart, not Basket.", rule.Description);
    }

    [Fact]
    public void Unknown_rule_is_not_found()
    {
        var rules = new RuleSet([Architecture]);

        Assert.Null(rules.Find(new RuleId("ABC-99")));
        Assert.False(rules.Contains(new RuleId("ABC-99")));
    }

    [Fact]
    public void Ids_keep_the_defined_order()
    {
        var rules = new RuleSet([Naming, Architecture]);

        Assert.Equal(new[] { new RuleId("DDD-02"), new RuleId("ARCH-01") }, rules.Ids);
    }

    [Fact]
    public void Empty_rule_set_is_rejected()
    {
        var error = Assert.Throws<DomainException>(() => new RuleSet([]));

        Assert.Equal(RuleErrors.EmptyRuleSet, error.Message);
    }

    [Fact]
    public void Duplicate_rule_is_rejected()
    {
        var error = Assert.Throws<DomainException>(() => new RuleSet([Architecture, Architecture]));

        Assert.Equal(RuleErrors.DuplicateRule(new RuleId("ARCH-01")), error.Message);
    }

    [Fact]
    public void Rule_without_title_is_rejected()
    {
        var error = Assert.Throws<DomainException>(
            () => new Rule(new RuleId("ARCH-01"), " ", Severity.Error, ""));

        Assert.Equal(RuleErrors.RuleTitleRequired, error.Message);
    }
}
