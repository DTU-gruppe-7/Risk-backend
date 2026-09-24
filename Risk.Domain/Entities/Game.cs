namespace Risk.Domain.Entities;

/// <summary>
/// Represents an active or completed Risk game session.
/// Acts as the aggregate root for a single game.
/// </summary>
public sealed class Game
{
    public Guid Id { get; private set; } = Guid.CreateVersion7();

    /// <summary>The user who created and hosts the game.</summary>
    public Guid HostId { get; private set; }

    public GameState State { get; private set; } = GameState.Lobby;

    /// <summary>The player whose turn it currently is.</summary>
    public Guid? CurrentPlayerId { get; private set; }

    public GamePhase CurrentPhase { get; private set; } = GamePhase.Deployment;

    public DateTimeOffset CreatedAt { get; private set; } = DateTimeOffset.UtcNow;
    public DateTimeOffset? StartedAt { get; private set; }
    public DateTimeOffset? EndedAt { get; private set; }

    // Navigation properties
    public GameSetting? Settings { get; private set; }
    private readonly List<Player> _players = [];
    public IReadOnlyCollection<Player> Players => _players.AsReadOnly();

    private readonly List<Territory> _territories = [];
    public IReadOnlyCollection<Territory> Territories => _territories.AsReadOnly();

    private readonly List<Continent> _continents = [];
    public IReadOnlyCollection<Continent> Continents => _continents.AsReadOnly();

    // EF Core requires a parameterless constructor
    private Game() { }

    public Game(Guid hostId)
    {
        if (hostId == Guid.Empty)
            throw new ArgumentException("HostId must be a valid GUID.", nameof(hostId));

        HostId = hostId;
    }

    public void Start()
    {
        if (State != GameState.Lobby)
            throw new InvalidOperationException("Game can only be started from the Lobby state.");

        State = GameState.InProgress;
        StartedAt = DateTimeOffset.UtcNow;
    }

    public void End()
    {
        if (State != GameState.InProgress)
            throw new InvalidOperationException("Game can only be ended when in progress.");

        State = GameState.Finished;
        EndedAt = DateTimeOffset.UtcNow;
    }

    public void SetCurrentPlayer(Guid playerId)
    {
        CurrentPlayerId = playerId;
    }

    public void SetPhase(GamePhase phase)
    {
        CurrentPhase = phase;
    }
}

public enum GameState
{
    Lobby,
    InProgress,
    Finished
}

public enum GamePhase
{
    Deployment,
    Attack,
    Fortify
}
