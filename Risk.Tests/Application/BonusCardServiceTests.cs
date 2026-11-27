using Risk.Application.Cards;
using Risk.Application.Common;
using Risk.Application.Exceptions;
using Risk.Domain.Entities;
using Risk.Domain.Map;
using Risk.Domain.Services;
using Risk.Tests.Domain;

namespace Risk.Tests.Application;

public class BonusCardServiceTests
{
    private sealed record Setup(Game Game, Player Red, FakeBonusCardRepository Cards, FakeUnitOfWork UnitOfWork, BonusCardService Service);

    /// <summary>A started game where the current user plays red, whose turn it is.</summary>
    private static Setup Create(CardTradeMode mode = CardTradeMode.Progressive)
    {
        var game = new Game(Guid.NewGuid());
        var red = game.AddPlayer(Guid.NewGuid(), "red");
        game.AddPlayer(Guid.NewGuid(), "blue");
        game.ConfigureSettings(6, 0, 0, false, false, cardTradeMode: mode);
        game.StartInJoinOrder(ClassicMap.Graph);

        var games = new FakeGameRepository();
        games.Add(game);
        var cards = new FakeBonusCardRepository();
        var unitOfWork = new FakeUnitOfWork();
        var currentUser = new FakeCurrentUser(red.UserId);
        var service = new BonusCardService(
            cards, unitOfWork, new GameAccess(games, currentUser));
        return new Setup(game, red, cards, unitOfWork, service);
    }

    private static List<Guid> Give(Setup s, Player owner, params BonusCardType[] types) =>
        types.Select(type =>
        {
            var card = new BonusCard(s.Game.Id, type);
            card.AssignToPlayer(owner.Id);
            s.Cards.Add(card);
            return card.Id;
        }).ToList();

    [Fact]
    public async Task The_preview_shows_the_value_without_trading()
    {
        var s = Create(CardTradeMode.Fixed);
        var ids = Give(s, s.Red, BonusCardType.Artillery, BonusCardType.Artillery, BonusCardType.Wild);

        var preview = await s.Service.PreviewTradeAsync(s.Game.Id, new TradeCardsRequest(ids));

        Assert.Equal(new TradePreviewResponse(true, 8, null, 0), preview);
        Assert.Equal(3, (await s.Cards.GetByPlayerIdAsync(s.Game.Id, s.Red.Id)).Count);
        Assert.Equal(0, s.UnitOfWork.SaveCount);
    }

    [Fact]
    public async Task The_preview_of_fewer_than_three_cards_is_not_valid()
    {
        var s = Create();
        var ids = Give(s, s.Red, BonusCardType.Infantry, BonusCardType.Infantry);

        var preview = await s.Service.PreviewTradeAsync(s.Game.Id, new TradeCardsRequest(ids));

        Assert.Equal(new TradePreviewResponse(false, null, null, 0), preview);
    }

    [Fact]
    public async Task The_preview_refuses_cards_outside_the_callers_hand_like_the_trade()
    {
        var s = Create();
        var blue = s.Game.Players.Single(p => p.Id != s.Red.Id);
        var ids = Give(s, s.Red, BonusCardType.Infantry, BonusCardType.Infantry)
            .Concat(Give(s, blue, BonusCardType.Infantry))
            .ToList();

        await Assert.ThrowsAsync<ForbiddenException>(() =>
            s.Service.PreviewTradeAsync(s.Game.Id, new TradeCardsRequest(ids)));
        await Assert.ThrowsAsync<ForbiddenException>(() =>
            s.Service.TradeAsync(s.Game.Id, new TradeCardsRequest(ids)));
    }

    [Fact]
    public void The_rules_response_is_built_from_the_rules_the_trades_use()
    {
        var rules = CardTradeRulesResponse.Current;

        Assert.Equal(CardTradeRules.FixedSets.Select(set => set.Troops), rules.FixedSets.Select(set => set.Troops));
        Assert.Equal([4, 6, 8, 10, 12, 15, 20], rules.FirstProgressiveValues);
        Assert.Equal(2, rules.OwnedTerritoryBonus);
        Assert.Equal(3, rules.CardsPerTrade);
        Assert.Equal(Game.ForcedCardTradeThreshold, rules.ForcedTradeHandSize);
    }
}
