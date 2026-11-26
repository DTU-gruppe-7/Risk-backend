using Risk.Domain.Entities;
using Risk.Domain.Exceptions;
using Risk.Domain.Map;
using Risk.Domain.Services;

namespace Risk.Tests.Domain;

/// <summary>Trading bonus cards: progressive and fixed set values, and the territory bonus.</summary>
public class CardTradeTests
{
    private static readonly MapGraph Map = ClassicMap.Graph;

    private const BonusCardType I = BonusCardType.Infantry;
    private const BonusCardType C = BonusCardType.Cavalry;
    private const BonusCardType A = BonusCardType.Artillery;
    private const BonusCardType W = BonusCardType.Wild;

    private sealed record Scenario(Game Game, Player Red, Player Blue);

    /// <summary>A two-player game in red's Deployment phase.</summary>
    private static Scenario Started(CardTradeMode mode)
    {
        var game = new Game(Guid.NewGuid());
        var red = game.AddPlayer(Guid.NewGuid(), "red");
        var blue = game.AddPlayer(Guid.NewGuid(), "blue");
        game.ConfigureSettings(6, 0, 0, false, false, cardTradeMode: mode);
        game.StartInJoinOrder(Map);
        var current = game.FindPlayer(game.CurrentPlayerId!.Value)!;
        return current.Id == red.Id ? new Scenario(game, red, blue) : new Scenario(game, blue, red);
    }

    /// <summary>Cards in the current player's hand, without a territory unless one is given.</summary>
    private static List<BonusCard> Hand(Scenario s, params (BonusCardType Type, string? Territory)[] cards) =>
        cards.Select(c =>
        {
            var card = new BonusCard(s.Game.Id, c.Type, c.Territory);
            card.AssignToPlayer(s.Red.Id);
            return card;
        }).ToList();

    private static List<BonusCard> Hand(Scenario s, params BonusCardType[] types) =>
        Hand(s, types.Select(t => (t, (string?)null)).ToArray());

    private static string OwnedBy(Scenario s, Player player, int skip = 0) =>
        s.Game.Territories.Where(t => t.OwnerId == player.Id).Skip(skip).First().Identifier;

    private static Territory TerritoryOf(Scenario s, string identifier) =>
        s.Game.Territories.Single(t => t.Identifier == identifier);

    // -------------------------------------------------------------------------
    // Set values
    // -------------------------------------------------------------------------

    [Theory]
    [InlineData(I, I, I, 4)]
    [InlineData(C, C, C, 6)]
    [InlineData(A, A, A, 8)]
    [InlineData(I, C, A, 10)]
    [InlineData(I, I, W, 4)]
    [InlineData(C, W, C, 6)]
    [InlineData(A, A, W, 8)]
    [InlineData(I, C, W, 10)]
    [InlineData(A, W, W, 10)]
    [InlineData(W, W, W, 10)]
    public void Fixed_sets_are_worth_their_table_value_with_wilds_counted_as_the_best_set(
        BonusCardType a, BonusCardType b, BonusCardType c, int expected)
    {
        var s = Started(CardTradeMode.Fixed);
        var reserve = s.Red.ReinforcementTroops;

        var result = s.Game.TradeBonusCards(s.Red.Id, Hand(s, a, b, c));

        Assert.Equal(expected, result.ReserveTroops);
        Assert.Equal(reserve + expected, s.Red.ReinforcementTroops);
    }

    [Fact]
    public void Fixed_sets_do_not_grow_with_each_trade()
    {
        var s = Started(CardTradeMode.Fixed);

        var first = s.Game.TradeBonusCards(s.Red.Id, Hand(s, I, I, I));
        var second = s.Game.TradeBonusCards(s.Red.Id, Hand(s, I, I, I));

        Assert.Equal(4, first.ReserveTroops);
        Assert.Equal(4, second.ReserveTroops);
        Assert.Equal(0, s.Game.BonusCardTradeCount);
    }

    [Fact]
    public void Progressive_sets_grow_with_every_trade_in_the_game_whatever_the_cards()
    {
        var s = Started(CardTradeMode.Progressive);

        var values = Enumerable.Range(0, 3)
            .Select(_ => s.Game.TradeBonusCards(s.Red.Id, Hand(s, A, A, A)).ReserveTroops)
            .ToList();

        Assert.Equal([4, 6, 8], values);
        Assert.Equal(3, s.Game.BonusCardTradeCount);
    }

    [Theory]
    [InlineData(0, 4)]
    [InlineData(1, 6)]
    [InlineData(2, 8)]
    [InlineData(3, 10)]
    [InlineData(4, 12)]
    [InlineData(5, 15)]
    [InlineData(6, 20)]
    [InlineData(7, 25)]
    public void Progressive_values_follow_the_official_sequence(int tradeNumber, int expected)
    {
        var s = Started(CardTradeMode.Progressive);
        Assert.Equal(expected, CardTradeRules.SetValue(CardTradeMode.Progressive, Hand(s, I, C, A), tradeNumber));
    }

    [Fact]
    public void Progressive_is_the_default()
    {
        Assert.Equal(CardTradeMode.Progressive, new Game(Guid.NewGuid()).Settings!.CardTradeMode);
    }

    [Fact]
    public void Invalid_sets_are_refused()
    {
        var s = Started(CardTradeMode.Fixed);
        Assert.Throws<BusinessRuleException>(() => s.Game.TradeBonusCards(s.Red.Id, Hand(s, I, I, C)));
    }

    // -------------------------------------------------------------------------
    // Territory bonus
    // -------------------------------------------------------------------------

    [Theory]
    [InlineData(CardTradeMode.Progressive)]
    [InlineData(CardTradeMode.Fixed)]
    public void An_owned_territory_on_a_card_gets_two_troops_placed_directly_on_it(CardTradeMode mode)
    {
        var s = Started(mode);
        var owned = TerritoryOf(s, OwnedBy(s, s.Red));
        var troopsBefore = owned.TroopCount;
        var reserve = s.Red.ReinforcementTroops;

        var result = s.Game.TradeBonusCards(s.Red.Id, Hand(s, (I, owned.Identifier), (I, null), (I, null)));

        Assert.Equal(owned.Id, result.BonusTerritoryId);
        Assert.Equal(2, result.BonusTroops);
        Assert.Equal(troopsBefore + 2, owned.TroopCount);
        // The reserve only gets the set's value; the bonus is not added to it.
        Assert.Equal(reserve + 4, s.Red.ReinforcementTroops);
    }

    [Fact]
    public void Only_one_territory_bonus_per_trade_and_the_first_matching_card_decides_where()
    {
        var s = Started(CardTradeMode.Fixed);
        var first = TerritoryOf(s, OwnedBy(s, s.Red, skip: 0));
        var second = TerritoryOf(s, OwnedBy(s, s.Red, skip: 1));
        var secondBefore = second.TroopCount;

        var result = s.Game.TradeBonusCards(s.Red.Id,
            Hand(s, (I, first.Identifier), (C, second.Identifier), (A, null)));

        Assert.Equal(first.Id, result.BonusTerritoryId);
        Assert.Equal(2, result.BonusTroops);
        Assert.Equal(secondBefore, second.TroopCount);
        Assert.Equal(10, result.ReserveTroops);
    }

    [Fact]
    public void A_territory_owned_by_someone_else_gives_no_bonus()
    {
        var s = Started(CardTradeMode.Fixed);
        var theirs = TerritoryOf(s, OwnedBy(s, s.Blue));
        var before = theirs.TroopCount;

        var result = s.Game.TradeBonusCards(s.Red.Id, Hand(s, (I, theirs.Identifier), (I, null), (I, null)));

        Assert.Null(result.BonusTerritoryId);
        Assert.Equal(0, result.BonusTroops);
        Assert.Equal(before, theirs.TroopCount);
    }

    [Fact]
    public void The_territory_bonus_is_given_at_most_once_per_turn()
    {
        var s = Started(CardTradeMode.Fixed);
        var owned = OwnedBy(s, s.Red);

        var first = s.Game.TradeBonusCards(s.Red.Id, Hand(s, (I, owned), (I, null), (I, null)));
        var second = s.Game.TradeBonusCards(s.Red.Id, Hand(s, (I, owned), (I, null), (I, null)));

        Assert.Equal(2, first.BonusTroops);
        Assert.Equal(0, second.BonusTroops);
        Assert.Null(second.BonusTerritoryId);
        Assert.True(s.Game.HasReceivedTerritoryBonusThisTurn);
    }

    [Fact]
    public void The_territory_bonus_is_available_again_next_turn()
    {
        var s = Started(CardTradeMode.Fixed);
        s.Game.TradeBonusCards(s.Red.Id, Hand(s, (I, OwnedBy(s, s.Red)), (I, null), (I, null)));

        var anywhere = TerritoryOf(s, OwnedBy(s, s.Red));
        // Deploying the whole reserve moves on to Attack by itself.
        s.Game.DeployTroops(s.Red.Id, anywhere, s.Red.ReinforcementTroops);
        Assert.Equal(GamePhase.Attack, s.Game.CurrentPhase);
        s.Game.EndPhase(s.Red.Id, GamePhase.Fortify, Map, DateTimeOffset.UtcNow);
        s.Game.EndPhase(s.Red.Id, GamePhase.Deployment, Map, DateTimeOffset.UtcNow);

        Assert.Equal(s.Blue.Id, s.Game.CurrentPlayerId);
        Assert.False(s.Game.HasReceivedTerritoryBonusThisTurn);
    }
}
