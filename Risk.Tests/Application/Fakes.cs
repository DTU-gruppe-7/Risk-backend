using Risk.Application.Interfaces;
using Risk.Application.Interfaces.Repository;
using Risk.Domain.Entities;
using Risk.Domain.Map;

namespace Risk.Tests.Application;

// In-memory stand-ins for the Infrastructure ports. Because the services only
// depend on these interfaces, the use cases can be tested without a database.

internal sealed class FakeCurrentUser(Guid userId) : ICurrentUser
{
    public Guid UserId { get; set; } = userId;
}

internal sealed class FakeUnitOfWork : IUnitOfWork
{
    public int SaveCount { get; private set; }

    public Task SaveChangesAsync(CancellationToken cancellationToken = default)
    {
        SaveCount++;
        return Task.CompletedTask;
    }

    public Task<IUnitOfWorkTransaction> BeginTransactionAsync(CancellationToken cancellationToken = default) =>
        Task.FromResult<IUnitOfWorkTransaction>(new FakeTransaction());

    private sealed class FakeTransaction : IUnitOfWorkTransaction
    {
        public Task CommitAsync(CancellationToken cancellationToken = default) => Task.CompletedTask;
        public ValueTask DisposeAsync() => ValueTask.CompletedTask;
    }
}

internal sealed class FakeGameRepository : IGameRepository
{
    public List<Game> Games { get; } = [];

    public Task<Game?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default) => GetAsync(id, cancellationToken);
    public Task<Game?> GetAsync(Guid id, CancellationToken cancellationToken = default) =>
        Task.FromResult(Games.FirstOrDefault(g => g.Id == id));
    public Task<Game?> GetByIdWithDetailsAsync(Guid id, CancellationToken cancellationToken = default) => GetAsync(id, cancellationToken);

    public Task<IReadOnlyList<Game>> ListAsync(GameState? state, CancellationToken cancellationToken = default) =>
        Task.FromResult<IReadOnlyList<Game>>(Games.Where(g => state is null || g.State == state).ToList());

    public Task<IReadOnlyList<Game>> GetGamesByUserIdAsync(Guid userId, CancellationToken cancellationToken = default) =>
        Task.FromResult<IReadOnlyList<Game>>(Games.Where(g => g.HasParticipant(userId)).ToList());

    public Task<IReadOnlyList<Game>> GetInProgressGamesAsync(CancellationToken cancellationToken = default) =>
        Task.FromResult<IReadOnlyList<Game>>(Games.Where(g => g.State == GameState.InProgress).ToList());

    public Task<bool> ExistsAsync(Guid id, CancellationToken cancellationToken = default) =>
        Task.FromResult(Games.Any(g => g.Id == id));

    public void Add(Game entity) => Games.Add(entity);
    public void Remove(Game entity) => Games.Remove(entity);
}

internal sealed class FakeMapRepository : IMapRepository
{
    public Task<IReadOnlyList<MapContinent>> GetContinentsAsync(CancellationToken cancellationToken = default) =>
        Task.FromResult(ClassicMap.Continents);

    public Task<IReadOnlyList<MapTerritory>> GetTerritoriesAsync(CancellationToken cancellationToken = default) =>
        Task.FromResult(ClassicMap.Territories);

    public Task<MapGraph> GetGraphAsync(CancellationToken cancellationToken = default) =>
        Task.FromResult(ClassicMap.Graph);
}

internal sealed class FakePlayerRepository(FakeGameRepository games) : IPlayerRepository
{
    private IEnumerable<Player> All => games.Games.SelectMany(g => g.Players);

    public Task<Player?> GetAsync(Guid gameId, Guid playerId, CancellationToken cancellationToken = default) =>
        Task.FromResult(All.FirstOrDefault(p => p.GameId == gameId && p.Id == playerId));

    public Task<IReadOnlyList<Player>> GetPlayersByGameIdAsync(Guid gameId, CancellationToken cancellationToken = default) =>
        Task.FromResult<IReadOnlyList<Player>>(All.Where(p => p.GameId == gameId).OrderBy(p => p.TurnOrder).ToList());

    public Task<Player?> GetByGameAndUserIdAsync(Guid gameId, Guid userId, CancellationToken cancellationToken = default) =>
        Task.FromResult(All.FirstOrDefault(p => p.GameId == gameId && p.UserId == userId));
}

internal sealed class FakeBonusCardRepository : IBonusCardRepository
{
    private readonly List<BonusCard> _cards = [];

    public Task<BonusCard?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default) =>
        Task.FromResult(_cards.FirstOrDefault(card => card.Id == id));

    public Task<BonusCard?> GetAsync(Guid gameId, Guid cardId, CancellationToken cancellationToken = default) =>
        Task.FromResult(_cards.FirstOrDefault(card => card.GameId == gameId && card.Id == cardId));

    public Task<BonusCard?> GetNextUnassignedAsync(Guid gameId, CancellationToken cancellationToken = default) =>
        Task.FromResult(_cards.FirstOrDefault(card => card.GameId == gameId && card.PlayerId is null && !card.Used));

    public Task<IReadOnlyList<BonusCard>> GetByPlayerIdAsync(
        Guid gameId, Guid playerId, CancellationToken cancellationToken = default) =>
        Task.FromResult<IReadOnlyList<BonusCard>>(
            _cards.Where(card => card.GameId == gameId && card.PlayerId == playerId && !card.Used).ToList());

    public Task<IReadOnlyList<BonusCard>> GetByGameIdAsync(
        Guid gameId, Guid? playerId = null, CancellationToken cancellationToken = default) =>
        Task.FromResult<IReadOnlyList<BonusCard>>(
            _cards.Where(card => card.GameId == gameId && (playerId is null || card.PlayerId == playerId)).ToList());

    public void Add(BonusCard entity) => _cards.Add(entity);
    public void Remove(BonusCard entity) => _cards.Remove(entity);
}

internal sealed class FakeMissionCardRepository : IMissionCardRepository
{
    private readonly List<MissionCard> _cards = [];

    public Task<MissionCard?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default) =>
        Task.FromResult(_cards.FirstOrDefault(card => card.Id == id));

    public Task<MissionCard?> GetAsync(Guid gameId, Guid cardId, CancellationToken cancellationToken = default) =>
        Task.FromResult(_cards.FirstOrDefault(card => card.GameId == gameId && card.Id == cardId));

    public Task<MissionCard?> GetByPlayerIdAsync(
        Guid gameId, Guid playerId, CancellationToken cancellationToken = default) =>
        Task.FromResult(_cards.FirstOrDefault(card => card.GameId == gameId && card.PlayerId == playerId));

    public Task<IReadOnlyList<MissionCard>> GetByGameIdAsync(
        Guid gameId, Guid? playerId = null, CancellationToken cancellationToken = default) =>
        Task.FromResult<IReadOnlyList<MissionCard>>(
            _cards.Where(card => card.GameId == gameId && (playerId is null || card.PlayerId == playerId)).ToList());

    public void Add(MissionCard entity) => _cards.Add(entity);
    public void Remove(MissionCard entity) => _cards.Remove(entity);
}
