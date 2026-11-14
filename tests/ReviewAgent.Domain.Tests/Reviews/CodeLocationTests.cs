using ReviewAgent.Domain.Reviews;

namespace ReviewAgent.Domain.Tests.Reviews;

public sealed class CodeLocationTests
{
    [Fact]
    public void Windows_path_separators_are_normalized()
    {
        var location = new CodeLocation(@"src\Orders.Api\Domain\Order.cs", 12);

        Assert.Equal("src/Orders.Api/Domain/Order.cs", location.FilePath);
    }

    [Fact]
    public void ToString_uses_path_and_line()
    {
        var location = new CodeLocation("src/Order.cs", 12);

        Assert.Equal("src/Order.cs:12", location.ToString());
    }

    [Theory]
    [InlineData("", 1)]
    [InlineData("/src/Order.cs", 1)]
    [InlineData("src/Order.cs", 0)]
    public void Invalid_location_is_rejected(string filePath, int line)
    {
        Assert.Throws<DomainException>(() => new CodeLocation(filePath, line));
    }
}
