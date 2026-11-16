namespace ReviewAgent.Application.Interfaces;

/// <summary>Carries out one command: a use case that changes state.</summary>
public interface ICommandHandler<in TCommand>
{
    Task HandleAsync(TCommand command, CancellationToken cancellationToken);
}

/// <summary>Carries out one command and returns a result, such as the ID of what it created.</summary>
public interface ICommandHandler<in TCommand, TResult>
{
    Task<TResult> HandleAsync(TCommand command, CancellationToken cancellationToken);
}
