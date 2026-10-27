using Risk.Domain.Exceptions;

namespace Risk.Domain.Map;

/// <summary>
/// Chooses which territories become blizzards when a game starts.
/// Rule: the remaining (playable) territories must stay connected, so a
/// blizzard never splits the board in two.
/// </summary>
public static class BlizzardPlacement
{
    /// <param name="map">The board.</param>
    /// <param name="count">How many blizzards to place. 0 = none.</param>
    /// <param name="minPlayable">At least this many territories must remain playable (e.g. one per player).</param>
    /// <param name="random">Source of randomness. Pass a seeded instance for reproducible results.</param>
    public static IReadOnlySet<string> Choose(MapGraph map, int count, int minPlayable, Random random)
    {
        if (count < 0)
            throw new DomainValidationException("Blizzard count must be non-negative.");
        if (count == 0)
            return new HashSet<string>();
        if (map.TerritoryIds.Count - count < Math.Max(minPlayable, 1))
            throw new BusinessRuleException("Too many blizzards: not enough territories would be left to play on.");

        var candidates = map.TerritoryIds.ToArray();
        random.Shuffle(candidates);

        var playable = map.TerritoryIds.ToHashSet();
        var blizzards = new HashSet<string>();

        // Try each territory in random order, keep it as a blizzard only if the rest of
        // the board is still connected without it. A territory rejected early may become
        // allowed later, so repeat until done or a whole pass makes no progress. On a
        // connected map this always succeeds: some territory can always be removed safely.
        var progress = true;
        while (blizzards.Count < count && progress)
        {
            progress = false;
            foreach (var candidate in candidates)
            {
                if (blizzards.Count == count) break;
                if (blizzards.Contains(candidate)) continue;

                playable.Remove(candidate);
                if (map.IsConnected(playable))
                {
                    blizzards.Add(candidate);
                    progress = true;
                }
                else
                {
                    playable.Add(candidate);
                }
            }
        }

        if (blizzards.Count < count)
            throw new BusinessRuleException($"Could not place {count} blizzards without splitting the board.");

        return blizzards;
    }
}
