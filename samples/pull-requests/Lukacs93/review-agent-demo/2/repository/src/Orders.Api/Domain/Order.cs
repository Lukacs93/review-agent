namespace Orders.Api.Domain;

/// <summary>A customer's order. The business rules live here.</summary>
public sealed class Order
{
    private readonly List<OrderLine> _lines = [];

    public Guid Id { get; private set; }
    public Guid CustomerId { get; private set; }
    public OrderStatus Status { get; private set; }
    public DateTimeOffset PlacedAt { get; private set; }
    public IReadOnlyList<OrderLine> Lines => _lines;

    public decimal Total => _lines.Sum(line => line.LineTotal);

    private Order() { } // EF Core

    public static Order Place(Guid customerId, IEnumerable<OrderLine> lines, DateTimeOffset now)
    {
        var order = new Order
        {
            Id = Guid.NewGuid(),
            CustomerId = customerId,
            Status = OrderStatus.Placed,
            PlacedAt = now,
        };

        order._lines.AddRange(lines);

        if (order._lines.Count == 0)
        {
            throw new DomainException("An order must contain at least one line.");
        }

        return order;
    }

    public void Cancel()
    {
        if (Status != OrderStatus.Placed)
        {
            throw new DomainException("Only a placed order can be cancelled.");
        }

        Status = OrderStatus.Cancelled;
    }
}
