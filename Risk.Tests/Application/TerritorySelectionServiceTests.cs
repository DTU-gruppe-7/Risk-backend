using Risk.Application.Common;
using Risk.Application.Exceptions;
using Risk.Application.Games;
using Risk.Domain.Entities;
using Risk.Domain.Exceptions;
using Risk.Domain.Map;

using Risk.Tests.Domain;

namespace Risk.Tests.Application;

public class TerritorySelectionServiceTests
{
    private static readonly MapGraph Map = ClassicMap.Graph;

    private sealed record Setup(
        Game Game, Player Current, Player Waiting, Player Third, Guid HostUser,
        TerritorySelectionService Service, FakeGameRealtime Realtime, FakeGameRepository Games);

    private static Setup Create(bool fog)
    {
        var host = Guid.NewGuid();
        var game = new Game(host);
        game.AddPlayer(Guid.NewGuid(), "red");
        game.AddPlayer(Guid.NewGuid(), "blue");
        game.AddPlayer(Guid.NewGuid(), "green");
        game.ConfigureSettings(6, 0, 0, fog, false);
        game.StartInJoinOrder(Map);

        var current = game.FindPlayer(game.CurrentPlayerId!.Value)!;
        var others = game.Players.Where(p => p.Id != current.Id).ToList();

        var games = new FakeGameRepository();
        games.Add(game);
        var realtime = new FakeGameRealtime();
        // The current user is irrelevant: hubs pass the user explicitly.
        var access = new GameAccess(games, new FakeCurrentUser(Guid.NewGuid()));
        var service = new TerritorySelectionService(access, new FakeMapRepository(), realtime);
        return new Setup(game, current, others[0], others[1], host, service, realtime, games);
    }

    private static Territory OwnedBy(Game game, Player player) =>
        game.Territories.First(t => t.OwnerId == player.Id);

    [Fact]
    public async Task The_current_players_selection_goes_to_everyone_else_but_not_the_caller()
    {
        var s = Create(fog: false);
        var source = OwnedBy(s.Game, s.Current);
        var target = OwnedBy(s.Game, s.Waiting);

        await s.Service.SelectAsync(s.Game.Id, s.Current.UserId, new SelectTerritoriesRequest(source.Id, target.Id));

        Assert.Equal(
            new[] { s.Waiting.UserId, s.Third.UserId, s.HostUser }.Order(),
            s.Realtime.Selections.Select(m => m.UserId).Order());
        Assert.All(s.Realtime.Selections, m =>
        {
            Assert.Equal(s.Game.Id, m.GameId);
            Assert.Equal(new TerritorySelectionDto(s.Game.Id, s.Current.Id, source.Id, target.Id), m.Selection);
        });
    }

    [Fact]
    public async Task Both_ids_null_is_relayed_as_a_clear()
    {
        var s = Create(fog: false);

        await s.Service.SelectAsync(s.Game.Id, s.Current.UserId, new SelectTerritoriesRequest(null, null));

        Assert.Equal(3, s.Realtime.Selections.Count);
        Assert.All(s.Realtime.Selections, m =>
            Assert.Equal(new TerritorySelectionDto(s.Game.Id, s.Current.Id, null, null), m.Selection));
    }

    [Fact]
    public async Task A_player_whose_turn_it_is_not_is_refused_and_nothing_is_sent()
    {
        var s = Create(fog: false);

        await Assert.ThrowsAsync<BusinessRuleException>(() => s.Service.SelectAsync(
            s.Game.Id, s.Waiting.UserId, new SelectTerritoriesRequest(OwnedBy(s.Game, s.Waiting).Id, null)));

        Assert.Empty(s.Realtime.Selections);
    }

    [Fact]
    public async Task Users_who_are_not_players_are_refused()
    {
        var s = Create(fog: false);

        await Assert.ThrowsAsync<ForbiddenException>(() => s.Service.SelectAsync(
            s.Game.Id, Guid.NewGuid(), new SelectTerritoriesRequest(null, null)));
        // The host watches but does not play, so has no turn to select in.
        await Assert.ThrowsAsync<ForbiddenException>(() => s.Service.SelectAsync(
            s.Game.Id, s.HostUser, new SelectTerritoriesRequest(null, null)));

        Assert.Empty(s.Realtime.Selections);
    }

    [Fact]
    public async Task A_game_that_is_not_in_progress_is_refused()
    {
        var host = Guid.NewGuid();
        var lobby = new Game(host);
        var player = lobby.AddPlayer(Guid.NewGuid(), "red");
        var games = new FakeGameRepository();
        games.Add(lobby);
        var realtime = new FakeGameRealtime();
        var service = new TerritorySelectionService(
            new GameAccess(games, new FakeCurrentUser(Guid.NewGuid())), new FakeMapRepository(), realtime);

        await Assert.ThrowsAsync<BusinessRuleException>(() => service.SelectAsync(
            lobby.Id, player.UserId, new SelectTerritoriesRequest(null, null)));
        await Assert.ThrowsAsync<NotFoundException>(() => service.SelectAsync(
            Guid.NewGuid(), player.UserId, new SelectTerritoriesRequest(null, null)));
        Assert.Empty(realtime.Selections);
    }

    [Fact]
    public async Task Territory_ids_from_another_game_are_refused()
    {
        var s = Create(fog: false);
        var other = Create(fog: false);
        var foreignId = other.Game.Territories.First().Id;

        await Assert.ThrowsAsync<ValidationFailedException>(() => s.Service.SelectAsync(
            s.Game.Id, s.Current.UserId, new SelectTerritoriesRequest(foreignId, null)));
        await Assert.ThrowsAsync<ValidationFailedException>(() => s.Service.SelectAsync(
            s.Game.Id, s.Current.UserId, new SelectTerritoriesRequest(OwnedBy(s.Game, s.Current).Id, foreignId)));

        Assert.Empty(s.Realtime.Selections);
    }

    [Fact]
    public async Task With_fog_each_receiver_only_gets_the_territories_they_can_see()
    {
        var s = Create(fog: true);
        var waitingSees = FogOfWar.VisibleTerritoriesFor(s.Game, Map, s.Waiting.Id)!;
        var thirdSees = FogOfWar.VisibleTerritoriesFor(s.Game, Map, s.Third.Id)!;

        // A target hidden from one waiting player but visible to the other.
        var target = s.Game.Territories.First(t =>
            !waitingSees.Contains(t.Identifier) && thirdSees.Contains(t.Identifier));
        var source = OwnedBy(s.Game, s.Current);

        await s.Service.SelectAsync(s.Game.Id, s.Current.UserId, new SelectTerritoriesRequest(source.Id, target.Id));

        TerritorySelectionDto To(Guid userId) => s.Realtime.Selections.Single(m => m.UserId == userId).Selection;

        Assert.Null(To(s.Waiting.UserId).TargetTerritoryId);
        Assert.Equal(target.Id, To(s.Third.UserId).TargetTerritoryId);
        Assert.Equal(waitingSees.Contains(source.Identifier) ? source.Id : null, To(s.Waiting.UserId).SourceTerritoryId);
        Assert.Equal(s.Current.Id, To(s.Waiting.UserId).PlayerId);

        // A host who is not playing sees no territories under fog, as in GameUpdated.
        Assert.Equal(new TerritorySelectionDto(s.Game.Id, s.Current.Id, null, null), To(s.HostUser));
    }

    [Fact]
    public async Task Nothing_is_saved_and_the_version_does_not_change()
    {
        var s = Create(fog: false);
        var version = s.Game.Version;
        var troops = s.Game.Territories.ToDictionary(t => t.Id, t => (t.OwnerId, t.TroopCount));

        await s.Service.SelectAsync(s.Game.Id, s.Current.UserId,
            new SelectTerritoriesRequest(OwnedBy(s.Game, s.Current).Id, OwnedBy(s.Game, s.Waiting).Id));

        Assert.Equal(version, s.Game.Version);
        Assert.Equal(troops, s.Game.Territories.ToDictionary(t => t.Id, t => (t.OwnerId, t.TroopCount)));
    }
}
