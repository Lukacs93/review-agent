// Template for BIZ-01: the order the warehouse handles orders in.
// Code for this case must behave as this template does; it may be written differently.
using Orders.Api.Application;
using Orders.Api.Domain;

namespace Orders.Api.Templates;

public static class OrderPriority
{
    /// <summary>
    /// Placed orders only (cancelled and shipped ones are left out); priority customers' orders first; within
    /// each group, the oldest order first.
    /// </summary>
    public static IEnumerable<Order> InWarehouseOrder(IEnumerable<Order> orders, ICustomerTiers tiers) =>
        orders
            .Where(order => order.Status == OrderStatus.Placed)
            .OrderByDescending(order => tiers.IsPriority(order.CustomerId))
            .ThenBy(order => order.PlacedAt);
}
