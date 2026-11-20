using System.Net.Http.Json;
using Orders.Api.Application.Payments;

namespace Orders.Api.Infrastructure.Payments;

/// <summary>
/// Calls the payment provider. The HTTP client factory gives it the address and the key from the configuration
/// (see Program.cs).
/// </summary>
public sealed class PaymentGatewayClient(HttpClient http) : IPaymentGateway
{
    public async Task<string> ChargeAsync(PaymentRequest request, CancellationToken cancellationToken)
    {
        var response = await http.PostAsJsonAsync("v1/charges", request, cancellationToken);
        response.EnsureSuccessStatusCode();

        var charge = await response.Content.ReadFromJsonAsync<ChargeResponse>(cancellationToken);
        return charge?.Id ?? throw new InvalidOperationException("The payment provider returned no payment ID.");
    }

    private sealed record ChargeResponse(string Id);
}
