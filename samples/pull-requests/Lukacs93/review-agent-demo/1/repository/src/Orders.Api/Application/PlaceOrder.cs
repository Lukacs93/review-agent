using Orders.Api.Domain;

namespace Orders.Api.Application;

public sealed record PlaceOrderLine(string ProductCode, int Quantity, decimal UnitPrice);

public sealed record PlaceOrderCommand(Guid CustomerId, IReadOnlyList<PlaceOrderLine> Lines);

/// <summary>Command: place a new order.</summary>
public sealed class PlaceOrderHandler(IOrderRepository orders, TimeProvider clock)
{
    public async Task<Guid> HandleAsync(PlaceOrderCommand command, CancellationToken cancellationToken)
    {
        var lines = command.Lines.Select(l => new OrderLine(l.ProductCode, l.Quantity, l.UnitPrice));
        var order = Order.Place(command.CustomerId, lines, clock.GetUtcNow());

        await orders.AddAsync(order, cancellationToken);
        await orders.SaveChangesAsync(cancellationToken);

        return order.Id;
    }
}
