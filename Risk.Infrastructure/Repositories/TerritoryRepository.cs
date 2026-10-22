using Microsoft.EntityFrameworkCore;
using Risk.Application.Interfaces.Repository;
using Risk.Domain.Entities;
using Risk.Infrastructure.Persistence;

namespace Risk.Infrastructure.Repositories;

public class TerritoryRepository(AppDbContext context) : Repository<Territory>(context), ITerritoryRepository
{
    public Task<Territory?> GetAsync(Guid gameId, Guid territoryId, CancellationToken cancellationToken = default) =>
        _dbSet.FirstOrDefaultAsync(t => t.Id == territoryId && t.GameId == gameId, cancellationToken);

    public async Task<IReadOnlyList<Territory>> GetByGameIdAsync(Guid gameId, Guid? ownerId = null,
        CancellationToken cancellationToken = default)
    {
        var query = _dbSet.AsNoTracking().Where(t => t.GameId == gameId);
        if (ownerId.HasValue)
            query = query.Where(t => t.OwnerId == ownerId.Value);

        return await query.OrderBy(t => t.Identifier).ToListAsync(cancellationToken);
    }
}
