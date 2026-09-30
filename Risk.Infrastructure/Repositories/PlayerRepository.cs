using Microsoft.EntityFrameworkCore;
using Risk.Application.Interfaces.Repository;
using Risk.Domain.Entities;
using Risk.Infrastructure.Persistence;

namespace Risk.Infrastructure.Repositories;

public class PlayerRepository(AppDbContext context) : Repository<Player>(context), IPlayerRepository
{
    public async Task<IEnumerable<Player>> GetPlayersByGameIdAsync(Guid gameId, CancellationToken cancellationToken = default)
    {
        return await _dbSet
            .Where(p => p.GameId == gameId)
            .Include(p => p.User)
            .Include(p => p.Territories)
            .ToListAsync(cancellationToken);
    }

    public async Task<Player?> GetByGameAndUserIdAsync(Guid gameId, Guid userId, CancellationToken cancellationToken = default)
    {
        return await _dbSet
            .Include(p => p.User)
            .Include(p => p.Territories)
            .FirstOrDefaultAsync(p => p.GameId == gameId && p.UserId == userId, cancellationToken);
    }
}