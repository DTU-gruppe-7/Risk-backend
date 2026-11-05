using Risk.Application.Common;
using Risk.Application.DTOs;
using Risk.Application.Interfaces.Services;
using Risk.Application.Services;
using Risk.Domain.Entities;
using Risk.Domain.Exceptions;
using Risk.Domain.Map;

namespace Risk.Tests.Application;

public class GameplayServiceTests
{
    /// <summary>Attacker rolls only <c>attackerValue</c>, defender only <c>defenderValue</c> (calls alternate).</summary>
    private sealed class AlternatingDice(int attackerValue, int defenderValue) : IDiceRoller
    {
        public int Calls { get; private set; }

        public IReadOnlyList<int> Roll(int count)
        {
            var value = Calls++ % 2 == 0 ? attackerValue : defenderValue;
            return Enumerable.Repeat(value, count).ToList();
        }
    }

    private sealed class Fixture
    {
        public required GameplayService Service { get; init; }
        public required Game Game { get; init; }
        public required Player Red { get; init; }
        public required Player Blue { get; init; }
        public Player? Green { get; init; }
        public required Territory From { get; init; }
        public required Territory To { get; init; }
        public required AlternatingDice Dice { get; init; }
        public required FakeBonusCardRepository BonusCards { get; init; }
        public required FakeUnitOfWork UnitOfWork { get; init; }
    }

    /// <summary>
    /// Red is in the Attack phase with <paramref name="fromTroops"/> troops next to a blue territory
    /// holding <paramref name="toTroops"/>. The attacker always wins every die.
    /// </summary>
    private static Fixture Create(
        int fromTroops = 6, int toTroops = 1, bool blueOnlyHasTarget = false, bool threePlayers = false)
    {
        var game = new Game(Guid.NewGuid());
        var red = game.AddPlayer(Guid.NewGuid(), "red");
        var blue = game.AddPlayer(Guid.NewGuid(), "blue");
        var green = threePlayers ? game.AddPlayer(Guid.NewGuid(), "green") : null;
        game.Start(ClassicMap.Graph, new Random(1));

        var from = game.Territories.First(t => t.OwnerId == red.Id);
        game.DeployTroops(red.Id, from, red.ReinforcementTroops);
        from.TransferOwnership(red.Id, fromTroops);

        var to = game.Territories.First(t =>
            t.Identifier == ClassicMap.Graph.NeighborsOf(from.Identifier).First());
        to.TransferOwnership(blue.Id, toTroops);

        if (blueOnlyHasTarget)
        {
            foreach (var territory in game.Territories
                         .Where(t => t.OwnerId == blue.Id && t.Id != to.Id).ToList())
                territory.TransferOwnership((green ?? red).Id, 1);
        }

        var games = new FakeGameRepository();
        games.Add(game);
        var bonusCards = new FakeBonusCardRepository();
        var unitOfWork = new FakeUnitOfWork();
        var dice = new AlternatingDice(attackerValue: 6, defenderValue: 1);
        var currentUser = new FakeCurrentUser(red.UserId);
        var access = new GameAccess(games, currentUser);
        var fogOfWar = new FogOfWar(currentUser, new FakeMapRepository());
        var resolver = new ConquestResolver(
            bonusCards, new FakeMissionCardRepository(), new FakeMapRepository());
        var service = new GameplayService(
            access, new FakeMapRepository(), resolver, fogOfWar, unitOfWork, dice);

        return new Fixture
        {
            Service = service, Game = game, Red = red, Blue = blue, Green = green,
            From = from, To = to, Dice = dice, BonusCards = bonusCards, UnitOfWork = unitOfWork,
        };
    }

    private static List<BonusCard> GiveCards(Fixture f, Player owner, int count)
    {
        var cards = new List<BonusCard>();
        for (var i = 0; i < count; i++)
        {
            var card = new BonusCard(f.Game.Id, BonusCardType.Wild);
            card.AssignToPlayer(owner.Id);
            f.BonusCards.Add(card);
            cards.Add(card);
        }
        return cards;
    }

    private static async Task<int> HandSizeAsync(Fixture f, Player player) =>
        (await f.BonusCards.GetByPlayerIdAsync(f.Game.Id, player.Id)).Count;

    private static AttackDto Attack(Fixture f, int? moving, int attacking = 1, int defending = 1) =>
        new(f.Game.Id, Guid.Empty, f.From.Id, f.To.Id, attacking, defending, moving);

    // ---- AttackAsync ------------------------------------------------------

    [Fact]
    public async Task Attack_conquest_succeeds_while_the_defender_still_owns_other_territories()
    {
        var f = Create();

        var result = await f.Service.AttackAsync(Attack(f, moving: 2));

        Assert.True(result.TerritoryConquered);
        Assert.Equal(2, result.TroopsMoved);
        Assert.Equal(f.Red.Id, f.To.OwnerId);
        Assert.Equal(2, f.To.TroopCount);
        Assert.False(f.Blue.IsEliminated);
        Assert.False(f.Game.HasPendingConquest);
    }

    [Fact]
    public async Task Attack_conquest_without_moving_troops_becomes_pending()
    {
        var f = Create();

        var result = await f.Service.AttackAsync(Attack(f, moving: null));

        Assert.True(result.TerritoryConquered);
        Assert.Equal(0, result.TroopsMoved);
        Assert.True(f.Game.HasPendingConquest);
        Assert.Equal(f.Blue.Id, f.To.OwnerId);
        Assert.Equal(0, f.To.TroopCount);
    }

    [Theory]
    [InlineData(0)]   // below the number of attacking dice
    [InlineData(99)]  // more than the origin can spare
    public async Task Attack_rejects_an_invalid_moving_count_before_any_dice_are_rolled(int moving)
    {
        var f = Create();
        var toBefore = f.To.TroopCount;

        await Assert.ThrowsAsync<DomainValidationException>(() =>
            f.Service.AttackAsync(Attack(f, moving)));

        Assert.Equal(0, f.Dice.Calls);
        Assert.Equal(toBefore, f.To.TroopCount);
        Assert.Equal(0, f.UnitOfWork.SaveCount);
    }

    [Fact]
    public async Task A_new_attack_is_rejected_while_a_conquest_is_pending()
    {
        var f = Create();
        await f.Service.AttackAsync(Attack(f, moving: null));

        await Assert.ThrowsAsync<BusinessRuleException>(() =>
            f.Service.AttackAsync(Attack(f, moving: 1)));
    }

    [Fact]
    public async Task Conquering_the_last_territory_eliminates_the_defender_and_wins_the_game()
    {
        var f = Create(blueOnlyHasTarget: true);

        await f.Service.AttackAsync(Attack(f, moving: 2));

        Assert.True(f.Blue.IsEliminated);
        Assert.Equal(GameState.Finished, f.Game.State);
        Assert.Equal(f.Red.Id, f.Game.WinnerId);
    }

    // ---- BlitzAttackAsync -------------------------------------------------

    [Fact]
    public async Task Blitz_with_moving_troops_completes_the_conquest()
    {
        var f = Create(fromTroops: 6, toTroops: 2);

        var result = await f.Service.BlitzAttackAsync(
            new BlitzAttackDto(f.Game.Id, Guid.Empty, f.From.Id, f.To.Id, MovingTroopsCount: 3));

        Assert.True(result.TerritoryConquered);
        Assert.Equal(3, result.TroopsMoved);
        Assert.Equal(f.Red.Id, f.To.OwnerId);
        Assert.Equal(3, f.To.TroopCount);
        Assert.False(f.Game.HasPendingConquest);
        Assert.False(f.Blue.IsEliminated);
    }

    [Fact]
    public async Task Blitz_without_moving_troops_leaves_a_pending_conquest()
    {
        var f = Create(fromTroops: 6, toTroops: 2);

        var result = await f.Service.BlitzAttackAsync(
            new BlitzAttackDto(f.Game.Id, Guid.Empty, f.From.Id, f.To.Id));

        Assert.True(result.TerritoryConquered);
        Assert.Equal(0, result.TroopsMoved);
        Assert.True(f.Game.HasPendingConquest);
        Assert.Equal(f.Blue.Id, f.To.OwnerId);
    }

    // ---- CompleteConquestAsync -------------------------------------------

    [Fact]
    public async Task Complete_conquest_finishes_a_pending_conquest()
    {
        var f = Create(fromTroops: 6, toTroops: 2);
        await f.Service.BlitzAttackAsync(
            new BlitzAttackDto(f.Game.Id, Guid.Empty, f.From.Id, f.To.Id));

        await f.Service.CompleteConquestAsync(
            new CompleteConquestDto(f.Game.Id, f.From.Id, f.To.Id, MovingTroopsCount: 2));

        Assert.False(f.Game.HasPendingConquest);
        Assert.Equal(f.Red.Id, f.To.OwnerId);
        Assert.Equal(2, f.To.TroopCount);
        Assert.False(f.Blue.IsEliminated);
    }

    // ---- Cards taken over from an eliminated player ----------------------

    [Fact]
    public async Task Eliminating_a_player_that_gives_five_cards_sends_the_attacker_back_to_deployment()
    {
        var f = Create(blueOnlyHasTarget: true, threePlayers: true);
        GiveCards(f, f.Red, 2);
        GiveCards(f, f.Blue, 3);

        await f.Service.AttackAsync(Attack(f, moving: 2));

        Assert.True(f.Blue.IsEliminated);
        Assert.Equal(5, await HandSizeAsync(f, f.Red));
        Assert.Equal(GameState.InProgress, f.Game.State);
        Assert.Equal(GamePhase.Deployment, f.Game.CurrentPhase);
        Assert.Equal(f.Red.Id, f.Game.CurrentPlayerId);
    }

    [Fact]
    public async Task Eliminating_a_player_that_leaves_four_cards_keeps_the_attacker_in_the_attack_phase()
    {
        var f = Create(blueOnlyHasTarget: true, threePlayers: true);
        GiveCards(f, f.Red, 2);
        GiveCards(f, f.Blue, 2);

        await f.Service.AttackAsync(Attack(f, moving: 2));

        Assert.True(f.Blue.IsEliminated);
        Assert.Equal(4, await HandSizeAsync(f, f.Red));
        Assert.Equal(GamePhase.Attack, f.Game.CurrentPhase);
    }

    [Fact]
    public async Task A_big_hand_without_an_elimination_does_not_lock_the_attacker_out()
    {
        var f = Create();
        GiveCards(f, f.Red, 5);

        await f.Service.AttackAsync(Attack(f, moving: 2));

        Assert.False(f.Blue.IsEliminated);
        Assert.Equal(GamePhase.Attack, f.Game.CurrentPhase);
        f.Game.EndPhase(f.Red.Id, GamePhase.Fortify, ClassicMap.Graph, DateTimeOffset.UtcNow);
        Assert.Equal(GamePhase.Fortify, f.Game.CurrentPhase);
    }

    [Fact]
    public async Task After_an_elimination_the_attacker_must_trade_down_then_places_troops_freely_and_attacks_again()
    {
        var f = Create(blueOnlyHasTarget: true, threePlayers: true);
        var redCards = GiveCards(f, f.Red, 2);
        var blueCards = GiveCards(f, f.Blue, 3);
        await f.Service.AttackAsync(Attack(f, moving: 2));
        var guard = new ForcedCardTradeGuard(f.BonusCards);

        // Deployment with five cards is blocked until the hand is traded down.
        await Assert.ThrowsAsync<BusinessRuleException>(() =>
            guard.EnsureCompletedAsync(f.Game, f.Red.Id));

        var troops = f.Game.TradeBonusCards(f.Red.Id, [.. redCards, blueCards[0]]);
        await guard.EnsureCompletedAsync(f.Game, f.Red.Id);
        Assert.Equal(2, await HandSizeAsync(f, f.Red));
        Assert.True(troops > 0);

        // The new troops go wherever the player wants, and then the attack phase opens again.
        var anywhere = f.Game.Territories.First(t => t.OwnerId == f.Red.Id && t.Id != f.From.Id);
        f.Game.DeployTroops(f.Red.Id, anywhere, f.Red.ReinforcementTroops);

        Assert.Equal(GamePhase.Attack, f.Game.CurrentPhase);
        Assert.Equal(f.Red.Id, f.Game.CurrentPlayerId);
    }

    [Fact]
    public async Task Complete_conquest_without_a_pending_conquest_is_rejected()
    {
        var f = Create();

        await Assert.ThrowsAsync<BusinessRuleException>(() =>
            f.Service.CompleteConquestAsync(
                new CompleteConquestDto(f.Game.Id, f.From.Id, f.To.Id, MovingTroopsCount: 1)));
    }
}
