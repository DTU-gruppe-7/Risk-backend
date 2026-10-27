using Risk.Domain.Common;
using Risk.Domain.Exceptions;

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

    // Navigation properties
    public Game? Game { get; private set; }
    public Player? Player { get; private set; }

    // EF Core requires a parameterless constructor
    private MissionCard() { }

    public MissionCard(Guid gameId, string title, string description)
    {
        GameId = Guard.NotEmpty(gameId, nameof(gameId));
        Title = Guard.NotBlank(title, nameof(title));
        Description = Guard.NotBlank(description, nameof(description));
    }

    public void AssignToPlayer(Guid playerId)
    {
        Guard.NotEmpty(playerId, nameof(playerId));
        if (PlayerId.HasValue)
            throw new BusinessRuleException("Mission card is already assigned.");

        PlayerId = playerId;
    }

    public void Complete()
    {
        if (!PlayerId.HasValue)
            throw new BusinessRuleException("Cannot complete an unassigned mission.");
        if (IsCompleted)
            throw new BusinessRuleException("Mission is already completed.");

        IsCompleted = true;
    }
}
