using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;
using Microsoft.Extensions.Logging;
using Risk.Application.Exceptions;
using Risk.Application.Games;
using Risk.Application.Interfaces;
using Risk.Domain.Entities;

namespace Risk.Infrastructure.Persistence;

/// <summary>
/// Commits changes and then tells the clients watching a game about it. Changed games are read from
/// the change tracker, so every use case (and the turn timeout worker) pushes updates without
/// having to remember to. Bonus cards that got a new owner are read the same way, so the player who
/// got them is told (<see cref="CardHandover"/>).
/// </summary>
public sealed class UnitOfWork(
    AppDbContext context,
    GameUpdatePublisher publisher,
    ILogger<UnitOfWork> logger) : IUnitOfWork
{
    private readonly HashSet<Guid> _changedGameIds = [];
    private readonly List<CardHandover> _cardHandovers = [];
    private bool _inTransaction;

    public async Task SaveChangesAsync(CancellationToken cancellationToken = default)
    {
        // Must be read before saving: afterwards every entry is Unchanged.
        var changed = ChangedGameIds();
        var handovers = CardHandovers();
        try
        {
            await context.SaveChangesAsync(cancellationToken);
        }
        catch (DbUpdateConcurrencyException ex)
        {
            // Translated here, so Application and the API never depend on EF Core types.
            throw new ConcurrencyConflictException(ex);
        }

        _changedGameIds.UnionWith(changed);
        _cardHandovers.AddRange(handovers);
        // Inside a transaction nothing is final until it commits.
        if (!_inTransaction)
            await PublishAsync(cancellationToken);
    }

    public async Task<IUnitOfWorkTransaction> BeginTransactionAsync(CancellationToken cancellationToken = default)
    {
        var transaction = await context.Database.BeginTransactionAsync(cancellationToken);
        _inTransaction = true;
        return new Transaction(this, transaction);
    }

    private List<Guid> ChangedGameIds() =>
        context.ChangeTracker.Entries()
            .Where(entry => entry.State is EntityState.Added or EntityState.Modified or EntityState.Deleted)
            .Select(entry => entry.Entity switch
            {
                Game game => game.Id,
                Player player => player.GameId,
                Territory territory => territory.GameId,
                GameSetting settings => settings.GameId,
                BonusCard card => card.GameId,
                MissionCard card => card.GameId,
                _ => Guid.Empty,
            })
            .Where(id => id != Guid.Empty)
            .ToList();

    /// <summary>Bonus cards about to get a new owner, with who held them before (null: the draw pile).</summary>
    private List<CardHandover> CardHandovers() =>
        context.ChangeTracker.Entries<BonusCard>()
            .Where(entry => entry.State == EntityState.Modified && entry.Property(card => card.PlayerId).IsModified)
            .Select(entry => new CardHandover(entry.Entity, entry.Property(card => card.PlayerId).OriginalValue))
            .Where(handover => handover.Card.PlayerId is not null && handover.Card.PlayerId != handover.PreviousPlayerId)
            .ToList();

    private async Task PublishAsync(CancellationToken cancellationToken)
    {
        if (_changedGameIds.Count == 0)
            return;

        var gameIds = _changedGameIds.ToList();
        var handovers = _cardHandovers.ToList();
        _changedGameIds.Clear();
        _cardHandovers.Clear();
        try
        {
            await publisher.PublishAsync(gameIds, handovers, cancellationToken);
        }
        catch (Exception exception) when (exception is not OperationCanceledException)
        {
            // The change is already saved. A missed push must not turn into a failed request:
            // clients still get the state on their next fetch or rejoin.
            logger.LogError(exception, "Failed to push updates for games {GameIds}.", gameIds);
        }
    }

    private sealed class Transaction(UnitOfWork owner, IDbContextTransaction inner) : IUnitOfWorkTransaction
    {
        public async Task CommitAsync(CancellationToken cancellationToken = default)
        {
            await inner.CommitAsync(cancellationToken);
            owner._inTransaction = false;
            await owner.PublishAsync(cancellationToken);
        }

        // Disposing an uncommitted EF transaction rolls it back, so nothing is pushed.
        public async ValueTask DisposeAsync()
        {
            if (owner._inTransaction)
            {
                owner._inTransaction = false;
                owner._changedGameIds.Clear();
                owner._cardHandovers.Clear();
            }
            await inner.DisposeAsync();
        }
    }
}
