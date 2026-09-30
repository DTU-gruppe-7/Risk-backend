using Risk.Domain.Entities;

namespace Risk.Application.Interfaces.Repository;

public interface IPlayerRepository : IRepository<Player>
{
    Task<IEnumerable<Player>> GetPlayersByGameIdAsync(Guid gameId, CancellationToken cancellationToken = default);
    Task<Player?> GetByGameAndUserIdAsync(Guid gameId, Guid userId, CancellationToken cancellationToken = default);
}