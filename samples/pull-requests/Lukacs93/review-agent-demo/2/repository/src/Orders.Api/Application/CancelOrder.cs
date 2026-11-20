namespace Orders.Api.Application;

public sealed record CancelOrderCommand(Guid OrderId);

/// <summary>Command: cancel an order.</summary>
public sealed class CancelOrderHandler(IOrderRepository orders)
{
    public async Task<bool> HandleAsync(CancelOrderCommand command, CancellationToken cancellationToken)
    {
        var order = await orders.GetAsync(command.OrderId, cancellationToken);
        if (order is null)
        {
            return false;
        }

        order.Cancel();
        await orders.SaveChangesAsync(cancellationToken);
        return true;
    }
}
