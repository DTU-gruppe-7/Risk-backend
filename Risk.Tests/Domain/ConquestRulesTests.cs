using Risk.Domain.Entities;
using Risk.Domain.Exceptions;
using Risk.Domain.Map;

namespace Risk.Tests.Domain;

/// <summary>Elimination after a conquest, pending conquests and their handling on timeout.</summary>
public class ConquestRulesTests
{
    private static readonly MapGraph Map = ClassicMap.Graph;

    /// <summary>A two-player game where red is in the Attack phase and a pending conquest of one blue territory exists.</summary>
    private sealed record Scenario(Game Game, Player Red, Player Blue, Territory From, Territory To);

    private static Scenario InAttackPhase(int turnTimeLimit = 0)
    {
        var game = new Game(Guid.NewGuid());
        var red = game.AddPlayer(Guid.NewGuid(), "red");
        var blue = game.AddPlayer(Guid.NewGuid(), "blue");
        if (turnTimeLimit > 0)
            game.ConfigureSettings(6, 0, turnTimeLimit, false, false);
        game.Start(Map, new Random(1));

        var from = game.Territories.First(t => t.OwnerId == red.Id);
        game.DeployTroops(red.Id, from, red.ReinforcementTroops);
        from.AddTroops(2);
        var to = game.Territories.First(t => t.OwnerId == blue.Id);
        return new Scenario(game, red, blue, from, to);
    }

    private static void Defeat(Territory territory) =>
        territory.RemoveTroops(territory.TroopCount);

    /// <summary>Hands every blue territory except <paramref name="keep"/> to red.</summary>
    private static void LeaveBlueWithOnly(Scenario s, Territory keep)
    {
        foreach (var territory in s.Game.Territories
                     .Where(t => t.OwnerId == s.Blue.Id && t.Id != keep.Id)
                     .ToList())
            territory.TransferOwnership(s.Red.Id, 1);
    }

    // ---- EliminateIfDefeated ----------------------------------------------

    [Fact]
    public void EliminateIfDefeated_keeps_a_player_who_still_owns_territories()
    {
        var s = InAttackPhase();

        var eliminated = s.Game.EliminateIfDefeated(s.Blue.Id);

        Assert.False(eliminated);
        Assert.False(s.Blue.IsEliminated);
    }

    [Fact]
    public void EliminateIfDefeated_eliminates_a_player_without_territories()
    {
        var s = InAttackPhase();
        foreach (var territory in s.Game.Territories.Where(t => t.OwnerId == s.Blue.Id).ToList())
            territory.TransferOwnership(s.Red.Id, 1);

        var eliminated = s.Game.EliminateIfDefeated(s.Blue.Id);

        Assert.True(eliminated);
        Assert.True(s.Blue.IsEliminated);
    }

    [Fact]
    public void EliminateIfDefeated_returns_false_for_an_already_eliminated_player()
    {
        var s = InAttackPhase();
        foreach (var territory in s.Game.Territories.Where(t => t.OwnerId == s.Blue.Id).ToList())
            territory.TransferOwnership(s.Red.Id, 1);
        s.Game.EliminateIfDefeated(s.Blue.Id);

        Assert.False(s.Game.EliminateIfDefeated(s.Blue.Id));
    }

    // ---- Pending conquest -------------------------------------------------

    [Fact]
    public void Beginning_a_pending_conquest_marks_the_game_as_pending()
    {
        var s = InAttackPhase();
        Assert.False(s.Game.HasPendingConquest);
        s.Game.EnsureNoPendingConquest();

        Defeat(s.To);
        s.Game.BeginPendingConquest(s.Red.Id, s.From, s.To);

        Assert.True(s.Game.HasPendingConquest);
        Assert.Throws<BusinessRuleException>(s.Game.EnsureNoPendingConquest);
    }

    [Fact]
    public void Completing_a_pending_conquest_moves_the_troops_and_clears_the_pending_state()
    {
        var s = InAttackPhase();
        var fromBefore = s.From.TroopCount;
        Defeat(s.To);
        s.Game.BeginPendingConquest(s.Red.Id, s.From, s.To);

        s.Game.CompletePendingConquest(s.Red.Id, s.From, s.To, 2);

        Assert.False(s.Game.HasPendingConquest);
        Assert.Equal(s.Red.Id, s.To.OwnerId);
        Assert.Equal(2, s.To.TroopCount);
        Assert.Equal(fromBefore - 2, s.From.TroopCount);
    }

    [Fact]
    public void A_conquest_cannot_be_completed_without_first_being_pending()
    {
        var s = InAttackPhase();
        Defeat(s.To);

        Assert.Throws<BusinessRuleException>(() =>
            s.Game.CompletePendingConquest(s.Red.Id, s.From, s.To, 1));
    }

    [Fact]
    public void A_pending_conquest_blocks_ending_the_phase()
    {
        var s = InAttackPhase();
        Defeat(s.To);
        s.Game.BeginPendingConquest(s.Red.Id, s.From, s.To);

        Assert.Throws<BusinessRuleException>(() =>
            s.Game.EndPhase(s.Red.Id, GamePhase.Fortify, Map, DateTimeOffset.UtcNow));
        Assert.Equal(GamePhase.Attack, s.Game.CurrentPhase);
    }

    [Fact]
    public void Ending_the_phase_works_again_once_the_conquest_is_completed()
    {
        var s = InAttackPhase();
        Defeat(s.To);
        s.Game.BeginPendingConquest(s.Red.Id, s.From, s.To);
        s.Game.CompletePendingConquest(s.Red.Id, s.From, s.To, 1);

        s.Game.EndPhase(s.Red.Id, GamePhase.Fortify, Map, DateTimeOffset.UtcNow);

        Assert.Equal(GamePhase.Fortify, s.Game.CurrentPhase);
    }

    // ---- Forced card trade ------------------------------------------------

    [Theory]
    [InlineData(4, false)]
    [InlineData(5, true)]
    [InlineData(9, true)]
    public void A_forced_card_trade_only_applies_in_the_deployment_phase(int handSize, bool blocked)
    {
        var game = new Game(Guid.NewGuid());
        game.AddPlayer(Guid.NewGuid(), "red");
        game.AddPlayer(Guid.NewGuid(), "blue");
        game.Start(Map, new Random(1));
        Assert.Equal(GamePhase.Deployment, game.CurrentPhase);

        if (blocked)
            Assert.Throws<BusinessRuleException>(() => game.EnsureNoForcedCardTrade(handSize));
        else
            game.EnsureNoForcedCardTrade(handSize);
    }

    [Fact]
    public void A_big_hand_never_blocks_the_attack_phase()
    {
        var s = InAttackPhase();

        s.Game.EnsureNoForcedCardTrade(9);
    }

    [Fact]
    public void The_attack_phase_can_return_to_deployment_for_a_card_trade()
    {
        var s = InAttackPhase();

        s.Game.ReturnToDeploymentForCardTrade();

        Assert.Equal(GamePhase.Deployment, s.Game.CurrentPhase);
        Assert.Equal(s.Red.Id, s.Game.CurrentPlayerId);
    }

    [Fact]
    public void Returning_to_deployment_is_only_possible_from_the_attack_phase()
    {
        var game = new Game(Guid.NewGuid());
        game.AddPlayer(Guid.NewGuid(), "red");
        game.AddPlayer(Guid.NewGuid(), "blue");
        game.Start(Map, new Random(1));

        Assert.Throws<BusinessRuleException>(game.ReturnToDeploymentForCardTrade);
    }

    [Fact]
    public void Returning_to_deployment_is_blocked_while_a_conquest_is_pending()
    {
        var s = InAttackPhase();
        Defeat(s.To);
        s.Game.BeginPendingConquest(s.Red.Id, s.From, s.To);

        Assert.Throws<BusinessRuleException>(s.Game.ReturnToDeploymentForCardTrade);
    }

    // ---- Timeout with a pending conquest ----------------------------------

    [Fact]
    public void A_pending_conquest_can_be_completed_with_one_troop_and_reports_who_is_involved()
    {
        var s = InAttackPhase(turnTimeLimit: 10);
        var fromBefore = s.From.TroopCount;
        Defeat(s.To);
        s.Game.BeginPendingConquest(s.Red.Id, s.From, s.To);

        var conquest = s.Game.CompletePendingConquestWithMinimumTroops();

        Assert.Equal((s.Red.Id, s.Blue.Id), conquest);
        Assert.False(s.Game.HasPendingConquest);
        Assert.Equal(s.Red.Id, s.To.OwnerId);
        Assert.Equal(1, s.To.TroopCount);
        Assert.Equal(fromBefore - 1, s.From.TroopCount);
    }

    [Fact]
    public void Completing_with_minimum_troops_does_nothing_when_no_conquest_is_pending()
    {
        var s = InAttackPhase();

        Assert.Null(s.Game.CompletePendingConquestWithMinimumTroops());
    }

    [Fact]
    public void A_timed_out_turn_cannot_advance_while_a_conquest_is_still_pending()
    {
        var s = InAttackPhase(turnTimeLimit: 10);
        Defeat(s.To);
        s.Game.BeginPendingConquest(s.Red.Id, s.From, s.To);

        Assert.Throws<BusinessRuleException>(() =>
            s.Game.AdvanceIfTurnTimedOut(Map, s.Game.TurnStartedAt!.Value.AddSeconds(10)));
    }

    [Fact]
    public void IsTurnTimedOut_follows_the_turn_time_limit()
    {
        var s = InAttackPhase(turnTimeLimit: 10);
        var started = s.Game.TurnStartedAt!.Value;

        Assert.False(s.Game.IsTurnTimedOut(started.AddSeconds(9)));
        Assert.True(s.Game.IsTurnTimedOut(started.AddSeconds(10)));
        Assert.False(InAttackPhase().Game.IsTurnTimedOut(started.AddDays(1)));
    }

    [Fact]
    public void Timeout_without_a_pending_conquest_behaves_as_before()
    {
        var s = InAttackPhase(turnTimeLimit: 10);
        var fromBefore = s.From.TroopCount;

        var advanced = s.Game.AdvanceIfTurnTimedOut(Map, s.Game.TurnStartedAt!.Value.AddSeconds(10));

        Assert.True(advanced);
        Assert.Equal(fromBefore, s.From.TroopCount);
        Assert.Equal(s.Blue.Id, s.Game.CurrentPlayerId);
    }
}
