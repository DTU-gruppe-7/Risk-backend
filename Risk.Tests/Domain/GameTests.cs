using Risk.Domain.Entities;
using Risk.Domain.Exceptions;
using Risk.Domain.Map;

namespace Risk.Tests.Domain;

public class GameTests
{
    private static readonly Guid Host = Guid.NewGuid();
    private static readonly MapGraph Map = ClassicMap.Graph;

    [Fact]
    public void Start_distributes_playable_territories_and_one_troop_per_territory()
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
            Assert.Equal(1, t.TroopCount);
        });
        Assert.Equal(21, game.Territories.Count(t => t.OwnerId == red.Id));
        Assert.Equal(21, game.Territories.Count(t => t.OwnerId == blue.Id));
        Assert.Equal(19, red.ReinforcementTroops);
        Assert.Equal(19, blue.ReinforcementTroops);
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

        game.DeployTroops(red.Id, territory, 3);

        Assert.Equal(16, red.ReinforcementTroops);
        Assert.Equal(4, territory.TroopCount);
    }

    [Fact]
    public void DeployTroops_rejects_deployment_outside_deployment_phase()
    {
        var game = new Game(Host);
        var red = game.AddPlayer(Guid.NewGuid(), "red");
        game.AddPlayer(Guid.NewGuid(), "blue");
        game.Start(Map, new Random(1));
        game.SetPhase(GamePhase.Attack);
        var territory = game.Territories.First(t => t.OwnerId == red.Id);

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

        Assert.Throws<BusinessRuleException>(() => game.SetPhase(GamePhase.Attack));
        Assert.Throws<BusinessRuleException>(() => game.SetCurrentPlayer(player.Id));

        game.Start(Map, new Random(1));
        game.SetPhase(GamePhase.Attack);
        game.SetCurrentPlayer(player.Id);

        Assert.Equal(GamePhase.Attack, game.CurrentPhase);
        Assert.Equal(player.Id, game.CurrentPlayerId);
    }

    [Fact]
    public void SetCurrentPlayer_rejects_eliminated_players_and_strangers()
    {
        var game = new Game(Host);
        var player = game.AddPlayer(Guid.NewGuid(), "red");
        game.AddPlayer(Guid.NewGuid(), "blue");
        game.Start(Map, new Random(1));
        game.EliminatePlayer(player.Id);

        Assert.Throws<BusinessRuleException>(() => game.SetCurrentPlayer(player.Id));
        Assert.Throws<BusinessRuleException>(() => game.SetCurrentPlayer(Guid.NewGuid()));
    }

    [Fact]
    public void Every_change_bumps_the_version()
    {
        var game = new Game(Host);
        var before = game.Version;

        var player = game.AddPlayer(Guid.NewGuid(), "red");
        game.AddPlayer(Guid.NewGuid(), "blue");
        game.ReinforcePlayer(player.Id, 3);
        game.Start(Map, new Random(1));

        Assert.Equal(before + 4, game.Version);
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
