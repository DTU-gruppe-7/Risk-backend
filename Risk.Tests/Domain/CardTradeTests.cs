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
        // The value does not grow, but every trade is still counted (shown as cardSetsTraded).
        Assert.Equal(2, s.Game.BonusCardTradeCount);
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

    /// <summary>A loose territory on the board, owned by <paramref name="ownerId"/>.</summary>
    private static Territory Owned(string identifier, Guid ownerId)
    {
        var territory = new Territory(Guid.NewGuid(), identifier);
        territory.Claim(ownerId);
        return territory;
    }

    private static BonusCard Card(string? territory) => new(Guid.NewGuid(), I, territory);

    [Fact]
    public void The_bonus_target_is_the_first_card_whose_territory_the_player_owns()
    {
        var me = Guid.NewGuid();
        var other = Guid.NewGuid();
        Territory[] board = [Owned("alaska", other), Owned("peru", me), Owned("egypt", me)];

        var target = CardTradeRules.TerritoryBonusTarget(
            [Card(null), Card("alaska"), Card("egypt"), Card("peru")], board, me);

        Assert.Equal("egypt", target?.Identifier);
    }

    [Fact]
    public void There_is_no_bonus_target_when_the_player_owns_none_of_the_cards_territories()
    {
        var me = Guid.NewGuid();
        Territory[] board = [Owned("alaska", Guid.NewGuid()), Owned("peru", me)];

        Assert.Null(CardTradeRules.TerritoryBonusTarget([Card("alaska"), Card(null), Card(null)], board, me));
    }

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

    // -------------------------------------------------------------------------
    // Preview, upcoming values and the rules
    // -------------------------------------------------------------------------

    public static TheoryData<CardTradeMode, BonusCardType[]> PreviewHands => new()
    {
        { CardTradeMode.Progressive, [I, C, A] },
        { CardTradeMode.Progressive, [I, I, W] },
        { CardTradeMode.Fixed, [A, A, A] },
        { CardTradeMode.Fixed, [C, W, C] },
        { CardTradeMode.Fixed, [I, C, W] },
    };

    [Theory]
    [MemberData(nameof(PreviewHands))]
    public void The_preview_gives_exactly_what_the_trade_then_gives(CardTradeMode mode, BonusCardType[] types)
    {
        var s = Started(mode);
        var owned = OwnedBy(s, s.Red);
        var cards = Hand(s, (types[0], owned), (types[1], null), (types[2], null));

        var preview = s.Game.PreviewCardTrade(s.Red.Id, cards);
        var trade = s.Game.TradeBonusCards(s.Red.Id, cards);

        Assert.Equal(trade, preview);
        Assert.NotNull(preview!.BonusTerritoryId);
    }

    [Fact]
    public void The_preview_changes_nothing()
    {
        var s = Started(CardTradeMode.Progressive);
        var owned = TerritoryOf(s, OwnedBy(s, s.Red));
        var cards = Hand(s, (I, owned.Identifier), (C, null), (A, null));
        var (reserve, troops, version) = (s.Red.ReinforcementTroops, owned.TroopCount, s.Game.Version);

        s.Game.PreviewCardTrade(s.Red.Id, cards);

        Assert.All(cards, card => Assert.False(card.Used));
        Assert.Equal(0, s.Game.BonusCardTradeCount);
        Assert.Equal(reserve, s.Red.ReinforcementTroops);
        Assert.Equal(troops, owned.TroopCount);
        Assert.False(s.Game.HasReceivedTerritoryBonusThisTurn);
        Assert.Equal(version, s.Game.Version);
    }

    [Fact]
    public void The_preview_is_not_limited_to_the_deployment_phase()
    {
        var s = Started(CardTradeMode.Fixed);
        s.Game.DeployTroops(s.Red.Id, TerritoryOf(s, OwnedBy(s, s.Red)), s.Red.ReinforcementTroops);
        Assert.Equal(GamePhase.Attack, s.Game.CurrentPhase);

        Assert.Equal(10, s.Game.PreviewCardTrade(s.Red.Id, Hand(s, I, C, A))!.ReserveTroops);
    }

    [Fact]
    public void The_preview_is_null_for_anything_but_a_valid_set_of_three_own_cards()
    {
        var s = Started(CardTradeMode.Progressive);
        var card = Hand(s, I)[0];

        Assert.Null(s.Game.PreviewCardTrade(s.Red.Id, Hand(s, I, I)));
        Assert.Null(s.Game.PreviewCardTrade(s.Red.Id, Hand(s, I, I, C)));
        Assert.Null(s.Game.PreviewCardTrade(s.Red.Id, [card, card, card]));
        Assert.Null(s.Game.PreviewCardTrade(s.Blue.Id, Hand(s, I, C, A)));
    }

    [Fact]
    public void The_upcoming_values_move_on_by_one_after_a_progressive_trade()
    {
        var s = Started(CardTradeMode.Progressive);
        Assert.Equal([4, 6, 8], s.Game.UpcomingCardTradeValues(3));

        s.Game.TradeBonusCards(s.Red.Id, Hand(s, I, C, A));

        Assert.Equal([6, 8, 10], s.Game.UpcomingCardTradeValues(3));
    }

    [Fact]
    public void There_are_no_upcoming_values_in_fixed_mode()
    {
        var s = Started(CardTradeMode.Fixed);

        Assert.Empty(s.Game.UpcomingCardTradeValues(3));
    }

    [Fact]
    public void The_fixed_sets_list_every_set_without_wild_cards_and_match_the_values_traded()
    {
        Assert.Equal(
            [(I, 4), (C, 6), (A, 8)],
            CardTradeRules.FixedSets.Take(3).Select(set => (set.Types.Distinct().Single(), set.Troops)));
        Assert.Equal([I, C, A], CardTradeRules.FixedSets[3].Types);
        Assert.Equal(CardTradeRules.FixedOneOfEachValue, CardTradeRules.FixedSets[3].Troops);

        var s = Started(CardTradeMode.Fixed);
        Assert.All(CardTradeRules.FixedSets, set =>
            Assert.Equal(set.Troops, CardTradeRules.SetValue(CardTradeMode.Fixed, Hand(s, set.Types.ToArray()), 0)));
    }
}
