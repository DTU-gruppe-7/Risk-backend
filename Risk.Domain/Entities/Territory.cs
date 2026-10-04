namespace Risk.Domain.Entities;

/// <summary>
/// A named region on the Risk board that can be owned by a player
/// and occupied by troops.
/// </summary>
public sealed class Territory
{
    public Guid Id { get; private set; } = Guid.CreateVersion7();

    public Guid ContinentId { get; private set; }

    /// <summary>
    /// The player who currently controls this territory.
    /// Null means the territory is unoccupied (initial setup phase).
    /// </summary>
    public Guid? OwnerId { get; private set; }

    /// <summary>Number of troops currently stationed in the territory.</summary>
    public int TroopCount { get; private set; } = 0;

    /// <summary>
    /// Stable, human-readable identifier for the territory (e.g. "north-africa").
    /// Used to reference the territory independently of its GUID.
    /// </summary>
    public string Identifier { get; private set; } = string.Empty;

    /// <summary>Whether the territory is active/visible in the current game.</summary>
    public bool Active { get; private set; } = true;

    // Navigation properties
    public Continent? Continent { get; private set; }
    public Player? Owner { get; private set; }

    private readonly List<Territory> _neighbors = [];
    /// <summary>Territories that share a border with this one.</summary>
    public IReadOnlyCollection<Territory> Neighbors => _neighbors.AsReadOnly();

    // EF Core requires a parameterless constructor
    private Territory() { }

    public Territory(Guid continentId, string identifier)
    {
        if (continentId == Guid.Empty)
            throw new ArgumentException("ContinentId must be a valid GUID.", nameof(continentId));
        if (string.IsNullOrWhiteSpace(identifier))
            throw new ArgumentException("Identifier must not be empty.", nameof(identifier));

        ContinentId = continentId;
        Identifier = identifier.Trim().ToLowerInvariant();
    }

    public void Claim(Guid playerId, int initialTroops = 1)
    {
        if (playerId == Guid.Empty)
            throw new ArgumentException("PlayerId must be a valid GUID.", nameof(playerId));
        if (initialTroops < 1)
            throw new ArgumentOutOfRangeException(nameof(initialTroops), "Must place at least 1 troop.");

        OwnerId = playerId;
        TroopCount = initialTroops;
    }

    public void AddTroops(int count)
    {
        if (count <= 0)
            throw new ArgumentOutOfRangeException(nameof(count), "Count must be positive.");

        TroopCount += count;
    }

    public void RemoveTroops(int count)
    {
        if (count <= 0 || count > TroopCount)
            throw new InvalidOperationException("Invalid troop removal amount.");

        TroopCount -= count;
    }

    public void TransferOwnership(Guid newOwnerId, int remainingTroops)
    {
        if (newOwnerId == Guid.Empty)
            throw new ArgumentException("NewOwnerId must be a valid GUID.", nameof(newOwnerId));
        if (remainingTroops < 1)
            throw new ArgumentOutOfRangeException(nameof(remainingTroops), "Must have at least 1 troop after transfer.");

        OwnerId = newOwnerId;
        TroopCount = remainingTroops;
    }

    public void Deactivate() => Active = false;
    
    public Guid GameId { get; private set; }
    public Game? Game { get; private set; }
}
