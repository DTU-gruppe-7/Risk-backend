using Risk.Application.Common;
using Risk.Application.Exceptions;
using Risk.Domain.Entities;
using Risk.Domain.Exceptions;
using Risk.Domain.Map;

namespace Risk.Tests.Application;

public class FogOfWarTests
{
    private static readonly MapGraph Map = ClassicMap.Graph;

    private static (Game Game, Player Red, Player Blue, Guid RedUser, Guid BlueUser, Guid HostUser) Create(bool fog)
    {
        var host = Guid.NewGuid();
        var game = new Game(host);
        var redUser = Guid.NewGuid();
        var blueUser = Guid.NewGuid();
        var red = game.AddPlayer(redUser, "red");
        var blue = game.AddPlayer(blueUser, "blue");
        game.ConfigureSettings(6, 0, 0, fog, false);
        game.Start(Map, new Random(1));
        return (game, red, blue, redUser, blueUser, host);
    }

    private static FogOfWar For(Guid userId) => new(new FakeCurrentUser(userId), new FakeMapRepository());

    [Fact]
    public void Fog_is_only_active_when_the_game_enables_it()
    {
        Assert.True(For(Guid.NewGuid()).IsActive(Create(fog: true).Game));
        Assert.False(For(Guid.NewGuid()).IsActive(Create(fog: false).Game));
    }

    [Fact]
    public void The_viewer_is_the_players_own_player_and_a_non_playing_host_has_none()
    {
        var (game, red, _, redUser, _, host) = Create(fog: true);

        Assert.Equal(red.Id, For(redUser).ViewerPlayerId(game));
        Assert.Null(For(host).ViewerPlayerId(game));
    }

    [Fact]
    public async Task Without_fog_every_territory_is_visible()
    {
        var (game, _, _, redUser, _, _) = Create(fog: false);

        Assert.Null(await For(redUser).VisibleTerritoriesAsync(game));
    }

    [Fact]
    public async Task With_fog_a_player_sees_their_own_territories_but_not_the_whole_board()
    {
        var (game, red, _, redUser, _, _) = Create(fog: true);

        var visible = await For(redUser).VisibleTerritoriesAsync(game);

        Assert.NotNull(visible);
        Assert.All(game.Territories.Where(t => t.OwnerId == red.Id), t => Assert.Contains(t.Identifier, visible));
        Assert.True(visible.Count < game.Territories.Count);
    }

    [Fact]
    public async Task With_fog_a_host_who_is_not_playing_sees_nothing()
    {
        var (game, _, _, _, _, host) = Create(fog: true);

        await Assert.ThrowsAsync<ForbiddenException>(() => For(host).VisibleTerritoriesAsync(game));
    }

    [Fact]
    public void A_hidden_target_is_rejected_and_a_visible_one_is_allowed()
    {
        var (game, red, _, redUser, _, _) = Create(fog: true);
        var visible = game.BoardOn(Map).VisibleTo(red.Id);
        var hidden = game.Territories.First(t => !t.IsBlizzard && !visible.Contains(t.Identifier));
        var seen = game.Territories.First(t => visible.Contains(t.Identifier));
        var fog = For(redUser);

        Assert.Throws<BusinessRuleException>(() => fog.EnsureTargetVisible(game, Map, red.Id, hidden));
        fog.EnsureTargetVisible(game, Map, red.Id, seen);
    }

    [Fact]
    public void Without_fog_any_target_is_allowed()
    {
        var (game, red, _, redUser, _, _) = Create(fog: false);
        var visible = game.BoardOn(Map).VisibleTo(red.Id);
        var far = game.Territories.First(t => !t.IsBlizzard && !visible.Contains(t.Identifier));

        For(redUser).EnsureTargetVisible(game, Map, red.Id, far);
    }
}
