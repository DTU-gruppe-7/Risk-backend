using Risk.Application.Services;
using Risk.Domain.Entities;
using Risk.Domain.Map;
using Risk.Tests.Domain;

namespace Risk.Tests.Application;

public class MissionVictoryTests
{
    private static readonly MapGraph Map = ClassicMap.Graph;

    private sealed record Setup(
        Game Game, Player Red, Player Blue, Player Green, ConquestResolver Resolver, MissionCard RedMission);

    /// <summary>Three players with missions on. Red's mission is to eliminate Blue.</summary>
    private static Setup Create()
    {
        var game = new Game(Guid.NewGuid());
        var red = game.AddPlayer(Guid.NewGuid(), "red");
        var blue = game.AddPlayer(Guid.NewGuid(), "blue");
        var green = game.AddPlayer(Guid.NewGuid(), "green");
        game.ConfigureSettings(6, 0, 0, hasFogOfWar: false, hasMissions: true);
        game.Start(Map, new Random(1));

        var missions = new FakeMissionCardRepository();
        var mission = new MissionCard(game.Id, "Eliminate blue", "Eliminate blue.",
            MissionObjectiveType.EliminatePlayer, targetPlayerId: blue.Id);
        mission.AssignToPlayer(red.Id);
        missions.Add(mission);

        var resolver = new ConquestResolver(new FakeBonusCardRepository(), missions, new FakeMapRepository());
        return new Setup(game, red, blue, green, resolver, mission);
    }

    /// <summary>Red, in its Attack phase, takes Blue's last territory (Blue's others go to Green).</summary>
    private static Task EliminateBlueAsync(Setup s)
    {
        var redTerritory = s.Game.Territories.First(t => t.OwnerId == s.Red.Id);
        s.Game.DeployTroops(s.Red.Id, redTerritory, s.Red.ReinforcementTroops);

        var blueTerritories = s.Game.Territories.Where(t => t.OwnerId == s.Blue.Id).ToList();
        foreach (var territory in blueTerritories.Skip(1))
            territory.TransferOwnership(s.Green.Id, 1);
        blueTerritories[0].TransferOwnership(s.Red.Id, 1);

        return s.Resolver.ResolveAsync(s.Game, s.Red.Id, s.Blue.Id);
    }

    [Fact]
    public async Task A_completed_mission_does_not_win_in_the_first_round()
    {
        var s = Create();

        await EliminateBlueAsync(s);

        Assert.True(s.Blue.IsEliminated);
        Assert.Equal(GameState.InProgress, s.Game.State);
        Assert.Null(s.Game.WinnerId);
        Assert.False(s.RedMission.IsCompleted);
    }

    [Fact]
    public async Task A_completed_mission_wins_once_every_player_has_had_a_turn()
    {
        var s = Create();
        s.Game.PlayTurn(s.Red);
        s.Game.PlayTurn(s.Blue);
        s.Game.PlayTurn(s.Green);
        Assert.Equal(2, s.Game.Round);

        await EliminateBlueAsync(s);

        Assert.Equal(GameState.Finished, s.Game.State);
        Assert.Equal(s.Red.Id, s.Game.WinnerId);
        Assert.True(s.RedMission.IsCompleted);
    }

    [Fact]
    public async Task Taking_the_whole_board_wins_even_with_missions_in_the_first_round()
    {
        var s = Create();
        var redTerritory = s.Game.Territories.First(t => t.OwnerId == s.Red.Id);
        s.Game.DeployTroops(s.Red.Id, redTerritory, s.Red.ReinforcementTroops);
        foreach (var territory in s.Game.Territories.Where(t => t.OwnerId != s.Red.Id && !t.IsBlizzard).ToList())
            territory.TransferOwnership(s.Red.Id, 1);

        await s.Resolver.ResolveAsync(s.Game, s.Red.Id, s.Blue.Id);

        Assert.Equal(GameState.Finished, s.Game.State);
        Assert.Equal(s.Red.Id, s.Game.WinnerId);
    }
}
