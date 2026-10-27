using Risk.Domain.Common;
using Risk.Domain.Exceptions;
using Risk.Domain.Map;

namespace Risk.Domain.Entities;

/// <summary>
/// One territory's state in one game: owner, troops and whether it is a blizzard.
/// What the territory is (continent, neighbours) comes from the static map,
/// see <see cref="MapTerritory"/>, referenced by <see cref="Identifier"/>.
/// Rules that involve several territories live in <see cref="GameBoard"/>.
/// </summary>
public sealed class Territory
{
    public Guid Id { get; private set; } = Guid.CreateVersion7();

    public Guid GameId { get; private set; }

    /// <summary>The map territory this row is the state of (e.g. "north-africa").</summary>
    public string Identifier { get; private set; } = string.Empty;

    /// <summary>
    /// The player who currently controls this territory.
    /// Null means the territory is unoccupied (initial setup phase).
    /// </summary>
    public Guid? OwnerId { get; private set; }

    /// <summary>Number of troops currently stationed in the territory.</summary>
    public int TroopCount { get; private set; } = 0;

    /// <summary>
    /// Blizzard game mode: the territory is impassable for the whole game. It cannot be
    /// owned, reinforced, attacked or moved through, and does not count towards continent
    /// bonuses. Decided when the game starts and never changes afterwards.
    /// </summary>
    public bool IsBlizzard { get; private set; }

    // Navigation properties
    public Game? Game { get; private set; }
    public Player? Owner { get; private set; }

    // EF Core requires a parameterless constructor
    private Territory() { }

    public Territory(Guid gameId, string identifier)
    {
        GameId = Guard.NotEmpty(gameId, nameof(gameId));
        Identifier = Guard.NotBlank(identifier, nameof(identifier)).ToLowerInvariant();
    }

    public void Claim(Guid playerId, int initialTroops = 1)
    {
        Guard.NotEmpty(playerId, nameof(playerId));
        Guard.Positive(initialTroops, nameof(initialTroops));
        EnsureNotBlizzard();
        if (OwnerId.HasValue)
            throw new BusinessRuleException("The territory is already occupied.");

        OwnerId = playerId;
        TroopCount = initialTroops;
    }

    /// <summary>Adds troops for a positive delta and removes them for a negative one.</summary>
    public void AdjustTroops(int delta)
    {
        if (delta > 0) AddTroops(delta);
        else if (delta < 0) RemoveTroops(-delta);
    }

    public void AddTroops(int count)
    {
        Guard.Positive(count, nameof(count));
        EnsureNotBlizzard();
        TroopCount += count;
    }

    public void RemoveTroops(int count)
    {
        Guard.Positive(count, nameof(count));
        if (count > TroopCount)
            throw new BusinessRuleException("Cannot remove more troops than are stationed in the territory.");

        TroopCount -= count;
    }

    public void TransferOwnership(Guid newOwnerId, int remainingTroops)
    {
        EnsureNotBlizzard();
        OwnerId = Guard.NotEmpty(newOwnerId, nameof(newOwnerId));
        TroopCount = Guard.Positive(remainingTroops, nameof(remainingTroops));
    }

    /// <summary>Only <see cref="Game.Start"/> places blizzards, before anyone owns anything.</summary>
    internal void MakeBlizzard()
    {
        if (OwnerId.HasValue)
            throw new BusinessRuleException("An occupied territory cannot become a blizzard.");

        IsBlizzard = true;
    }

    private void EnsureNotBlizzard()
    {
        if (IsBlizzard)
            throw new BusinessRuleException("The territory is covered by a blizzard.");
    }
}
