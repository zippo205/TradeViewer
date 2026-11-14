namespace TradeViewer.Application.Abstractions.Messaging;

/// <summary>A request that changes state. Every write in the system is a command.</summary>
public interface ICommand<TResult>
{
}

public interface ICommandHandler<in TCommand, TResult>
    where TCommand : ICommand<TResult>
{
    Task<Result<TResult>> HandleAsync(TCommand command, CancellationToken cancellationToken);
}
