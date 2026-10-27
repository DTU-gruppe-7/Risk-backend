using Risk.Domain.Exceptions;

namespace Risk.Domain.Map;

/// <summary>
/// The board's topology: which territories exist, which continent each belongs to,
/// and who borders whom. Built from the seeded map data; knows nothing about a game.
/// </summary>
public sealed class MapGraph
{
    private readonly Dictionary<string, HashSet<string>> _neighbors;
    private readonly Dictionary<string, string> _continentOf;
    private readonly Dictionary<string, MapContinent> _continents;

    public MapGraph(
        IEnumerable<MapContinent> continents,
        IEnumerable<MapTerritory> territories,
        IEnumerable<MapBorder> borders)
    {
        _continents = continents.ToDictionary(c => c.Id);
        _continentOf = territories.ToDictionary(t => t.Id, t => t.ContinentId);
        _neighbors = _continentOf.Keys.ToDictionary(id => id, _ => new HashSet<string>());

        foreach (var (territory, continent) in _continentOf)
        {
            if (!_continents.ContainsKey(continent))
                throw new DomainValidationException($"Territory '{territory}' references unknown continent '{continent}'.");
        }

        foreach (var border in borders)
        {
            if (!_neighbors.ContainsKey(border.TerritoryId) || !_neighbors.ContainsKey(border.NeighborId))
                throw new DomainValidationException($"Border '{border.TerritoryId}' → '{border.NeighborId}' references an unknown territory.");

            // Borders are logically bidirectional, whatever the data says.
            _neighbors[border.TerritoryId].Add(border.NeighborId);
            _neighbors[border.NeighborId].Add(border.TerritoryId);
        }

        // Sorted, so seeded randomness (blizzard placement) is reproducible.
        TerritoryIds = _continentOf.Keys.Order(StringComparer.Ordinal).ToList();
    }

    public IReadOnlyList<string> TerritoryIds { get; }

    public IReadOnlyCollection<MapContinent> Continents => _continents.Values;

    public bool Contains(string territoryId) => _continentOf.ContainsKey(territoryId);

    public IReadOnlySet<string> NeighborsOf(string territoryId) =>
        _neighbors.TryGetValue(territoryId, out var neighbors)
            ? neighbors
            : throw new DomainValidationException($"Unknown territory '{territoryId}'.");

    public string ContinentOf(string territoryId) =>
        _continentOf.TryGetValue(territoryId, out var continent)
            ? continent
            : throw new DomainValidationException($"Unknown territory '{territoryId}'.");

    public IEnumerable<string> TerritoriesIn(string continentId) =>
        TerritoryIds.Where(id => _continentOf[id] == continentId);

    /// <summary>Every territory reachable from <paramref name="start"/>, moving only through territories <paramref name="canEnter"/> accepts.</summary>
    public IReadOnlySet<string> ReachableFrom(string start, Func<string, bool> canEnter)
    {
        var visited = new HashSet<string> { start };
        var queue = new Queue<string>([start]);

        while (queue.TryDequeue(out var current))
        {
            foreach (var next in NeighborsOf(current))
            {
                if (canEnter(next) && visited.Add(next))
                    queue.Enqueue(next);
            }
        }

        return visited;
    }

    /// <summary>True if every territory in <paramref name="ids"/> can reach every other without leaving the set.</summary>
    public bool IsConnected(IReadOnlySet<string> ids) =>
        ids.Count == 0 || ReachableFrom(ids.First(), ids.Contains).Count == ids.Count;
}
