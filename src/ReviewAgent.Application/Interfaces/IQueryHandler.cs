namespace ReviewAgent.Application.Interfaces;

/// <summary>Answers one query: a use case that only reads.</summary>
public interface IQueryHandler<in TQuery, TResult>
{
    Task<TResult> HandleAsync(TQuery query, CancellationToken cancellationToken);
}
