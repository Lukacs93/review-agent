namespace Orders.Api.Application;

public sealed record BasketItem(string ProductCode, int Quantity, decimal UnitPrice);

/// <summary>The customer's basket that becomes an order.</summary>
public sealed record Basket(Guid CustomerId, IReadOnlyList<BasketItem> Items)
{
    public PlaceOrderCommand ToPlaceOrderCommand() =>
        new(CustomerId, Items.Select(i => new PlaceOrderLine(i.ProductCode, i.Quantity, i.UnitPrice)).ToList());
}
