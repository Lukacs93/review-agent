using Orders.Api.Application;
using Orders.Api.Domain;

namespace Orders.Api.Infrastructure;

public sealed class OrderRepository(OrdersDbContext db) : IOrderRepository
{
    public async Task AddAsync(Order order, CancellationToken cancellationToken) =>
        await db.Orders.AddAsync(order, cancellationToken);

    public async Task<Order?> GetAsync(Guid id, CancellationToken cancellationToken) =>
        await db.Orders.FindAsync([id], cancellationToken);

    public Task SaveChangesAsync(CancellationToken cancellationToken) =>
        db.SaveChangesAsync(cancellationToken);
}
