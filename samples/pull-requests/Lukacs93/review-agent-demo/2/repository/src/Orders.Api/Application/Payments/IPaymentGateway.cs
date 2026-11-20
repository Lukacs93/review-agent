namespace Orders.Api.Application.Payments;

/// <summary>A charge on a card the payment provider has tokenized.</summary>
public sealed record PaymentRequest(Guid OrderId, string PaymentToken, decimal ChargedAmount);

/// <summary>The approved client of the payment provider (SEC-01).</summary>
public interface IPaymentGateway
{
    /// <summary>Charges the card and returns the provider's payment ID.</summary>
    Task<string> ChargeAsync(PaymentRequest request, CancellationToken cancellationToken);
}
