using Risk.Domain.Entities;

namespace Risk.Application.Interfaces.Repository;

public interface ITerritoryRepository : IRepository<Territory>
{
    Task<Territory?> GetAsync(Guid gameId, Guid territoryId, CancellationToken cancellationToken = default);

    /// <summary>All territories in the game (blizzards included), optionally only those owned by one player.</summary>
    Task<IReadOnlyList<Territory>> GetByGameIdAsync(Guid gameId, Guid? ownerId = null, CancellationToken cancellationToken = default);
}
