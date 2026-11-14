namespace TradeViewer.Application.Abstractions.Messaging;

/// <summary>A request that reads state and never changes it.</summary>
public interface IQuery<TResult>
{
}

public interface IQueryHandler<in TQuery, TResult>
    where TQuery : IQuery<TResult>
{
    Task<Result<TResult>> HandleAsync(TQuery query, CancellationToken cancellationToken);
}
