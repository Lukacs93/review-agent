using Orders.Api.Application;

namespace Orders.Api.Infrastructure;

/// <summary>The priority customers, from the <c>Customers:PriorityCustomerIds</c> setting.</summary>
public sealed class CustomerTiers(IConfiguration configuration) : ICustomerTiers
{
    private readonly HashSet<Guid> _priorityCustomerIds =
        configuration.GetSection("Customers:PriorityCustomerIds").Get<HashSet<Guid>>() ?? [];

    public bool IsPriority(Guid customerId) => _priorityCustomerIds.Contains(customerId);
}
