using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Orders.Api.Application;
using Orders.Api.Infrastructure;

namespace Orders.Api.Controllers;

[ApiController]
[Route("orders")]
public sealed class OrdersController(
    PlaceOrderHandler placeOrder,
    GetOrderHandler getOrder,
    CancelOrderHandler cancelOrder,
    OrdersDbContext db) : ControllerBase
{
    [HttpPost]
    public async Task<IActionResult> Place(PlaceOrderCommand command, CancellationToken cancellationToken)
    {
        var id = await placeOrder.HandleAsync(command, cancellationToken);
        return CreatedAtAction(nameof(Get), new { id }, new { id });
    }

    [HttpGet("{id:guid}")]
    public async Task<IActionResult> Get(Guid id, CancellationToken cancellationToken)
    {
        var order = await getOrder.HandleAsync(new GetOrderQuery(id), cancellationToken);
        return order is null ? NotFound() : Ok(order);
    }

    [HttpPost("{id:guid}/cancel")]
    public async Task<IActionResult> Cancel(Guid id, CancellationToken cancellationToken)
    {
        var found = await cancelOrder.HandleAsync(new CancelOrderCommand(id), cancellationToken);
        return found ? NoContent() : NotFound();
    }

    [HttpPost("{id:guid}/discount")]
    public async Task<IActionResult> ApplyDiscount(Guid id, CancellationToken cancellationToken)
    {
        var order = await db.Orders.FirstOrDefaultAsync(o => o.Id == id, cancellationToken);
        if (order is null)
        {
            return NotFound();
        }

        // 10% off above 50,000 HUF, another 5% for loyal customers (more than 5 orders)
        var discount = 0m;
        if (order.Total > 50_000m)
        {
            discount = order.Total * 0.10m;
        }

        var previousOrders = await db.Orders.CountAsync(o => o.CustomerId == order.CustomerId, cancellationToken);
        if (previousOrders > 5)
        {
            discount += order.Total * 0.05m;
        }

        order.DiscountAmount = discount;
        await db.SaveChangesAsync(cancellationToken);

        return Ok(new { order.Id, order.Total, order.DiscountAmount });
    }
}
