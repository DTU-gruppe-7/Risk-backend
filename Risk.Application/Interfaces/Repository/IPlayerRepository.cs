using Risk.Domain.Entities;

namespace Risk.Application.Interfaces.Repository;

/// <summary>
/// Read access to players. Players belong to the <see cref="Game"/> aggregate,
/// so they are added, removed and changed through <see cref="IGameRepository"/>.
/// </summary>
public interface IPlayerRepository
{
    Task<Player?> GetAsync(Guid gameId, Guid playerId, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<Player>> GetPlayersByGameIdAsync(Guid gameId, CancellationToken cancellationToken = default);
    Task<Player?> GetByGameAndUserIdAsync(Guid gameId, Guid userId, CancellationToken cancellationToken = default);
}
