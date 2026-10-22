using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;
using Risk.Application.Exceptions;
using Risk.Application.Interfaces;

namespace Risk.Infrastructure.Persistence;

public sealed class UnitOfWork(AppDbContext context) : IUnitOfWork
{
    public async Task SaveChangesAsync(CancellationToken cancellationToken = default)
    {
        try
        {
            await context.SaveChangesAsync(cancellationToken);
        }
        catch (DbUpdateConcurrencyException ex)
        {
            // Translated here, so Application and the API never depend on EF Core types.
            throw new ConcurrencyConflictException(ex);
        }
    }

    public async Task<IUnitOfWorkTransaction> BeginTransactionAsync(CancellationToken cancellationToken = default) =>
        new Transaction(await context.Database.BeginTransactionAsync(cancellationToken));

    private sealed class Transaction(IDbContextTransaction inner) : IUnitOfWorkTransaction
    {
        public Task CommitAsync(CancellationToken cancellationToken = default) =>
            inner.CommitAsync(cancellationToken);

        // Disposing an uncommitted EF transaction rolls it back.
        public ValueTask DisposeAsync() => inner.DisposeAsync();
    }
}
