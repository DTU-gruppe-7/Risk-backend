using Microsoft.EntityFrameworkCore;
using Risk.Application.Interfaces.Repository;
using Risk.Domain.Entities;
using Risk.Infrastructure.Persistence;

namespace Risk.Infrastructure.Repositories;

public class BonusCardRepository(AppDbContext context) : Repository<BonusCard>(context), IBonusCardRepository
{
    public Task<BonusCard?> GetAsync(Guid gameId, Guid cardId, CancellationToken cancellationToken = default) =>
        _dbSet.FirstOrDefaultAsync(b => b.Id == cardId && b.GameId == gameId, cancellationToken);

    public Task<BonusCard?> GetNextUnassignedAsync(Guid gameId, CancellationToken cancellationToken = default) =>
        _dbSet
            .Where(b => b.GameId == gameId && b.PlayerId == null && !b.Used)
            .OrderBy(b => b.Id)
            .FirstOrDefaultAsync(cancellationToken);

    public async Task<IReadOnlyList<BonusCard>> GetByPlayerIdAsync(
        Guid gameId, Guid playerId, CancellationToken cancellationToken = default) =>
        await _dbSet
            .Where(b => b.GameId == gameId && b.PlayerId == playerId && !b.Used)
            .ToListAsync(cancellationToken);

    public async Task<IReadOnlyList<BonusCard>> GetByGameIdAsync(Guid gameId, Guid? playerId = null,
        CancellationToken cancellationToken = default)
    {
        var query = _dbSet.AsNoTracking().Where(b => b.GameId == gameId);
        if (playerId.HasValue)
            query = query.Where(b => b.PlayerId == playerId.Value);

        return await query.OrderBy(b => b.Type).ToListAsync(cancellationToken);
    }
}

public class MissionCardRepository(AppDbContext context) : Repository<MissionCard>(context), IMissionCardRepository
{
    public Task<MissionCard?> GetAsync(Guid gameId, Guid cardId, CancellationToken cancellationToken = default) =>
        _dbSet.FirstOrDefaultAsync(m => m.Id == cardId && m.GameId == gameId, cancellationToken);

    public Task<MissionCard?> GetByPlayerIdAsync(
        Guid gameId, Guid playerId, CancellationToken cancellationToken = default) =>
        _dbSet.FirstOrDefaultAsync(
            m => m.GameId == gameId && m.PlayerId == playerId && !m.IsCompleted,
            cancellationToken);

    public async Task<IReadOnlyList<MissionCard>> GetByGameIdAsync(Guid gameId, Guid? playerId = null,
        CancellationToken cancellationToken = default)
    {
        var query = _dbSet.AsNoTracking().Where(m => m.GameId == gameId);
        if (playerId.HasValue)
            query = query.Where(m => m.PlayerId == playerId.Value);

        return await query.OrderBy(m => m.Title).ToListAsync(cancellationToken);
    }
}
