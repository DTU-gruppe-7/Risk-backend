namespace Risk.Domain.Map;

// The board itself: reference data that is the same for every game and never
// changes at runtime. Seeded from ClassicMap through EF migrations.
// Per-game state (owner, troops, active) lives in Entities.Territory.

/// <summary>A continent on the board, e.g. "north-america".</summary>
public sealed class MapContinent
{
    /// <summary>Stable slug, also used by the frontend.</summary>
    public string Id { get; private set; } = string.Empty;
    public string Name { get; private set; } = string.Empty;

    /// <summary>Extra troops per turn for holding every territory in the continent.</summary>
    public int CaptureBonus { get; private set; }

    private readonly List<MapTerritory> _territories = [];
    public IReadOnlyCollection<MapTerritory> Territories => _territories.AsReadOnly();

    private MapContinent() { }

    internal MapContinent(string id, string name, int captureBonus)
    {
        Id = id;
        Name = name;
        CaptureBonus = captureBonus;
    }
}

/// <summary>A territory on the board, e.g. "north-africa".</summary>
public sealed class MapTerritory
{
    /// <summary>Stable slug, matches the frontend's map geometry ids.</summary>
    public string Id { get; private set; } = string.Empty;
    public string ContinentId { get; private set; } = string.Empty;

    public MapContinent? Continent { get; private set; }

    private readonly List<MapBorder> _borders = [];
    /// <summary>One row per neighbour (both directions are stored).</summary>
    public IReadOnlyCollection<MapBorder> Borders => _borders.AsReadOnly();

    private MapTerritory() { }

    internal MapTerritory(string id, string continentId)
    {
        Id = id;
        ContinentId = continentId;
    }
}

/// <summary>A directed adjacency. ClassicMap stores both directions of every border.</summary>
public sealed class MapBorder
{
    public string TerritoryId { get; private set; } = string.Empty;
    public string NeighborId { get; private set; } = string.Empty;

    private MapBorder() { }

    internal MapBorder(string territoryId, string neighborId)
    {
        TerritoryId = territoryId;
        NeighborId = neighborId;
    }
}
