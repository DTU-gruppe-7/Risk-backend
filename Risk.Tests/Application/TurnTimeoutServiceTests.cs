using Risk.Application.Games;
using Risk.Application.Services;
using Risk.Domain.Entities;
using Risk.Domain.Map;

namespace Risk.Tests.Application;

public class TurnTimeoutServiceTests
{
    private sealed class Fixture
    {
        public required TurnTimeoutService Service { get; init; }
        public required Game Game { get; init; }
        public required Player Red { get; init; }
        public required Player Blue { get; init; }
        public Player? Green { get; init; }
        public required Territory From { get; init; }
        public required Territory To { get; init; }
        public required DateTimeOffset TimedOutAt { get; init; }
        public required FakeBonusCardRepository BonusCards { get; init; }
        public required FakeUnitOfWork UnitOfWork { get; init; }
    }

    /// <summary>Red is mid-attack on a time-limited turn with a blue territory left at 0 troops (pending).</summary>
    private static Fixture Create(bool blueOnlyHasTarget = false, bool threePlayers = false, bool pending = true)
    {
        var game = new Game(Guid.NewGuid());
        var red = game.AddPlayer(Guid.NewGuid(), "red");
        var blue = game.AddPlayer(Guid.NewGuid(), "blue");
        var green = threePlayers ? game.AddPlayer(Guid.NewGuid(), "green") : null;
        game.ConfigureSettings(6, 0, 10, false, false);
        game.Start(ClassicMap.Graph, new Random(1));

        var from = game.Territories.First(t => t.OwnerId == red.Id);
        game.DeployTroops(red.Id, from, red.ReinforcementTroops);
        from.TransferOwnership(red.Id, 6);
        var to = game.Territories.First(t =>
            t.Identifier == ClassicMap.Graph.NeighborsOf(from.Identifier).First());
        to.TransferOwnership(blue.Id, 1);

        if (blueOnlyHasTarget)
        {
            foreach (var territory in game.Territories
                         .Where(t => t.OwnerId == blue.Id && t.Id != to.Id).ToList())
                territory.TransferOwnership((green ?? red).Id, 1);
        }

        if (pending)
        {
            to.RemoveTroops(to.TroopCount);
            game.BeginPendingConquest(red.Id, from, to);
        }

        var games = new FakeGameRepository();
        games.Add(game);
        var bonusCards = new FakeBonusCardRepository();
        var unitOfWork = new FakeUnitOfWork();
        var resolver = new ConquestResolver(
            bonusCards, new FakeMissionCardRepository(), new FakeMapRepository());

        return new Fixture
        {
            Service = new TurnTimeoutService(games, new FakeMapRepository(), resolver, unitOfWork),
            Game = game, Red = red, Blue = blue, Green = green, From = from, To = to,
            TimedOutAt = game.TurnStartedAt!.Value.AddSeconds(10),
            BonusCards = bonusCards, UnitOfWork = unitOfWork,
        };
    }

    [Fact]
    public async Task A_timed_out_turn_with_a_pending_conquest_completes_it_with_one_troop_and_moves_on()
    {
        var f = Create();
        var fromBefore = f.From.TroopCount;

        var advanced = await f.Service.AdvanceTimedOutTurnsAsync(f.TimedOutAt);

        Assert.Equal(1, advanced);
        Assert.False(f.Game.HasPendingConquest);
        Assert.Equal(f.Red.Id, f.To.OwnerId);
        Assert.Equal(1, f.To.TroopCount);
        Assert.Equal(fromBefore - 1, f.From.TroopCount);
        Assert.Equal(f.Blue.Id, f.Game.CurrentPlayerId);
        Assert.Equal(GamePhase.Deployment, f.Game.CurrentPhase);
        Assert.All(f.Game.Territories.Where(t => !t.IsBlizzard), t => Assert.True(t.TroopCount >= 1));
        Assert.Equal(1, f.UnitOfWork.SaveCount);
    }

    [Fact]
    public async Task The_timeout_eliminates_a_defeated_player_and_hands_over_their_cards()
    {
        var f = Create(blueOnlyHasTarget: true, threePlayers: true);
        for (var i = 0; i < 2; i++)
        {
            var card = new BonusCard(f.Game.Id, BonusCardType.Wild);
            card.AssignToPlayer(f.Blue.Id);
            f.BonusCards.Add(card);
        }

        await f.Service.AdvanceTimedOutTurnsAsync(f.TimedOutAt);

        Assert.True(f.Blue.IsEliminated);
        Assert.Equal(2, (await f.BonusCards.GetByPlayerIdAsync(f.Game.Id, f.Red.Id)).Count);
        Assert.Equal(f.Green!.Id, f.Game.CurrentPlayerId);
    }

    [Fact]
    public async Task The_timeout_that_completes_the_winning_conquest_finishes_the_game()
    {
        var f = Create(blueOnlyHasTarget: true);

        var advanced = await f.Service.AdvanceTimedOutTurnsAsync(f.TimedOutAt);

        Assert.Equal(0, advanced);
        Assert.Equal(GameState.Finished, f.Game.State);
        Assert.Equal(f.Red.Id, f.Game.WinnerId);
        Assert.Equal(1, f.UnitOfWork.SaveCount);
    }

    [Fact]
    public async Task A_timed_out_turn_without_a_pending_conquest_just_moves_on()
    {
        var f = Create(pending: false);

        var advanced = await f.Service.AdvanceTimedOutTurnsAsync(f.TimedOutAt);

        Assert.Equal(1, advanced);
        Assert.Equal(f.Blue.Id, f.Game.CurrentPlayerId);
    }

    [Fact]
    public async Task A_turn_that_has_not_timed_out_is_left_alone()
    {
        var f = Create();

        var advanced = await f.Service.AdvanceTimedOutTurnsAsync(f.TimedOutAt.AddSeconds(-1));

        Assert.Equal(0, advanced);
        Assert.True(f.Game.HasPendingConquest);
        Assert.Equal(f.Red.Id, f.Game.CurrentPlayerId);
        Assert.Equal(0, f.UnitOfWork.SaveCount);
    }
}
