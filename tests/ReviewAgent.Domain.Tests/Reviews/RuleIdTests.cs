using ReviewAgent.Domain.Reviews;

namespace ReviewAgent.Domain.Tests.Reviews;

public sealed class RuleIdTests
{
    [Theory]
    [InlineData("ARCH-01", "ARCH-01")]
    [InlineData(" ddd-02 ", "DDD-02")]
    public void Rule_id_is_trimmed_and_upper_cased(string input, string expected)
    {
        Assert.Equal(expected, new RuleId(input).Value);
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public void Empty_rule_id_is_rejected(string input)
    {
        Assert.Throws<DomainException>(() => new RuleId(input));
    }

    [Fact]
    public void Null_rule_id_is_rejected()
    {
        Assert.Throws<DomainException>(() => new RuleId(null!));
    }

    [Fact]
    public void Rule_ids_with_the_same_value_are_equal()
    {
        Assert.Equal(new RuleId("arch-01"), new RuleId("ARCH-01"));
    }
}
