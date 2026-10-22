using Risk.Domain.Entities;
using Risk.Domain.Map;

namespace Risk.Tests.Domain;

public class GameBoardTests
{
    private static readonly MapGraph Map = ClassicMap.Graph;
    private static readonly Guid Red = Guid.NewGuid();
    private static readonly Guid Blue = Guid.NewGuid();

    /// <summary>A started-game board where every territory is unowned and nothing is a blizzard.</summary>
    private static (GameBoard Board, Dictionary<string, Territory> Territories) NewBoard(params string[] blizzards)
    {
        var gameId = Guid.NewGuid();
        var territories = Map.TerritoryIds.ToDictionary(id => id, id => new Territory(gameId, id));
        foreach (var id in blizzards)
            territories[id].MakeBlizzard();

        return (new GameBoard(Map, territories.Values), territories);
    }

    private static void Own(Dictionary<string, Territory> territories, Guid player, int troops, params string[] ids)
    {
        foreach (var id in ids)
            territories[id].Claim(player, troops);
    }

    // -------------------------------------------------------------------------
    // Movement
    // -------------------------------------------------------------------------

    [Fact]
    public void Blizzards_are_removed_from_neighbours()
    {
        var (board, _) = NewBoard("kamchatka");

        Assert.DoesNotContain("kamchatka", board.NeighborsOf("alaska"));
        Assert.Contains("alberta", board.NeighborsOf("alaska"));
        Assert.Empty(board.NeighborsOf("kamchatka"));
    }

    [Fact]
    public void Cannot_attack_into_or_out_of_a_blizzard()
    {
        var (board, t) = NewBoard("kamchatka");
        Own(t, Red, 5, "alaska");
        Own(t, Blue, 1, "alberta");

        Assert.True(board.CanAttack("alaska", "alberta", Red));
        Assert.False(board.CanAttack("alaska", "kamchatka", Red));
    }

    [Fact]
    public void Attack_needs_two_troops_an_enemy_target_and_a_border()
    {
        var (board, t) = NewBoard();
        Own(t, Red, 1, "alaska");
        Own(t, Red, 3, "alberta");
        Own(t, Blue, 1, "ontario", "japan");

        Assert.False(board.CanAttack("alaska", "northwest-territory", Red)); // only 1 troop
        Assert.True(board.CanAttack("alberta", "ontario", Red));
        Assert.False(board.CanAttack("alberta", "alaska", Red));             // own territory
        Assert.False(board.CanAttack("alberta", "japan", Red));              // not adjacent
    }

    [Fact]
    public void Fortify_follows_own_territories_and_is_blocked_by_blizzards()
    {
        // alaska – alberta – ontario all red: path exists.
        var (open, t1) = NewBoard();
        Own(t1, Red, 3, "alaska", "alberta", "ontario");
        Assert.True(open.CanFortify("alaska", "ontario", Red));

        // Same, but alberta is a blizzard and northwest-territory is enemy: no path.
        var (blocked, t2) = NewBoard("alberta");
        Own(t2, Red, 3, "alaska", "ontario");
        Own(t2, Blue, 1, "northwest-territory");
        Assert.False(blocked.CanFortify("alaska", "ontario", Red));
    }

    // -------------------------------------------------------------------------
    // Continent bonus
    // -------------------------------------------------------------------------

    [Fact]
    public void Continent_bonus_requires_every_territory_without_blizzards()
    {
        var (board, t) = NewBoard();
        Own(t, Red, 1, "eastern-australia", "new-guinea", "indonesia");

        Assert.Equal(0, board.ContinentBonusFor(Red));

        Own(t, Red, 1, "western-australia");
        Assert.Equal(2, board.ContinentBonusFor(Red));
    }

    [Fact]
    public void Blizzards_do_not_count_towards_continent_bonus()
    {
        // South America with Peru under a blizzard: owning the other three is enough.
        var (board, t) = NewBoard("peru");
        Own(t, Red, 1, "venezuela", "brazil", "argentina");

        Assert.Equal(["south-america"], board.ContinentsHeldBy(Red).Select(c => c.Id));
        Assert.Equal(2, board.ContinentBonusFor(Red));
    }

    [Fact]
    public void Continent_entirely_under_blizzard_gives_no_bonus()
    {
        var (board, _) = NewBoard("venezuela", "brazil", "peru", "argentina");

        Assert.Equal(0, board.ContinentBonusFor(Red));
    }

    // -------------------------------------------------------------------------
    // Visibility (fog of war)
    // -------------------------------------------------------------------------

    [Fact]
    public void Player_sees_own_territories_their_neighbours_and_all_blizzards()
    {
        var (board, t) = NewBoard("kamchatka", "madagascar");
        Own(t, Red, 1, "alaska");

        var visible = board.VisibleTo(Red);

        Assert.Equal(
            new[] { "alaska", "alberta", "kamchatka", "madagascar", "northwest-territory" },
            visible.Order());
    }
}
