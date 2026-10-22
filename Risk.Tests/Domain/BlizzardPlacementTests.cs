using Risk.Domain.Exceptions;
using Risk.Domain.Map;

namespace Risk.Tests.Domain;

public class BlizzardPlacementTests
{
    private static readonly MapGraph Map = ClassicMap.Graph;

    public static TheoryData<int> Seeds() => [.. Enumerable.Range(0, 50)];

    [Theory]
    [MemberData(nameof(Seeds))]
    public void Never_splits_the_board(int seed)
    {
        var blizzards = BlizzardPlacement.Choose(Map, count: 8, minPlayable: 6, new Random(seed));

        var playable = Map.TerritoryIds.Where(id => !blizzards.Contains(id)).ToHashSet();
        Assert.Equal(8, blizzards.Count);
        Assert.True(Map.IsConnected(playable));
    }

    [Fact]
    public void Never_picks_a_chokepoint()
    {
        // Some single territories cut the board on their own (e.g. Indonesia is Australia's
        // only link to Asia). Across many seeds, such a chokepoint must never be chosen.
        for (var seed = 0; seed < 200; seed++)
        {
            var blizzards = BlizzardPlacement.Choose(Map, count: 1, minPlayable: 2, new Random(seed));
            var playable = Map.TerritoryIds.Where(id => !blizzards.Contains(id)).ToHashSet();
            Assert.True(Map.IsConnected(playable), $"Seed {seed} split the board with {string.Join(", ", blizzards)}.");
        }
    }

    [Fact]
    public void Same_seed_gives_same_placement()
    {
        var first = BlizzardPlacement.Choose(Map, 6, 2, new Random(42));
        var second = BlizzardPlacement.Choose(Map, 6, 2, new Random(42));

        Assert.Equal(first.Order(), second.Order());
    }

    [Fact]
    public void Zero_means_no_blizzards()
    {
        Assert.Empty(BlizzardPlacement.Choose(Map, 0, 6, new Random(1)));
    }

    [Fact]
    public void Leaves_at_least_one_territory_per_player()
    {
        Assert.Throws<BusinessRuleException>(() =>
            BlizzardPlacement.Choose(Map, count: 38, minPlayable: 6, new Random(1)));
    }

    [Fact]
    public void Can_cover_all_but_one_territory_on_a_line()
    {
        // a-b-c-d: b and c are chokepoints at first, but become safe once an end is gone.
        var line = TinyMap(("a", "b"), ("b", "c"), ("c", "d"));

        for (var seed = 0; seed < 20; seed++)
            Assert.Equal(3, BlizzardPlacement.Choose(line, 3, 1, new Random(seed)).Count);
    }

    [Fact]
    public void Gives_up_on_a_map_that_is_already_split()
    {
        // Two islands: any blizzard leaves the rest disconnected.
        var islands = TinyMap(("a", "b"), ("c", "d"));

        Assert.Throws<BusinessRuleException>(() => BlizzardPlacement.Choose(islands, 1, 1, new Random(1)));
    }

    private static MapGraph TinyMap(params (string From, string To)[] edges)
    {
        var ids = edges.SelectMany(e => new[] { e.From, e.To }).Distinct().ToList();
        return new MapGraph(
            [ClassicMap.Continents[0]],
            ids.Select(NewTerritory),
            edges.Select(e => NewBorder(e.From, e.To)));
    }

    // Map entities have internal constructors; the test assembly can see them.
    private static MapTerritory NewTerritory(string id) => new(id, ClassicMap.Continents[0].Id);
    private static MapBorder NewBorder(string from, string to) => new(from, to);
}
