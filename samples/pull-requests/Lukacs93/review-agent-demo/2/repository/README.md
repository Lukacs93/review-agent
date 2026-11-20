# Orders sample

A small order management API with a DDD structure. It is the demo repository of the review-agent project:
the code review agent reviews pull requests here. The team's rules are in [`.review/rules.md`](.review/rules.md).

## Run

```
dotnet test
dotnet run --project src/Orders.Api
```

## Structure

```
src/Orders.Api/
  Domain/          entities and business rules
  Application/     command and query handlers
  Infrastructure/  EF Core, data access
  Controllers/     HTTP endpoints, call handlers only
tests/Orders.Domain.Tests/
```
