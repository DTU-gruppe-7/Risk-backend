using Risk.Domain.Entities;

namespace Risk.Application.Interfaces.Repository;

public interface IGameRepository : IRepository<Game>
{
    Task<Game?> GetByIdWithDetailsAsync(Guid id, CancellationToken cancellationToken = default);
    Task<IEnumerable<Game>> GetGamesByUserIdAsync(Guid userId, CancellationToken cancellationToken = default);
}