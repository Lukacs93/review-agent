# Team rules – Orders service

This file is the context for the code review agent. Every review comment must reference one of the rules
below by its ID. If a change breaks none of them, no comment is needed.

Severity: **error** = must be fixed before merge, **warning** = should be fixed.

## Architecture

### ARCH-01 – Controllers do not access the database directly (error)

Classes in `Controllers/` may only use application-layer handlers (`*Handler`).
`OrdersDbContext`, `DbSet` or EF Core queries must not appear in a controller.

## Domain-Driven Design

### DDD-01 – Business rules live in the entity (error)

Calculations, decisions and state changes that belong to the order's business rules (for example discounts,
shipping, cancellation conditions) belong in the entities under `Domain/`, as methods. Controllers and handlers
must not contain such logic, and entity properties must not get a public setter.

### DDD-02 – Ubiquitous language (warning)

Names follow the glossary. Do not introduce synonyms.

| Term | Meaning | Do not use |
| --- | --- | --- |
| `Cart` | the customer's cart before the order is placed | Basket, Bag |
| `Order` | a placed order | Purchase |
| `OrderLine` | a single line of an order | Item, Position |
| `Customer` | the buyer | Client, User |
| `Discount` | a price reduction | Rebate, Reduction |

## Testing

### TEST-01 – New domain methods need unit tests (error)

If a change adds a new public method to a class under `Domain/`, the same pull request must add a test for it
in the `tests/Orders.Domain.Tests` project.

## CQRS

### CQRS-01 – Queries do not change state (error)

A `*Query` and its handler only read. They must not call `SaveChangesAsync` or modify an entity.

## Security

### SEC-01 – External APIs only through the approved client (error)

The payment provider is called only through `IPaymentGateway` (`Application/Payments/`), whose implementation in
`Infrastructure/Payments/` gets the address and the key from the configuration. Do not create an
`HttpClient` or call the provider's address anywhere else.

### SEC-02 – No secrets in code (error)

Keys, passwords and tokens never appear in the code or in `appsettings.json`. They come from the configuration:
user secrets in development, environment variables in production.

## Naming

### NAME-01 – Names of sensitive and financial data (error)

Sensitive and financial data use the names below, so audits and log masking find them.

| Data | Name | Do not use |
| --- | --- | --- |
| the token the payment provider gives for a card | `PaymentToken` | CardNumber, Card, Pan |
| an amount of money, always `decimal` | `...Amount`, for example `ChargedAmount` | Price, Sum, Value; `double`, `float` |
| the customer's tax number | `TaxNumber` | Vat, TaxId |
| a bank account number | `BankAccountNumber` | Iban, Account |

The full card number is never stored or passed on: only the `PaymentToken` is.

## Business cases

### BIZ-01 – Business cases follow their template (error)

Some business cases have a template below. Code for such a case must behave as its template does; it may be
written differently.

**The order the warehouse handles orders in:**

```csharp
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
```
