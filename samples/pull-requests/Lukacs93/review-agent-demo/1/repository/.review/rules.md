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
