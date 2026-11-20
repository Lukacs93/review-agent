namespace Orders.Api.Application;

public sealed record GetOrderQuery(Guid OrderId);

public sealed record OrderLineDto(string ProductCode, int Quantity, decimal UnitPrice);

public sealed record OrderDto(Guid Id, Guid CustomerId, string Status, decimal Total, IReadOnlyList<OrderLineDto> Lines);

/// <summary>Query: the details of one order. Never changes state.</summary>
public sealed class GetOrderHandler(IOrderRepository orders)
{
    public async Task<OrderDto?> HandleAsync(GetOrderQuery query, CancellationToken cancellationToken)
    {
        var order = await orders.GetAsync(query.OrderId, cancellationToken);
        if (order is null)
        {
            return null;
        }

        return new OrderDto(
            order.Id,
            order.CustomerId,
            order.Status.ToString(),
            order.Total,
            order.Lines.Select(l => new OrderLineDto(l.ProductCode, l.Quantity, l.UnitPrice)).ToList());
    }
}
