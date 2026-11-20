namespace Orders.Api.Application;

public sealed record GetPickListQuery;

public sealed record PickListEntry(Guid OrderId, Guid CustomerId, DateTimeOffset PlacedAt);

/// <summary>Query: the orders the warehouse picks next, in the order it picks them. Never changes state.</summary>
public sealed class GetPickListHandler(IOrderRepository orders)
{
    public async Task<IReadOnlyList<PickListEntry>> HandleAsync(GetPickListQuery query, CancellationToken cancellationToken)
    {
        var all = await orders.ListAsync(cancellationToken);

        return all
            .OrderByDescending(order => order.PlacedAt)
            .Select(order => new PickListEntry(order.Id, order.CustomerId, order.PlacedAt))
            .ToList();
    }
}
