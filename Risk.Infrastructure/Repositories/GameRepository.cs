using Microsoft.EntityFrameworkCore;
using Risk.Application.Interfaces.Repository;
using Risk.Domain.Entities;
using Risk.Infrastructure.Persistence;

namespace Risk.Infrastructure.Repositories;

public class GameRepository(AppDbContext context) : Repository<Game>(context), IGameRepository
{
    private IQueryable<Game> WithSettingsAndPlayers =>
        _dbSet.Include(g => g.Settings).Include(g => g.Players);

    public override Task<Game?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default) =>
        GetAsync(id, cancellationToken);

    public Task<Game?> GetAsync(Guid id, CancellationToken cancellationToken = default) =>
        WithSettingsAndPlayers.FirstOrDefaultAsync(g => g.Id == id, cancellationToken);

    public async Task<Game?> GetByIdWithDetailsAsync(Guid id, CancellationToken cancellationToken = default)
    {
        return await _dbSet
            .Include(g => g.Settings)
            .Include(g => g.Players)
            .ThenInclude(p => p.User)
            .Include(g => g.Territories)
            .ThenInclude(t => t.Owner)
            .AsSplitQuery()
            .FirstOrDefaultAsync(g => g.Id == id, cancellationToken);
    }

    public async Task<IReadOnlyList<Game>> ListAsync(GameState? state, CancellationToken cancellationToken = default)
    {
        var query = WithSettingsAndPlayers.AsNoTracking();
        if (state.HasValue)
            query = query.Where(g => g.State == state.Value);

        return await query.OrderByDescending(g => g.CreatedAt).ToListAsync(cancellationToken);
    }

    public async Task<IReadOnlyList<Game>> GetGamesByUserIdAsync(Guid userId, CancellationToken cancellationToken = default)
    {
        return await WithSettingsAndPlayers
            .AsNoTracking()
            .Where(g => g.Players.Any(p => p.UserId == userId) || g.HostId == userId)
            .ToListAsync(cancellationToken);
    }

    public Task<bool> ExistsAsync(Guid id, CancellationToken cancellationToken = default) =>
        _dbSet.AnyAsync(g => g.Id == id, cancellationToken);
}
