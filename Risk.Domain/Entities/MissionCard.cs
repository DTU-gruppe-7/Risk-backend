using Risk.Domain.Common;
using Risk.Domain.Exceptions;
using Risk.Domain.Map;

namespace Risk.Domain.Entities;

/// <summary>
/// A secret mission card assigned to a player at the start of the game.
/// When <see cref="GameSetting.HasMissions"/> is true, a player wins by
/// completing their mission rather than conquering the whole board.
/// </summary>
public sealed class MissionCard
{
    public Guid Id { get; private set; } = Guid.CreateVersion7();

    public Guid GameId { get; private set; }

    /// <summary>The player this mission is assigned to.</summary>
    public Guid? PlayerId { get; private set; }

    /// <summary>Short title of the mission (e.g. "Conquer Europe and Australia").</summary>
    public string Title { get; private set; } = string.Empty;

    /// <summary>Full description of the victory condition.</summary>
    public string Description { get; private set; } = string.Empty;

    /// <summary>Whether the assigned player has completed this mission.</summary>
    public bool IsCompleted { get; private set; } = false;

    public MissionObjectiveType ObjectiveType { get; private set; }
    public string? TargetIdentifier { get; private set; }
    public Guid? TargetPlayerId { get; private set; }

    // Navigation properties
    public Game? Game { get; private set; }
    public Player? Player { get; private set; }

    // EF Core requires a parameterless constructor
    private MissionCard() { }

    public MissionCard(
        Guid gameId,
        string title,
        string description,
        MissionObjectiveType objectiveType = MissionObjectiveType.ControlAllTerritories,
        string? targetIdentifier = null,
        Guid? targetPlayerId = null)
    {
        GameId = Guard.NotEmpty(gameId, nameof(gameId));
        Title = Guard.NotBlank(title, nameof(title));
        Description = Guard.NotBlank(description, nameof(description));
        ObjectiveType = objectiveType;
        TargetIdentifier = targetIdentifier?.Trim().ToLowerInvariant();
        TargetPlayerId = targetPlayerId;
    }

    public void AssignToPlayer(Guid playerId)
    {
        Guard.NotEmpty(playerId, nameof(playerId));
        if (PlayerId.HasValue)
            throw new BusinessRuleException("Mission card is already assigned.");

        PlayerId = playerId;
    }

    public void Complete(Game game, MapGraph map)
    {
        if (!PlayerId.HasValue)
            throw new BusinessRuleException("Cannot complete an unassigned mission.");
        if (IsCompleted)
            throw new BusinessRuleException("Mission is already completed.");
        if (!IsSatisfiedBy(game, map, PlayerId.Value))
            throw new BusinessRuleException("The mission objective has not been completed.");

        IsCompleted = true;
    }

    public bool IsSatisfiedBy(Game game, MapGraph map, Guid playerId) =>
        ObjectiveType switch
        {
            MissionObjectiveType.ControlAllTerritories => game.ControlsAllTerritories(playerId),
            MissionObjectiveType.ControlContinent =>
                !string.IsNullOrWhiteSpace(TargetIdentifier) &&
                game.Territories
                    .Where(territory => !territory.IsBlizzard &&
                        map.ContinentOf(territory.Identifier) == TargetIdentifier)
                    .All(territory => territory.OwnerId == playerId),
            MissionObjectiveType.EliminatePlayer =>
                TargetPlayerId.HasValue &&
                game.FindPlayer(TargetPlayerId.Value)?.IsEliminated == true,
            _ => throw new BusinessRuleException("The mission objective is not supported.")
        };

    public void ReleaseFromPlayer()
    {
        PlayerId = null;
        IsCompleted = false;
    }

}

public enum MissionObjectiveType
{
    ControlAllTerritories,
    ControlContinent,
    EliminatePlayer
}
