using Risk.Application.Games;
using Risk.Domain.Entities;
using Risk.Domain.Map;

namespace Risk.Tests.Application;

public class DeckTests
{
    private static readonly IReadOnlyList<MapTerritory> Territories = ClassicMap.Territories;

    private static Game GameWith(int players, bool hasMissions = false)
    {
        var game = new Game(Guid.NewGuid());
        for (var i = 0; i < players; i++)
            game.AddPlayer(Guid.NewGuid(), $"color{i}");
        game.ConfigureSettings(6, 0, 0, false, hasMissions);
        return game;
    }

    // ---- BonusDeck --------------------------------------------------------

    [Fact]
    public void The_bonus_deck_has_one_card_per_territory_and_two_wild_cards()
    {
        var gameId = Guid.NewGuid();

        var deck = BonusDeck.Create(gameId, Territories);

        Assert.Equal(Territories.Count + 2, deck.Count);
        Assert.All(deck, card => Assert.Equal(gameId, card.GameId));
        Assert.Equal(2, deck.Count(card => card.Type == BonusCardType.Wild));
        Assert.Equal(
            Territories.Select(t => t.Id),
            deck.Where(card => card.Type != BonusCardType.Wild).Select(card => card.TerritoryIdentifier));
    }

    [Fact]
    public void The_bonus_deck_spreads_the_three_symbols_evenly()
    {
        var deck = BonusDeck.Create(Guid.NewGuid(), Territories);

        var counts = deck
            .Where(card => card.Type != BonusCardType.Wild)
            .GroupBy(card => card.Type)
            .Select(group => group.Count())
            .ToList();

        Assert.Equal(3, counts.Count);
        Assert.True(counts.Max() - counts.Min() <= 1);
    }

    // ---- MissionDeck ------------------------------------------------------

    [Fact]
    public void Every_player_gets_one_mission_assigned_in_turn_order()
    {
        var game = GameWith(3, hasMissions: true);

        var missions = MissionDeck.CreateFor(game, Territories);

        var players = game.Players.OrderBy(p => p.TurnOrder).ToList();
        Assert.Equal(3, missions.Count);
        Assert.Equal(players.Select(p => p.Id), missions.Select(m => m.PlayerId!.Value));
        Assert.All(missions, m => Assert.Equal(MissionObjectiveType.ControlContinent, m.ObjectiveType));
        Assert.Equal(3, missions.Select(m => m.TargetIdentifier).Distinct().Count());
    }

    [Fact]
    public void Continents_are_reused_when_there_are_more_players_than_continents()
    {
        // A board with only two continents and three players.
        var twoContinents = Territories.Select(t => t.ContinentId).Distinct().Take(2).ToList();
        var smallBoard = Territories.Where(t => twoContinents.Contains(t.ContinentId)).ToList();
        var game = GameWith(3, hasMissions: true);

        var missions = MissionDeck.CreateFor(game, smallBoard);

        Assert.Equal(3, missions.Count);
        Assert.Equal(2, missions.Select(m => m.TargetIdentifier).Distinct().Count());
        Assert.Equal(missions[0].TargetIdentifier, missions[2].TargetIdentifier);
    }

    [Fact]
    public void A_map_without_territories_gives_no_missions()
    {
        var game = GameWith(2, hasMissions: true);

        Assert.Empty(MissionDeck.CreateFor(game, []));
    }

    // ---- GameDeckSetup ----------------------------------------------------

    [Fact]
    public async Task Setup_stores_the_bonus_deck_once()
    {
        var game = GameWith(2);
        var bonusCards = new FakeBonusCardRepository();
        var setup = new GameDeckSetup(bonusCards, new FakeMissionCardRepository());

        await setup.CreateAsync(game, Territories);
        await setup.CreateAsync(game, Territories);

        Assert.Equal(Territories.Count + 2, (await bonusCards.GetByGameIdAsync(game.Id)).Count);
    }

    [Fact]
    public async Task Setup_only_creates_missions_when_the_game_uses_them()
    {
        var withoutMissions = GameWith(2, hasMissions: false);
        var withMissions = GameWith(2, hasMissions: true);
        var missionCards = new FakeMissionCardRepository();
        var setup = new GameDeckSetup(new FakeBonusCardRepository(), missionCards);

        await setup.CreateAsync(withoutMissions, Territories);
        await setup.CreateAsync(withMissions, Territories);
        await setup.CreateAsync(withMissions, Territories);

        Assert.Empty(await missionCards.GetByGameIdAsync(withoutMissions.Id));
        Assert.Equal(2, (await missionCards.GetByGameIdAsync(withMissions.Id)).Count);
    }
}
