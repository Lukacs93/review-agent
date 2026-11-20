using System.Net.Http.Json;

namespace Orders.Api.Application;

public sealed record PayOrderCommand(Guid OrderId, string CardNumber, double Total);

/// <summary>Command: pay an order by card. Returns the provider's payment ID, or null if the order is unknown.</summary>
public sealed class PayOrderHandler(IOrderRepository orders)
{
    private const string ChargesUrl = "https://payments.example.com/v1/charges";

    public async Task<string?> HandleAsync(PayOrderCommand command, CancellationToken cancellationToken)
    {
        var order = await orders.GetAsync(command.OrderId, cancellationToken);
        if (order is null)
        {
            return null;
        }

        using var http = new HttpClient();
        http.DefaultRequestHeaders.Add("X-Api-Key", "demo-payments-key-7f3a9c");

        var response = await http.PostAsJsonAsync(
            ChargesUrl,
            new { orderId = order.Id, cardNumber = command.CardNumber, total = command.Total },
            cancellationToken);
        response.EnsureSuccessStatusCode();

        var charge = await response.Content.ReadFromJsonAsync<ChargeResponse>(cancellationToken);
        return charge?.Id;
    }

    private sealed record ChargeResponse(string Id);
}
