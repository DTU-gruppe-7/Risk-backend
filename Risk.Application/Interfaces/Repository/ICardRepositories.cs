using Risk.Domain.Entities;

namespace Risk.Application.Interfaces.Repository;

public interface IBonusCardRepository : IRepository<BonusCard>
{
    Task<BonusCard?> GetAsync(Guid gameId, Guid cardId, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<BonusCard>> GetByGameIdAsync(Guid gameId, Guid? playerId = null, CancellationToken cancellationToken = default);
}

public interface IMissionCardRepository : IRepository<MissionCard>
{
    Task<MissionCard?> GetAsync(Guid gameId, Guid cardId, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<MissionCard>> GetByGameIdAsync(Guid gameId, Guid? playerId = null, CancellationToken cancellationToken = default);
}
