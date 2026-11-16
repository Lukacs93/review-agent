namespace Orders.Api.Domain;

/// <summary>A single line of an order.</summary>
public sealed class OrderLine
{
    public string ProductCode { get; private set; } = "";
    public int Quantity { get; private set; }
    public decimal UnitPrice { get; private set; }

    public decimal LineTotal => UnitPrice * Quantity;

    private OrderLine() { } // EF Core

    public OrderLine(string productCode, int quantity, decimal unitPrice)
    {
        if (string.IsNullOrWhiteSpace(productCode))
        {
            throw new DomainException("Product code is required.");
        }

        if (quantity <= 0)
        {
            throw new DomainException("Quantity must be positive.");
        }

        if (unitPrice < 0)
        {
            throw new DomainException("Unit price cannot be negative.");
        }

        ProductCode = productCode;
        Quantity = quantity;
        UnitPrice = unitPrice;
    }
}
