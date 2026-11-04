using Risk.Domain.Common;
using Risk.Domain.Exceptions;
using Risk.Domain.Map;

namespace Risk.Domain.Entities;

/// <summary>
/// Represents an active or completed Risk game session.
/// Acts as the aggregate root for a single game: settings and players are
/// only changed through this class, so the game rules live in one place.
/// </summary>
public sealed class Game
{
    public Guid Id { get; private set; } = Guid.CreateVersion7();

    /// <summary>
    /// Increases on every change to the aggregate. Used as a concurrency token,
    /// and lets clients ignore pushed updates older than what they already have.
    /// </summary>
    public long Version { get; private set; }

    /// <summary>The user who created and hosts the game.</summary>
    public Guid HostId { get; private set; }

    public GameState State { get; private set; } = GameState.Lobby;

    /// <summary>The player whose turn it currently is.</summary>
    public Guid? CurrentPlayerId { get; private set; }

    public GamePhase CurrentPhase { get; private set; } = GamePhase.Deployment;

    public DateTimeOffset CreatedAt { get; private set; } = DateTimeOffset.UtcNow;
    public DateTimeOffset? StartedAt { get; private set; }
    public DateTimeOffset? TurnStartedAt { get; private set; }
    public DateTimeOffset? EndedAt { get; private set; }
    public bool HasConqueredTerritoryThisTurn { get; private set; }
    public Guid? PendingConquestFromTerritoryId { get; private set; }
    public Guid? PendingConquestToTerritoryId { get; private set; }

    // Navigation properties
    public GameSetting? Settings { get; private set; }
    private readonly List<Player> _players = [];
    public IReadOnlyCollection<Player> Players => _players.AsReadOnly();

    /// <summary>This game's state for each map territory. Empty until the game starts.</summary>
    private readonly List<Territory> _territories = [];
    public IReadOnlyCollection<Territory> Territories => _territories.AsReadOnly();

    // EF Core requires a parameterless constructor
    private Game() { }

    public Game(Guid hostId)
    {
        HostId = Guard.NotEmpty(hostId, nameof(hostId));
        Settings = new GameSetting(Id);
    }

    public int MaxPlayers => Settings?.MaxPlayers ?? GameSetting.DefaultMaxPlayers;

    public bool IsHost(Guid userId) => HostId == userId;

    /// <summary>True for the host and for every user who has joined as a player.</summary>
    public bool HasParticipant(Guid userId) =>
        IsHost(userId) || _players.Any(p => p.UserId == userId);

    public Player? FindPlayer(Guid playerId) => _players.FirstOrDefault(p => p.Id == playerId);

    /// <summary>The board with this game's territory state. Requires <see cref="Territories"/> to be loaded.</summary>
    public GameBoard BoardOn(MapGraph map) => new(map, _territories);

    // -------------------------------------------------------------------------
    // Lobby
    // -------------------------------------------------------------------------

    public void ConfigureSettings(int maxPlayers, int timer, int turnTimeLimit, bool hasFogOfWar, bool hasMissions,
        int blizzardCount = 0)
    {
        EnsureState(GameState.Lobby, "Settings can only be changed while the game is in the Lobby.");
        if (maxPlayers < _players.Count)
            throw new BusinessRuleException("MaxPlayers cannot be lower than the number of players already in the game.");

        if (Settings is null)
            Settings = new GameSetting(Id, maxPlayers, timer, turnTimeLimit, hasFogOfWar, hasMissions, blizzardCount);
        else
            Settings.Update(maxPlayers, timer, turnTimeLimit, hasFogOfWar, hasMissions, blizzardCount);

        BumpVersion();
    }

    public Player AddPlayer(Guid userId, string color)
    {
        EnsureState(GameState.Lobby, "The game has already started.");
        if (_players.Any(p => p.UserId == userId))
            throw new BusinessRuleException("You are already in this game.");
        if (_players.Count >= MaxPlayers)
            throw new BusinessRuleException("The game is full.");

        // Max + 1 instead of Count + 1, so a player leaving never causes duplicate turn orders.
        var turnOrder = _players.Count == 0 ? 1 : _players.Max(p => p.TurnOrder) + 1;
        var player = new Player(Id, userId, color, turnOrder);
        _players.Add(player);
        BumpVersion();
        return player;
    }

    public void RemovePlayer(Guid playerId)
    {
        EnsureState(GameState.Lobby, "Cannot leave a game that has already started.");
        _players.Remove(RequirePlayer(playerId));
        BumpVersion();
    }

    // -------------------------------------------------------------------------
    // Game flow
    // -------------------------------------------------------------------------

   /// <summary>
    /// Starts the game, lays out the board and distributes playable territories between
    /// the players, with <see cref="GameSetting.BlizzardCount"/> of them turned into blizzards.
    /// </summary>
    /// <param name="map">The board.</param>
    /// <param name="random">Decides blizzard placement, territory ownership and initial troop placement.</param>
    public void Start(MapGraph map, Random random)
    {
        EnsureState(GameState.Lobby, "Game can only be started from the Lobby state.");
        if (map.TerritoryIds.Count == 0)
            throw new DomainValidationException("The map has no territories.");
        if (_players.Count is < GameSetting.MinPlayers or > GameSetting.DefaultMaxPlayers)
            throw new BusinessRuleException(
                $"A classic Risk game requires between {GameSetting.MinPlayers} and {GameSetting.DefaultMaxPlayers} players.");

        // 1. Opret alle territorier på kortet og marker blizzards
        var blizzards = BlizzardPlacement.Choose(
            map, Settings?.BlizzardCount ?? 0, minPlayable: _players.Count, random);

        foreach (var id in map.TerritoryIds)
        {
            var territory = new Territory(Id, id);
            if (blizzards.Contains(id))
                territory.MakeBlizzard();
            _territories.Add(territory);
        }

        // 2. Udtræk de spilbare territorier (ikke-blizzards) og bland dem tilfældigt
        var playableTerritories = _territories
            .Where(territory => !territory.IsBlizzard)
            .ToList();

        for (var i = playableTerritories.Count - 1; i > 0; i--)
        {
            var swapIndex = random.Next(i + 1);
            (playableTerritories[i], playableTerritories[swapIndex]) =
                (playableTerritories[swapIndex], playableTerritories[i]);
        }

        // 3. Fordel territorierne skiftevis (Round-Robin) til spillerne
        var players = _players.OrderBy(player => player.TurnOrder).ToList();
        for (var i = 0; players.Count > 0 && i < playableTerritories.Count; i++)
        {
            playableTerritories[i].Claim(players[i % players.Count].Id);
        }

        // 4. Fordel de resterende klassiske starttropper tilfældigt på spillerens territorier.
        var startingTroops = players.Count switch
        {
            2 => 40,
            3 => 35,
            4 => 30,
            5 => 25,
            6 => 20,
            _ => throw new BusinessRuleException("Unsupported number of players for classic Risk."),
        };

        foreach (var player in players)
        {
            var ownedTerritories = playableTerritories
                .Where(t => t.OwnerId == player.Id)
                .ToList();
            var remainingTroops = startingTroops - ownedTerritories.Count;

            for (var troop = 0; troop < remainingTroops; troop++)
            {
                var territory = ownedTerritories[random.Next(ownedTerritories.Count)];
                territory.AddTroops(1);
            }
        }

        // 5. Start første spillers normale tur med reinforcement deployment.
        var firstPlayer = players.First();
        firstPlayer.AddReinforcements(CalculateReinforcements(firstPlayer.Id, map));
        CurrentPlayerId = firstPlayer.Id;
        CurrentPhase = GamePhase.Deployment;
        State = GameState.InProgress;
        StartedAt = DateTimeOffset.UtcNow;
        TurnStartedAt = StartedAt;
        BumpVersion();
    }

    public void End()
    {
        EnsureState(GameState.InProgress, "Game can only be ended when in progress.");
        State = GameState.Finished;
        EndedAt = DateTimeOffset.UtcNow;
        BumpVersion();
    }

    public void EndPhase(Guid playerId, GamePhase expectedNextPhase, MapGraph map, DateTimeOffset now)
    {
        EnsureState(GameState.InProgress, "A phase can only be ended while the game is in progress.");
        if (CurrentPlayerId != playerId)
            throw new BusinessRuleException("It is not this player's turn.");
        if (RequirePlayer(playerId).IsEliminated)
            throw new BusinessRuleException("An eliminated player cannot end a phase.");
        if (expectedNextPhase != NextPhase())
            throw new BusinessRuleException($"The next phase must be {NextPhase()}.");
        if (expectedNextPhase == GamePhase.Attack && RequirePlayer(playerId).ReinforcementTroops > 0)
            throw new BusinessRuleException("All reinforcement troops must be deployed before attacking.");

        if (expectedNextPhase == GamePhase.Attack)
        {
            CurrentPhase = GamePhase.Attack;
        }
        else if (expectedNextPhase == GamePhase.Fortify)
        {
            CurrentPhase = GamePhase.Fortify;
        }
        else
        {
            AdvanceTurn(map, now);
            return;
        }

        BumpVersion();
    }

    public bool AdvanceIfTurnTimedOut(MapGraph map, DateTimeOffset now)
    {
        EnsureState(GameState.InProgress, "A turn can only time out while the game is in progress.");
        var limit = Settings?.TurnTimeLimit ?? 0;
        if (limit == 0 || TurnStartedAt is null || TurnStartedAt.Value.AddSeconds(limit) > now)
            return false;

        AdvanceTurn(map, now);
        return true;
    }

    private GamePhase NextPhase() => CurrentPhase switch
    {
        GamePhase.Deployment => GamePhase.Attack,
        GamePhase.Attack => GamePhase.Fortify,
        GamePhase.Fortify => GamePhase.Deployment,
        _ => throw new BusinessRuleException("The game has an invalid phase.")
    };

    private void AdvanceTurn(MapGraph map, DateTimeOffset now)
    {
        var activePlayers = _players
            .Where(player => !player.IsEliminated)
            .OrderBy(player => player.TurnOrder)
            .ToList();
        if (activePlayers.Count == 0)
            throw new BusinessRuleException("There are no active players left.");

        var current = CurrentPlayerId is null
            ? null
            : activePlayers.FirstOrDefault(player => player.Id == CurrentPlayerId);
        var next = current is null
            ? activePlayers[0]
            : activePlayers.FirstOrDefault(player => player.TurnOrder > current.TurnOrder) ?? activePlayers[0];

        next.AddReinforcements(CalculateReinforcements(next.Id, map));
        HasConqueredTerritoryThisTurn = false;
        CurrentPlayerId = next.Id;
        CurrentPhase = GamePhase.Deployment;
        TurnStartedAt = now;
        BumpVersion();
    }

    private int CalculateReinforcements(Guid playerId, MapGraph map)
    {
        var ownedTerritories = _territories.Count(t => t.OwnerId == playerId);
        var territoryReinforcements = Math.Max(3, ownedTerritories / 3);
        var continentBonus = BoardOn(map).ContinentBonusFor(playerId);
        return territoryReinforcements + continentBonus;
    }

    public bool RecordConquest(Guid playerId)
    {
        EnsureState(GameState.InProgress, "A territory can only be conquered while the game is in progress.");
        if (CurrentPhase != GamePhase.Attack || CurrentPlayerId != playerId)
            throw new BusinessRuleException("A territory can only be conquered during the active player's Attack phase.");
        if (HasConqueredTerritoryThisTurn)
            return false;

        HasConqueredTerritoryThisTurn = true;
        BumpVersion();
        return true;
    }

    public void Fortify(Guid playerId, Territory from, Territory to, int troops, MapGraph map, DateTimeOffset now)
    {
        EnsureState(GameState.InProgress, "Fortification can only happen while the game is in progress.");
        if (CurrentPhase != GamePhase.Fortify)
            throw new BusinessRuleException("Fortification can only happen during the Fortify phase.");
        if (CurrentPlayerId != playerId)
            throw new BusinessRuleException("It is not this player's turn.");
        if (RequirePlayer(playerId).IsEliminated)
            throw new BusinessRuleException("An eliminated player cannot fortify.");
        if (from.GameId != Id || to.GameId != Id)
            throw new BusinessRuleException("Both territories must belong to this game.");
        if (troops <= 0)
            throw new BusinessRuleException("At least one troop must be moved.");

        var board = BoardOn(map);
        if (!board.CanFortify(from.Identifier, to.Identifier, playerId))
            throw new BusinessRuleException("The selected territories cannot be fortified.");
        if (troops >= from.TroopCount)
            throw new BusinessRuleException("The originating territory must retain at least one troop.");

        from.RemoveTroops(troops);
        to.AddTroops(troops);
        AdvanceTurn(map, now);
    }

    public void BeginPendingConquest(Guid playerId, Territory from, Territory to)
    {
        EnsureState(GameState.InProgress, "A conquest can only happen while the game is in progress.");
        if (CurrentPhase != GamePhase.Attack || CurrentPlayerId != playerId)
            throw new BusinessRuleException("A conquest can only happen during the active player's Attack phase.");
        PendingConquestFromTerritoryId = from.Id;
        PendingConquestToTerritoryId = to.Id;
        BumpVersion();
    }

    public void CompletePendingConquest(Guid playerId, Territory from, Territory to, int troops)
    {
        EnsureState(GameState.InProgress, "A conquest can only happen while the game is in progress.");
        if (PendingConquestFromTerritoryId != from.Id || PendingConquestToTerritoryId != to.Id)
            throw new BusinessRuleException("There is no pending conquest for these territories.");
        if (CurrentPlayerId != playerId || CurrentPhase != GamePhase.Attack)
            throw new BusinessRuleException("Only the active player can complete a conquest.");
        if (troops <= 0 || troops >= from.TroopCount)
            throw new BusinessRuleException("The origin territory must retain at least one troop.");

        from.RemoveTroops(troops);
        to.TransferOwnership(playerId, troops);
        PendingConquestFromTerritoryId = null;
        PendingConquestToTerritoryId = null;
        BumpVersion();
    }

    public void DeployTroops(Guid playerId, Territory territory, int troops)
    {
        EnsureState(GameState.InProgress, "Troops can only be deployed while the game is in progress.");
        if (CurrentPhase != GamePhase.Deployment)
            throw new BusinessRuleException("Troops can only be deployed during the Deployment phase.");
        if (CurrentPlayerId != playerId)
            throw new BusinessRuleException("It is not this player's turn.");

        var player = RequirePlayer(playerId);
        if (territory.GameId != Id)
            throw new BusinessRuleException("The territory is not part of this game.");
        if (territory.OwnerId != playerId)
            throw new BusinessRuleException("You can only deploy troops to your own territories.");

        player.PlaceTroops(troops);
        territory.AddTroops(troops);
        if (player.ReinforcementTroops == 0)
            CurrentPhase = GamePhase.Attack;
        BumpVersion();
    }

    public bool EliminatePlayer(Guid playerId)
    {
        EnsureState(GameState.InProgress, "A player can only be eliminated while the game is in progress.");
        var player = RequirePlayer(playerId);
        if (player.IsEliminated)
            return false;
        if (_territories.Any(territory => territory.OwnerId == playerId))
            throw new BusinessRuleException("A player can only be eliminated after losing all territories.");

        player.Eliminate();
        BumpVersion();
        return true;
    }

    // -------------------------------------------------------------------------
    // Helpers
    // -------------------------------------------------------------------------

    private Player RequirePlayer(Guid playerId) =>
        FindPlayer(playerId) ?? throw new BusinessRuleException("The player is not part of this game.");

    private void EnsureState(GameState expected, string message)
    {
        if (State != expected)
            throw new BusinessRuleException(message);
    }

    private void BumpVersion() => Version++;
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
