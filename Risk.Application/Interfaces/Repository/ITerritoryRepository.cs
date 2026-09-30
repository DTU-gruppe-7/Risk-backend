using Risk.Domain.Entities;

namespace Risk.Application.Interfaces.Repository;

public interface ITerritoryRepository : IRepository<Territory>
{
    Task<IEnumerable<Territory>> GetByGameIdAsync(Guid gameId, CancellationToken cancellationToken = default);
    Task<IEnumerable<Territory>> GetNeighborsAsync(Guid territoryId, CancellationToken cancellationToken = default);
}