namespace Risk.Application.Interfaces;

/// <summary>
/// Commits the changes made through the repositories in the current request.
/// Implemented in Infrastructure, so Application never sees the DbContext.
/// </summary>
public interface IUnitOfWork
{
    /// <exception cref="Exceptions.ConcurrencyConflictException">Someone else changed the data first.</exception>
    Task SaveChangesAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Starts a transaction. Everything saved before <see cref="IUnitOfWorkTransaction.CommitAsync"/>
    /// is rolled back if the transaction is disposed without being committed.
    /// </summary>
    Task<IUnitOfWorkTransaction> BeginTransactionAsync(CancellationToken cancellationToken = default);
}

public interface IUnitOfWorkTransaction : IAsyncDisposable
{
    Task CommitAsync(CancellationToken cancellationToken = default);
}
