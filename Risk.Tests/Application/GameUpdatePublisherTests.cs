using Risk.Application.Common;
using Risk.Application.Exceptions;
using Risk.Application.Games;
using Risk.Domain.Entities;
using Risk.Domain.Map;

using Risk.Tests.Domain;

namespace Risk.Tests.Application;

public class GameUpdatePublisherTests
{
    private static readonly MapGraph Map = ClassicMap.Graph;

    private sealed record Setup(
        Game Game, Player Red, Player Blue, Guid RedUser, Guid BlueUser, Guid HostUser,
        GameUpdatePublisher Publisher, FakeGameRealtime Realtime);

    private static Setup Create(bool fog, bool hostPlays = false)
    {
        var host = Guid.NewGuid();
        var game = new Game(host);
        var redUser = hostPlays ? host : Guid.NewGuid();
        var blueUser = Guid.NewGuid();
        var red = game.AddPlayer(redUser, "red");
        var blue = game.AddPlayer(blueUser, "blue");
        game.ConfigureSettings(6, 0, 0, fog, false);
        game.StartInJoinOrder(Map);

        var games = new FakeGameRepository();
        games.Add(game);
        var realtime = new FakeGameRealtime();
        var publisher = new GameUpdatePublisher(games, new FakeMapRepository(), realtime);
        return new Setup(game, red, blue, redUser, blueUser, host, publisher, realtime);
    }

    [Fact]
    public async Task Without_fog_one_update_with_the_whole_board_goes_to_the_game()
    {
        var s = Create(fog: false);

        await s.Publisher.PublishAsync([s.Game.Id]);

        Assert.Empty(s.Realtime.ToUser);
        var (gameId, update) = Assert.Single(s.Realtime.ToGame);
        Assert.Equal(s.Game.Id, gameId);
        Assert.Equal(s.Game.Version, update.Game.Version);
        Assert.Equal(s.Game.Territories.Count, update.Territories.Count);
        Assert.Equal(
            s.Game.Territories.Select(t => t.Identifier).Order(),
            update.Territories.Select(t => t.Identifier));
    }

    [Fact]
    public async Task With_fog_every_participant_gets_their_own_filtered_update()
    {
        var s = Create(fog: true);

        await s.Publisher.PublishAsync([s.Game.Id]);

        Assert.Empty(s.Realtime.ToGame);
        Assert.Equal(
            new[] { s.RedUser, s.BlueUser, s.HostUser }.Order(),
            s.Realtime.ToUser.Select(u => u.UserId).Order());

        var red = s.Realtime.ToUser.Single(u => u.UserId == s.RedUser).Update;
        var visible = FogOfWar.VisibleTerritoriesFor(s.Game, Map, s.Red.Id)!;
        Assert.Equal(visible.Order(), red.Territories.Select(t => t.Identifier));
        Assert.True(red.Territories.Count < s.Game.Territories.Count);

        // Fog of war hides territories, not reinforcements: everyone sees every reserve (same as REST).
        Assert.True(s.Red.ReinforcementTroops > 0);
        Assert.Equal(s.Red.ReinforcementTroops, red.Game.Players.Single(p => p.Id == s.Red.Id).ReinforcementTroops);
        var blue = s.Realtime.ToUser.Single(u => u.UserId == s.BlueUser).Update;
        Assert.Equal(s.Red.ReinforcementTroops, blue.Game.Players.Single(p => p.Id == s.Red.Id).ReinforcementTroops);
    }

    [Fact]
    public async Task With_fog_a_host_who_is_not_playing_sees_no_territories()
    {
        var s = Create(fog: true);

        await s.Publisher.PublishAsync([s.Game.Id]);

        var host = s.Realtime.ToUser.Single(u => u.UserId == s.HostUser).Update;
        Assert.Empty(host.Territories);
        Assert.Equal(s.Red.ReinforcementTroops, host.Game.Players.Single(p => p.Id == s.Red.Id).ReinforcementTroops);
    }

    /// <summary>Red (first in turn) conquers a blue neighbour and has not moved troops in yet.</summary>
    private static (Territory From, Territory To) BeginConquest(Setup s)
    {
        var from = s.Game.Territories.First(t =>
            t.OwnerId == s.Red.Id &&
            Map.NeighborsOf(t.Identifier).Any(n => s.Game.Territories.Single(o => o.Identifier == n).OwnerId == s.Blue.Id));
        var to = s.Game.Territories.First(t =>
            t.OwnerId == s.Blue.Id && Map.NeighborsOf(from.Identifier).Contains(t.Identifier));
        s.Game.DeployTroops(s.Red.Id, from, s.Red.ReinforcementTroops);
        to.RemoveTroops(to.TroopCount);
        s.Game.BeginPendingConquest(s.Red.Id, from, to);
        return (from, to);
    }

    [Fact]
    public async Task Without_fog_the_pending_conquest_is_sent_to_everyone()
    {
        var s = Create(fog: false);
        var (from, to) = BeginConquest(s);

        await s.Publisher.PublishAsync([s.Game.Id]);

        var pending = Assert.Single(s.Realtime.ToGame).Update.Game.PendingConquest;
        Assert.Equal(new PendingConquestResponse(from.Id, to.Id, 1, from.TroopCount - 1), pending);
    }

    [Fact]
    public async Task With_fog_the_pending_conquest_only_goes_to_those_who_see_both_territories()
    {
        var s = Create(fog: true);
        var (from, to) = BeginConquest(s);

        await s.Publisher.PublishAsync([s.Game.Id]);

        PendingConquestResponse? For(Guid user) =>
            s.Realtime.ToUser.Single(u => u.UserId == user).Update.Game.PendingConquest;
        Assert.Equal(new PendingConquestResponse(from.Id, to.Id, 1, from.TroopCount - 1), For(s.RedUser));
        Assert.NotNull(For(s.BlueUser));   // the defender sees their lost territory and its attacker
        Assert.Null(For(s.HostUser));      // a host who only watches sees no territories under fog
    }

    [Fact]
    public async Task There_is_no_pending_conquest_when_nothing_waits()
    {
        var s = Create(fog: false);

        await s.Publisher.PublishAsync([s.Game.Id]);

        Assert.Null(Assert.Single(s.Realtime.ToGame).Update.Game.PendingConquest);
    }

    [Fact]
    public async Task A_host_who_plays_gets_one_update_not_two()
    {
        var s = Create(fog: true, hostPlays: true);

        await s.Publisher.PublishAsync([s.Game.Id]);

        Assert.Equal(2, s.Realtime.ToUser.Count);
        Assert.Single(s.Realtime.ToUser, u => u.UserId == s.HostUser);
    }

    [Fact]
    public async Task Unknown_games_are_skipped()
    {
        var s = Create(fog: false);

        await s.Publisher.PublishAsync([Guid.NewGuid()]);

        Assert.Empty(s.Realtime.ToGame);
        Assert.Empty(s.Realtime.ToUser);
    }

    [Fact]
    public async Task A_snapshot_is_filtered_for_the_viewer()
    {
        var s = Create(fog: true);

        var snapshot = await s.Publisher.SnapshotForAsync(s.Game.Id, s.BlueUser);

        var visible = FogOfWar.VisibleTerritoriesFor(s.Game, Map, s.Blue.Id)!;
        Assert.Equal(visible.Order(), snapshot.Territories.Select(t => t.Identifier));
        await Assert.ThrowsAsync<NotFoundException>(() => s.Publisher.SnapshotForAsync(Guid.NewGuid(), s.BlueUser));
    }

    [Fact]
    public async Task Only_participants_may_watch_a_game()
    {
        var s = Create(fog: false);
        var games = new FakeGameRepository();
        games.Add(s.Game);
        // The current user is irrelevant: hubs pass the user explicitly.
        var access = new GameAccess(games, new FakeCurrentUser(Guid.NewGuid()));

        Assert.Same(s.Game, await access.RequireParticipantAsync(s.Game.Id, s.RedUser));
        Assert.Same(s.Game, await access.RequireParticipantAsync(s.Game.Id, s.HostUser));
        await Assert.ThrowsAsync<ForbiddenException>(() => access.RequireParticipantAsync(s.Game.Id, Guid.NewGuid()));
    }
}
