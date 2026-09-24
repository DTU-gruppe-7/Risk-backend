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
        if (gameId == Guid.Empty)
            throw new ArgumentException("GameId must be a valid GUID.", nameof(gameId));
        if (string.IsNullOrWhiteSpace(title))
            throw new ArgumentException("Title must not be empty.", nameof(title));
        if (string.IsNullOrWhiteSpace(description))
            throw new ArgumentException("Description must not be empty.", nameof(description));

        GameId = gameId;
        Title = title.Trim();
        Description = description.Trim();
    }

    public void AssignToPlayer(Guid playerId)
    {
        if (playerId == Guid.Empty)
            throw new ArgumentException("PlayerId must be a valid GUID.", nameof(playerId));
        if (PlayerId.HasValue)
            throw new InvalidOperationException("Mission card is already assigned.");

        PlayerId = playerId;
    }

    public void Complete()
    {
        if (!PlayerId.HasValue)
            throw new InvalidOperationException("Cannot complete an unassigned mission.");
        if (IsCompleted)
            throw new InvalidOperationException("Mission is already completed.");

        IsCompleted = true;
    }
}
