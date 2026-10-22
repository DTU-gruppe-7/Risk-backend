using Risk.Domain.Map;

namespace Risk.Tests.Domain;

// Guards the seeded board: a typo in ClassicMap would otherwise only show up as a
// broken migration or a missing border in a running game.
public class ClassicMapTests
{
    private static readonly HashSet<string> TerritoryIds = ClassicMap.Territories.Select(t => t.Id).ToHashSet();

    [Fact]
    public void Has_the_classic_six_continents_and_42_territories()
    {
        Assert.Equal(6, ClassicMap.Continents.Count);
        Assert.Equal(42, TerritoryIds.Count);
        Assert.Equal(42, ClassicMap.Territories.Count);
    }

    [Fact]
    public void Every_territory_belongs_to_a_known_continent()
    {
        var continentIds = ClassicMap.Continents.Select(c => c.Id).ToHashSet();

        Assert.All(ClassicMap.Territories, t => Assert.Contains(t.ContinentId, continentIds));
    }

    [Fact]
    public void Borders_only_reference_known_territories_and_never_themselves()
    {
        Assert.All(ClassicMap.Borders, b =>
        {
            Assert.Contains(b.TerritoryId, TerritoryIds);
            Assert.Contains(b.NeighborId, TerritoryIds);
            Assert.NotEqual(b.TerritoryId, b.NeighborId);
        });
    }

    [Fact]
    public void Borders_are_symmetric_and_unique()
    {
        var pairs = ClassicMap.Borders.Select(b => (b.TerritoryId, b.NeighborId)).ToList();
        var set = pairs.ToHashSet();

        Assert.Equal(pairs.Count, set.Count);
        Assert.All(pairs, p => Assert.Contains((p.NeighborId, p.TerritoryId), set));
    }

    [Fact]
    public void Classic_board_has_83_borders()
    {
        Assert.Equal(83 * 2, ClassicMap.Borders.Count);
    }

    [Fact]
    public void Whole_board_is_connected()
    {
        Assert.True(ClassicMap.Graph.IsConnected(TerritoryIds));
    }

    [Fact]
    public void Every_territory_has_at_least_one_neighbor()
    {
        var withBorders = ClassicMap.Borders.Select(b => b.TerritoryId).ToHashSet();

        Assert.All(TerritoryIds, id => Assert.Contains(id, withBorders));
    }
}
