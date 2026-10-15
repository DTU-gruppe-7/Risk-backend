using Microsoft.EntityFrameworkCore;
using Risk.Application.Interfaces.Repository;
using Risk.Domain.Entities;
using Risk.Infrastructure.Persistence;

namespace Risk.Infrastructure.Repositories;

public class TerritoryRepository(AppDbContext context) : Repository<Territory>(context), ITerritoryRepository
{
    public async Task<IEnumerable<Territory>> GetByGameIdAsync(Guid gameId,
        CancellationToken cancellationToken = default)
    {
        return await _dbSet
            .Where(t => EF.Property<Guid>(t, "GameId") == gameId)
            .Include(t => t.Owner)
            .Include(t => t.Continent)
            .ToListAsync(cancellationToken);
    }

    public async Task<IEnumerable<Territory>> GetNeighborsAsync(Guid territoryId,
        CancellationToken cancellationToken = default)
    {
        return await _context.TerritoryBorders
                .Where(tb => tb.TerritoryId == territoryId && tb.BorderingTerritory != null)
                .Select(tb => tb.BorderingTerritory!)
                .ToListAsync(cancellationToken);
    }
}
    