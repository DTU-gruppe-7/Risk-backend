using Risk.Domain.Entities;
using Risk.Domain.Exceptions;

namespace Risk.Domain.Map;

/// <summary>
/// One game's board: the static map combined with that game's territory state.
/// The single place that knows how blizzards change movement, bonuses and visibility,
/// so attack, fortify and fog of war all use the same rules.
/// </summary>
public sealed class GameBoard
{
    private readonly MapGraph _map;
    private readonly Dictionary<string, Territory> _territories;

    public GameBoard(MapGraph map, IEnumerable<Territory> territories)
    {
        _map = map;
        _territories = territories.ToDictionary(t => t.Identifier);

        var unknown = _territories.Keys.FirstOrDefault(id => !map.Contains(id));
        if (unknown is not null)
            throw new DomainValidationException($"Territory '{unknown}' is not on the map.");
    }

    public Territory this[string territoryId] =>
        _territories.TryGetValue(territoryId, out var territory)
            ? territory
            : throw new DomainValidationException($"Territory '{territoryId}' is not part of this game.");

    public bool IsBlizzard(string territoryId) => this[territoryId].IsBlizzard;

    // -------------------------------------------------------------------------
    // Movement
    // -------------------------------------------------------------------------

    /// <summary>Map neighbours, minus blizzards. A blizzard itself has no neighbours.</summary>
    public IEnumerable<string> NeighborsOf(string territoryId) =>
        IsBlizzard(territoryId)
            ? []
            : _map.NeighborsOf(territoryId).Where(n => !IsBlizzard(n));

    public bool AreAdjacent(string a, string b) => NeighborsOf(a).Contains(b);

    /// <summary>
    /// The player owns <paramref name="from"/> with at least 2 troops (one must stay behind),
    /// and <paramref name="to"/> is an adjacent playable territory they do not own.
    /// </summary>
    public bool CanAttack(string from, string to, Guid playerId) =>
        IsOwnedBy(from, playerId)
        && this[from].TroopCount >= 2
        && !IsOwnedBy(to, playerId)
        && AreAdjacent(from, to);

    /// <summary>
    /// Troops can move between two of the player's territories when a path of the
    /// player's own territories connects them (blizzards block the path).
    /// </summary>
    public bool CanFortify(string from, string to, Guid playerId) =>
        from != to
        && IsOwnedBy(from, playerId)
        && IsOwnedBy(to, playerId)
        && this[from].TroopCount >= 2
        && _map.ReachableFrom(from, id => IsOwnedBy(id, playerId)).Contains(to);

    // -------------------------------------------------------------------------
    // Reinforcements
    // -------------------------------------------------------------------------

    /// <summary>
    /// Continents the player holds. Blizzards do not count: a continent is held when the
    /// player owns every non-blizzard territory in it. A continent that is entirely
    /// blizzard can never be held.
    /// </summary>
    public IReadOnlyList<MapContinent> ContinentsHeldBy(Guid playerId) =>
        _map.Continents
            .Where(continent =>
            {
                var playable = _map.TerritoriesIn(continent.Id).Where(id => !IsBlizzard(id)).ToList();
                return playable.Count > 0 && playable.All(id => IsOwnedBy(id, playerId));
            })
            .ToList();

    public int ContinentBonusFor(Guid playerId) => ContinentsHeldBy(playerId).Sum(c => c.CaptureBonus);

    // -------------------------------------------------------------------------
    // Visibility (fog of war)
    // -------------------------------------------------------------------------

    /// <summary>
    /// What a player may see under fog of war: their own territories, the playable
    /// territories bordering them, and every blizzard (blizzards are public).
    /// Computed from the current state, never stored, since it changes with every conquest.
    /// </summary>
    public IReadOnlySet<string> VisibleTo(Guid playerId)
    {
        var visible = new HashSet<string>();
        foreach (var territory in _territories.Values)
        {
            if (territory.IsBlizzard)
            {
                visible.Add(territory.Identifier);
            }
            else if (territory.OwnerId == playerId)
            {
                visible.Add(territory.Identifier);
                visible.UnionWith(NeighborsOf(territory.Identifier));
            }
        }
        return visible;
    }

    private bool IsOwnedBy(string territoryId, Guid playerId)
    {
        var territory = this[territoryId];
        return !territory.IsBlizzard && territory.OwnerId == playerId;
    }
}
