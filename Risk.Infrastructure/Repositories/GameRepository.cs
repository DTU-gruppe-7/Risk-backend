using Microsoft.EntityFrameworkCore;
using Risk.Application.Interfaces.Repository;
using Risk.Domain.Entities;
using Risk.Infrastructure.Persistence;

namespace Risk.Infrastructure.Repositories;

public class GameRepository(AppDbContext context) : Repository<Game>(context), IGameRepository
{
    public async Task<Game?> GetByIdWithDetailsAsync(Guid id, CancellationToken cancellationToken = default)
    {
        return await _dbSet
            .Include(g => g.Players)
            .ThenInclude(p => p.User)
            .Include(g => g.Continents)
            .ThenInclude(c => c.Territories)
            .Include(g => g.Territories)
            .ThenInclude(t => t.Owner)
            .FirstOrDefaultAsync(g => g.Id == id, cancellationToken);
    }

    public async Task<IEnumerable<Game>> GetGamesByUserIdAsync(Guid userId, CancellationToken cancellationToken = default)
    {
        return await _dbSet
            .Include(g => g.Players)
            .Where(g => g.Players.Any(p => p.UserId == userId) || g.HostId == userId)
            .ToListAsync(cancellationToken);
    }
}