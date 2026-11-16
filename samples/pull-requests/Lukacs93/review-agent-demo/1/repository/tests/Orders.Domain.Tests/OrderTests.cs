using Orders.Api.Domain;

namespace Orders.Domain.Tests;

public sealed class OrderTests
{
    private static readonly DateTimeOffset Now = new(2026, 11, 1, 10, 0, 0, TimeSpan.Zero);

    [Fact]
    public void Place_without_lines_is_rejected()
    {
        Assert.Throws<DomainException>(() => Order.Place(Guid.NewGuid(), [], Now));
    }

    [Fact]
    public void Total_is_the_sum_of_the_lines()
    {
        var order = Order.Place(Guid.NewGuid(),
        [
            new OrderLine("BOOK-1", 2, 4_500m),
            new OrderLine("PEN-7", 3, 600m),
        ], Now);

        Assert.Equal(10_800m, order.Total);
    }

    [Fact]
    public void Placed_order_can_be_cancelled()
    {
        var order = Order.Place(Guid.NewGuid(), [new OrderLine("BOOK-1", 1, 4_500m)], Now);

        order.Cancel();

        Assert.Equal(OrderStatus.Cancelled, order.Status);
    }

    [Fact]
    public void Cancelled_order_cannot_be_cancelled_again()
    {
        var order = Order.Place(Guid.NewGuid(), [new OrderLine("BOOK-1", 1, 4_500m)], Now);
        order.Cancel();

        Assert.Throws<DomainException>(order.Cancel);
    }

    [Theory]
    [InlineData("", 1, 100)]
    [InlineData("BOOK-1", 0, 100)]
    [InlineData("BOOK-1", 1, -1)]
    public void Invalid_order_line_is_rejected(string productCode, int quantity, decimal unitPrice)
    {
        Assert.Throws<DomainException>(() => new OrderLine(productCode, quantity, unitPrice));
    }
}
