using ReviewAgent.Domain.Reviews;
using ReviewAgent.Domain.Reviews.Errors;

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
    [InlineData("", 1, ValueObjectErrors.FilePathRequired)]
    [InlineData("/src/Order.cs", 1, ValueObjectErrors.FilePathNotRelative)]
    [InlineData("src/Order.cs", 0, ValueObjectErrors.LineNotPositive)]
    public void Invalid_location_is_rejected(string filePath, int line, string expectedMessage)
    {
        var error = Assert.Throws<DomainException>(() => new CodeLocation(filePath, line));

        Assert.Equal(expectedMessage, error.Message);
    }
}
