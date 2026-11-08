using Risk.Domain.Entities;
using Risk.Domain.Exceptions;
using Risk.Domain.Map;

namespace Risk.Tests.Domain;

public class GameTests
{
    private static readonly Guid Host = Guid.NewGuid();
    private static readonly MapGraph Map = ClassicMap.Graph;

    [Fact]
    public void Start_distributes_playable_territories_and_the_classic_starting_troops()
    {
        var game = new Game(Host);
        var red = game.AddPlayer(Guid.NewGuid(), "red");
        var blue = game.AddPlayer(Guid.NewGuid(), "blue");

        game.Start(Map, new Random(1));

        Assert.Equal(Map.TerritoryIds, game.Territories.Select(t => t.Identifier));
        Assert.All(game.Territories.Where(t => !t.IsBlizzard), t =>
        {
            Assert.Equal(game.Id, t.GameId);
            Assert.NotNull(t.OwnerId);
            Assert.True(t.TroopCount >= 1);
        });
        Assert.Equal(21, game.Territories.Count(t => t.OwnerId == red.Id));
        Assert.Equal(21, game.Territories.Count(t => t.OwnerId == blue.Id));

        // Two players start with 40 troops each, already placed on their territories.
        Assert.Equal(40, game.Territories.Where(t => t.OwnerId == red.Id).Sum(t => t.TroopCount));
        Assert.Equal(40, game.Territories.Where(t => t.OwnerId == blue.Id).Sum(t => t.TroopCount));

        // Only the first player has reinforcements to place: territories / 3 (at least 3) plus continent bonus.
        var expected = Math.Max(3, 21 / 3) + game.BoardOn(Map).ContinentBonusFor(red.Id);
        Assert.Equal(expected, red.ReinforcementTroops);
        Assert.Equal(0, blue.ReinforcementTroops);
    }

    [Fact]
    public void Start_does_not_assign_blizzard_territories()
    {
        var game = new Game(Host);
        game.AddPlayer(Guid.NewGuid(), "red");
        game.AddPlayer(Guid.NewGuid(), "blue");
        game.ConfigureSettings(2, 0, 0, false, false, blizzardCount: 2);

        game.Start(Map, new Random(1));

        Assert.All(game.Territories.Where(t => t.IsBlizzard), t =>
        {
            Assert.Null(t.OwnerId);
            Assert.Equal(0, t.TroopCount);
        });
    }

    [Fact]
    public void DeployTroops_moves_troops_from_reserve_to_owned_territory()
    {
        var game = new Game(Host);
        var red = game.AddPlayer(Guid.NewGuid(), "red");
        game.AddPlayer(Guid.NewGuid(), "blue");
        game.Start(Map, new Random(1));
        var territory = game.Territories.First(t => t.OwnerId == red.Id);
        var reserveBefore = red.ReinforcementTroops;
        var troopsBefore = territory.TroopCount;

        game.DeployTroops(red.Id, territory, 3);

        Assert.Equal(reserveBefore - 3, red.ReinforcementTroops);
        Assert.Equal(troopsBefore + 3, territory.TroopCount);
    }

    [Fact]
    public void Deploying_the_last_reinforcement_automatically_starts_attack_phase()
    {
        var game = new Game(Host);
        var red = game.AddPlayer(Guid.NewGuid(), "red");
        game.AddPlayer(Guid.NewGuid(), "blue");
        game.Start(Map, new Random(1));
        var territory = game.Territories.First(t => t.OwnerId == red.Id);

        game.DeployTroops(red.Id, territory, red.ReinforcementTroops);

        Assert.Equal(GamePhase.Attack, game.CurrentPhase);
        Assert.Equal(0, red.ReinforcementTroops);
    }

    [Fact]
    public void DeployTroops_rejects_deployment_outside_deployment_phase()
    {
        var game = new Game(Host);
        var red = game.AddPlayer(Guid.NewGuid(), "red");
        game.AddPlayer(Guid.NewGuid(), "blue");
        game.Start(Map, new Random(1));
        var territory = game.Territories.First(t => t.OwnerId == red.Id);
        game.DeployTroops(red.Id, territory, red.ReinforcementTroops);

        Assert.Throws<BusinessRuleException>(() => game.DeployTroops(red.Id, territory, 1));
    }

    [Fact]
    public void DeployTroops_rejects_deployment_outside_current_players_turn()
    {
        var game = new Game(Host);
        var red = game.AddPlayer(Guid.NewGuid(), "red");
        var blue = game.AddPlayer(Guid.NewGuid(), "blue");
        game.Start(Map, new Random(1));
        var territory = game.Territories.First(t => t.OwnerId == blue.Id);

        Assert.Throws<BusinessRuleException>(() => game.DeployTroops(blue.Id, territory, 1));
    }

    [Fact]
    public void Fortify_moves_troops_and_starts_the_next_players_deployment_phase()
    {
        var game = new Game(Host);
        var red = game.AddPlayer(Guid.NewGuid(), "red");
        var blue = game.AddPlayer(Guid.NewGuid(), "blue");
        game.Start(Map, new Random(1));

        game.DeployTroops(red.Id, game.Territories.First(t => t.OwnerId == red.Id), red.ReinforcementTroops);
        game.EndPhase(red.Id, GamePhase.Fortify, Map, DateTimeOffset.UtcNow);

        var source = game.Territories
            .Where(t => t.OwnerId == red.Id)
            .First(t => Map.NeighborsOf(t.Identifier)
                .Select(id => game.Territories.FirstOrDefault(target => target.Identifier == id))
                .Any(target => target?.OwnerId == red.Id));
        var destination = Map.NeighborsOf(source.Identifier)
            .Select(id => game.Territories.First(target => target.Identifier == id))
            .First(t => t.OwnerId == red.Id);
        source.AddTroops(2);
        var sourceBefore = source.TroopCount;
        var destinationBefore = destination.TroopCount;

        game.Fortify(red.Id, source, destination, 2, Map, DateTimeOffset.UtcNow);

        Assert.Equal(sourceBefore - 2, source.TroopCount);
        Assert.Equal(destinationBefore + 2, destination.TroopCount);
        Assert.Equal(blue.Id, game.CurrentPlayerId);
        Assert.Equal(GamePhase.Deployment, game.CurrentPhase);
    }

    [Fact]
    public void Fortify_rejects_moving_all_troops_from_origin()
    {
        var game = new Game(Host);
        var red = game.AddPlayer(Guid.NewGuid(), "red");
        game.AddPlayer(Guid.NewGuid(), "blue");
        game.Start(Map, new Random(1));

        game.DeployTroops(red.Id, game.Territories.First(t => t.OwnerId == red.Id), red.ReinforcementTroops);
        game.EndPhase(red.Id, GamePhase.Fortify, Map, DateTimeOffset.UtcNow);

        var source = game.Territories.First(t => t.OwnerId == red.Id);
        var destination = game.Territories.First(t => t.OwnerId == red.Id && t.Id != source.Id);

        Assert.Throws<BusinessRuleException>(() =>
            game.Fortify(red.Id, source, destination, source.TroopCount, Map, DateTimeOffset.UtcNow));
    }

    [Fact]
    public void Ending_each_phase_advances_to_the_next_player()
    {
        var game = new Game(Host);
        var red = game.AddPlayer(Guid.NewGuid(), "red");
        game.AddPlayer(Guid.NewGuid(), "blue");
        game.Start(Map, new Random(1));
        var now = DateTimeOffset.UtcNow;
        var territory = game.Territories.First(t => t.OwnerId == red.Id);
        game.DeployTroops(red.Id, territory, red.ReinforcementTroops);

        Assert.Equal(GamePhase.Attack, game.CurrentPhase);
        game.EndPhase(red.Id, GamePhase.Fortify, Map, now);
        Assert.Equal(GamePhase.Fortify, game.CurrentPhase);
        game.EndPhase(red.Id, GamePhase.Deployment, Map, now);

        Assert.Equal(GamePhase.Deployment, game.CurrentPhase);
        Assert.NotEqual(red.Id, game.CurrentPlayerId);
        Assert.Equal(now, game.TurnStartedAt);
    }

    [Fact]
    public void Starting_a_new_turn_adds_territory_based_reinforcements()
    {
        var game = new Game(Host);
        var red = game.AddPlayer(Guid.NewGuid(), "red");
        var blue = game.AddPlayer(Guid.NewGuid(), "blue");
        game.Start(Map, new Random(1));

        var redTerritory = game.Territories.First(t => t.OwnerId == red.Id);
        game.DeployTroops(red.Id, redTerritory, red.ReinforcementTroops);
        game.EndPhase(red.Id, GamePhase.Fortify, Map, DateTimeOffset.UtcNow);
        game.EndPhase(red.Id, GamePhase.Deployment, Map, DateTimeOffset.UtcNow);

        var blueTerritories = game.Territories.Count(t => t.OwnerId == blue.Id);
        var expected = Math.Max(3, blueTerritories / 3) + game.BoardOn(Map).ContinentBonusFor(blue.Id);
        Assert.Equal(expected, blue.ReinforcementTroops);
    }

    [Fact]
    public void Deployment_cannot_end_while_reinforcements_remain()
    {
        var game = new Game(Host);
        var red = game.AddPlayer(Guid.NewGuid(), "red");
        game.AddPlayer(Guid.NewGuid(), "blue");
        game.Start(Map, new Random(1));

        Assert.Throws<BusinessRuleException>(() =>
            game.EndPhase(red.Id, GamePhase.Attack, Map, DateTimeOffset.UtcNow));
    }

    [Fact]
    public void A_timed_out_turn_advances_to_the_next_player()
    {
        var game = new Game(Host);
        var red = game.AddPlayer(Guid.NewGuid(), "red");
        game.AddPlayer(Guid.NewGuid(), "blue");
        game.ConfigureSettings(6, 0, 10, false, false);
        game.Start(Map, new Random(1));
        var turnStarted = game.TurnStartedAt!.Value;

        Assert.True(game.AdvanceIfTurnTimedOut(Map, turnStarted.AddSeconds(10)));
        Assert.Equal(GamePhase.Deployment, game.CurrentPhase);
        Assert.NotEqual(red.Id, game.CurrentPlayerId);
        Assert.False(game.AdvanceIfTurnTimedOut(Map, turnStarted.AddSeconds(10)));
    }

    [Fact]
    public void A_phase_can_only_be_ended_by_the_active_player_in_order()
    {
        var game = new Game(Host);
        var red = game.AddPlayer(Guid.NewGuid(), "red");
        var blue = game.AddPlayer(Guid.NewGuid(), "blue");
        game.Start(Map, new Random(1));

        Assert.Throws<BusinessRuleException>(() => game.EndPhase(blue.Id, GamePhase.Attack, Map, DateTimeOffset.UtcNow));
        Assert.Throws<BusinessRuleException>(() => game.EndPhase(red.Id, GamePhase.Fortify, Map, DateTimeOffset.UtcNow));
    }

    [Fact]
    public void Start_places_the_configured_number_of_blizzards()
    {
        var game = new Game(Host);
        game.AddPlayer(Guid.NewGuid(), "red");
        game.AddPlayer(Guid.NewGuid(), "blue");
        game.ConfigureSettings(6, 0, 0, false, false, blizzardCount: 5);

        game.Start(Map, new Random(1));

        Assert.Equal(5, game.Territories.Count(t => t.IsBlizzard));
    }

    [Fact]
    public void Start_rejects_more_blizzards_than_the_board_can_take()
    {
        var game = new Game(Host);
        game.AddPlayer(Guid.NewGuid(), "red");
        game.AddPlayer(Guid.NewGuid(), "blue");
        game.ConfigureSettings(6, 0, 0, false, false, blizzardCount: 42);

        Assert.Throws<BusinessRuleException>(() => game.Start(Map, new Random(1)));
        Assert.Equal(GameState.Lobby, game.State);
    }

    [Fact]
    public void Start_requires_a_map()
    {
        var empty = new MapGraph([], [], []);
        var game = new Game(Host);
        game.AddPlayer(Guid.NewGuid(), "red");
        game.AddPlayer(Guid.NewGuid(), "blue");

        Assert.Throws<DomainValidationException>(() => game.Start(empty, new Random(1)));
    }

    [Fact]
    public void New_game_starts_in_lobby_with_default_settings()
    {
        var game = new Game(Host);

        Assert.Equal(GameState.Lobby, game.State);
        Assert.NotNull(game.Settings);
        Assert.Equal(GameSetting.DefaultMaxPlayers, game.MaxPlayers);
    }

    [Fact]
    public void AddPlayer_assigns_increasing_turn_order()
    {
        var game = new Game(Host);

        var first = game.AddPlayer(Guid.NewGuid(), "Red");
        var second = game.AddPlayer(Guid.NewGuid(), "blue");

        Assert.Equal(1, first.TurnOrder);
        Assert.Equal(2, second.TurnOrder);
        Assert.Equal("red", first.Color);
    }

    [Fact]
    public void AddPlayer_after_someone_left_never_reuses_a_turn_order()
    {
        var game = new Game(Host);
        var first = game.AddPlayer(Guid.NewGuid(), "red");
        game.AddPlayer(Guid.NewGuid(), "blue");

        game.RemovePlayer(first.Id);
        var third = game.AddPlayer(Guid.NewGuid(), "green");

        Assert.Equal(3, third.TurnOrder);
    }

    [Fact]
    public void AddPlayer_rejects_the_same_user_twice()
    {
        var game = new Game(Host);
        var user = Guid.NewGuid();
        game.AddPlayer(user, "red");

        Assert.Throws<BusinessRuleException>(() => game.AddPlayer(user, "blue"));
    }

    [Fact]
    public void AddPlayer_rejects_when_full()
    {
        var game = new Game(Host);
        game.ConfigureSettings(maxPlayers: 2, timer: 0, turnTimeLimit: 0, hasFogOfWar: false, hasMissions: false);
        game.AddPlayer(Guid.NewGuid(), "red");
        game.AddPlayer(Guid.NewGuid(), "blue");

        Assert.Throws<BusinessRuleException>(() => game.AddPlayer(Guid.NewGuid(), "green"));
    }

    [Fact]
    public void Lobby_actions_are_rejected_after_start()
    {
        var game = new Game(Host);
        var player = game.AddPlayer(Guid.NewGuid(), "red");
        game.AddPlayer(Guid.NewGuid(), "blue");
        game.Start(Map, new Random(1));

        Assert.Throws<BusinessRuleException>(() => game.AddPlayer(Guid.NewGuid(), "blue"));
        Assert.Throws<BusinessRuleException>(() => game.RemovePlayer(player.Id));
        Assert.Throws<BusinessRuleException>(() => game.ConfigureSettings(4, 0, 0, false, false));
    }

    [Fact]
    public void ConfigureSettings_cannot_go_below_current_player_count()
    {
        var game = new Game(Host);
        game.AddPlayer(Guid.NewGuid(), "red");
        game.AddPlayer(Guid.NewGuid(), "blue");
        game.AddPlayer(Guid.NewGuid(), "green");

        Assert.Throws<BusinessRuleException>(() => game.ConfigureSettings(2, 0, 0, false, false));
    }

    [Theory]
    [InlineData(1)]
    [InlineData(7)]
    public void ConfigureSettings_validates_max_players(int maxPlayers)
    {
        var game = new Game(Host);

        Assert.Throws<DomainValidationException>(() => game.ConfigureSettings(maxPlayers, 0, 0, false, false));
    }

    [Fact]
    public void Start_and_End_follow_the_state_machine()
    {
        var game = new Game(Host);
        game.AddPlayer(Guid.NewGuid(), "red");
        game.AddPlayer(Guid.NewGuid(), "blue");

        Assert.Throws<BusinessRuleException>(game.End);
        game.Start(Map, new Random(1));
        Assert.Throws<BusinessRuleException>(() => game.Start(Map, new Random(1)));
        game.End();

        Assert.Equal(GameState.Finished, game.State);
        Assert.NotNull(game.StartedAt);
        Assert.NotNull(game.EndedAt);
    }

    [Fact]
    public void Phase_and_current_player_require_a_game_in_progress()
    {
        var game = new Game(Host);
        var player = game.AddPlayer(Guid.NewGuid(), "red");
        game.AddPlayer(Guid.NewGuid(), "blue");

        Assert.Throws<BusinessRuleException>(() =>
            game.EndPhase(player.Id, GamePhase.Fortify, Map, DateTimeOffset.UtcNow));

        game.Start(Map, new Random(1));
        var territory = game.Territories.First(t => t.OwnerId == player.Id);
        game.DeployTroops(player.Id, territory, player.ReinforcementTroops);

        Assert.Equal(GamePhase.Attack, game.CurrentPhase);
        Assert.Equal(player.Id, game.CurrentPlayerId);
    }

    [Fact]
    public void SetCurrentPlayer_rejects_eliminated_players_and_strangers()
    {
        var game = new Game(Host);
        var player = game.AddPlayer(Guid.NewGuid(), "red");
        var opponent = game.AddPlayer(Guid.NewGuid(), "blue");
        game.Start(Map, new Random(1));

        foreach (var territory in game.Territories.Where(t => t.OwnerId == player.Id).ToList())
            territory.TransferOwnership(opponent.Id, 1);

        game.EliminatePlayer(player.Id);

        Assert.Throws<BusinessRuleException>(() =>
            game.EndPhase(player.Id, GamePhase.Attack, Map, DateTimeOffset.UtcNow));
    }

    [Fact]
    public void FinishWithWinner_records_winner_and_blocks_further_actions()
    {
        var game = new Game(Host);
        var winner = game.AddPlayer(Guid.NewGuid(), "red");
        var opponent = game.AddPlayer(Guid.NewGuid(), "blue");
        game.Start(Map, new Random(1));

        foreach (var territory in game.Territories.Where(t => t.OwnerId == opponent.Id).ToList())
            territory.TransferOwnership(winner.Id, 1);

        game.FinishWithWinner(winner.Id);

        Assert.Equal(GameState.Finished, game.State);
        Assert.Equal(winner.Id, game.WinnerId);
        Assert.Throws<BusinessRuleException>(() =>
            game.DeployTroops(winner.Id, game.Territories.First(t => t.OwnerId == winner.Id), 1));
    }

    [Fact]
    public void Every_change_bumps_the_version()
    {
        var game = new Game(Host);
        var before = game.Version;

        var player = game.AddPlayer(Guid.NewGuid(), "red");
        game.AddPlayer(Guid.NewGuid(), "blue");
        game.Start(Map, new Random(1));

        Assert.Equal(before + 3, game.Version);
    }

    [Fact]
    public void HasParticipant_is_true_for_host_and_players_only()
    {
        var game = new Game(Host);
        var user = Guid.NewGuid();
        game.AddPlayer(user, "red");

        Assert.True(game.HasParticipant(Host));
        Assert.True(game.HasParticipant(user));
        Assert.False(game.HasParticipant(Guid.NewGuid()));
    }
}
