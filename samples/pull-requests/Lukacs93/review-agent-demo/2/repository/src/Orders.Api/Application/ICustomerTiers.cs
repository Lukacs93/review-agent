namespace Orders.Api.Application;

/// <summary>Tells which customers are priority customers.</summary>
public interface ICustomerTiers
{
    bool IsPriority(Guid customerId);
}
