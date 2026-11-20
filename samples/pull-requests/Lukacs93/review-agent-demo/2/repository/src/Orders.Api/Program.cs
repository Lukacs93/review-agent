using Microsoft.EntityFrameworkCore;
using Orders.Api.Application;
using Orders.Api.Application.Payments;
using Orders.Api.Infrastructure;
using Orders.Api.Infrastructure.Payments;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddDbContext<OrdersDbContext>(options =>
    options.UseSqlite(builder.Configuration.GetConnectionString("Orders") ?? "Data Source=orders.db"));

builder.Services.AddScoped<IOrderRepository, OrderRepository>();
builder.Services.AddScoped<PlaceOrderHandler>();
builder.Services.AddScoped<GetOrderHandler>();
builder.Services.AddScoped<CancelOrderHandler>();
builder.Services.AddScoped<PayOrderHandler>();
builder.Services.AddScoped<GetPickListHandler>();
builder.Services.AddSingleton(TimeProvider.System);
builder.Services.AddSingleton<ICustomerTiers, CustomerTiers>();

// The approved payment client (SEC-01). The key comes from the configuration: user secrets in development,
// an environment variable in production (SEC-02).
builder.Services.AddHttpClient<IPaymentGateway, PaymentGatewayClient>(http =>
{
    http.BaseAddress = new Uri(builder.Configuration["Payments:Url"]!);
    http.DefaultRequestHeaders.Add("X-Api-Key", builder.Configuration["Payments:ApiKey"]);
});

builder.Services.AddControllers();

var app = builder.Build();

using (var scope = app.Services.CreateScope())
{
    scope.ServiceProvider.GetRequiredService<OrdersDbContext>().Database.EnsureCreated();
}

app.MapControllers();
app.Run();
