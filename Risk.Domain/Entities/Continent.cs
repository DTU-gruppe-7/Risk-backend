namespace Risk.Domain.Entities;

/// <summary>
/// A continent on the Risk board. Controlling all territories in a continent
/// grants the owning player a reinforcement bonus each turn.
/// </summary>
public sealed class Continent
{
    public Guid Id { get; private set; } = Guid.CreateVersion7();

    public Guid GameId { get; private set; }

    public string Name { get; private set; } = string.Empty;

    /// <summary>
    /// Extra reinforcement troops awarded per turn to the player
    /// who controls all territories in this continent.
    /// </summary>
    public int CaptureBonus { get; private set; }

    // Navigation properties
    public Game? Game { get; private set; }

    private readonly List<Territory> _territories = [];
    public IReadOnlyCollection<Territory> Territories => _territories.AsReadOnly();

    // EF Core requires a parameterless constructor
    private Continent() { }

    public Continent(Guid gameId, string name, int captureBonus)
    {
        if (gameId == Guid.Empty)
            throw new ArgumentException("GameId must be a valid GUID.", nameof(gameId));
        if (string.IsNullOrWhiteSpace(name))
            throw new ArgumentException("Name must not be empty.", nameof(name));
        if (captureBonus < 0)
            throw new ArgumentOutOfRangeException(nameof(captureBonus), "CaptureBonus must be non-negative.");

        GameId = gameId;
        Name = name.Trim();
        CaptureBonus = captureBonus;
    }
}
