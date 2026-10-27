using Risk.Domain.Entities;

namespace Risk.Application.Interfaces.Repository;

public interface IGameRepository : IRepository<Game>
{
    /// <summary>The game with settings and players: everything the aggregate's rules need.</summary>
    Task<Game?> GetAsync(Guid id, CancellationToken cancellationToken = default);

    Task<Game?> GetByIdWithDetailsAsync(Guid id, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<Game>> ListAsync(GameState? state, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<Game>> GetGamesByUserIdAsync(Guid userId, CancellationToken cancellationToken = default);
    Task<bool> ExistsAsync(Guid id, CancellationToken cancellationToken = default);
}
