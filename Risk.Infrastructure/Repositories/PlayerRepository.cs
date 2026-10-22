using Microsoft.EntityFrameworkCore;
using Risk.Application.Interfaces.Repository;
using Risk.Domain.Entities;
using Risk.Infrastructure.Persistence;

namespace Risk.Infrastructure.Repositories;

public class PlayerRepository(AppDbContext context) : IPlayerRepository
{
    private readonly DbSet<Player> _players = context.Players;

    public Task<Player?> GetAsync(Guid gameId, Guid playerId, CancellationToken cancellationToken = default) =>
        _players.AsNoTracking()
            .FirstOrDefaultAsync(p => p.Id == playerId && p.GameId == gameId, cancellationToken);

    public async Task<IReadOnlyList<Player>> GetPlayersByGameIdAsync(Guid gameId, CancellationToken cancellationToken = default)
    {
        return await _players
            .AsNoTracking()
            .Where(p => p.GameId == gameId)
            .OrderBy(p => p.TurnOrder)
            .ToListAsync(cancellationToken);
    }

    public Task<Player?> GetByGameAndUserIdAsync(Guid gameId, Guid userId, CancellationToken cancellationToken = default) =>
        _players
            .AsNoTracking()
            .Include(p => p.User)
            .Include(p => p.Territories)
            .FirstOrDefaultAsync(p => p.GameId == gameId && p.UserId == userId, cancellationToken);
}
