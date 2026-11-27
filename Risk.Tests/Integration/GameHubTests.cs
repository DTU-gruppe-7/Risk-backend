using System.Net.Http.Json;
using System.Text.Json;
using System.Threading.Channels;
using Microsoft.AspNetCore.SignalR;
using Microsoft.AspNetCore.SignalR.Client;

namespace Risk.Tests.Integration;

[Collection(ApiCollection.Name)]
public sealed class GameHubTests(ApiFixture api)
{
    private static readonly TimeSpan Timeout = TimeSpan.FromSeconds(10);

    [Fact]
    public async Task Participants_receive_the_current_state_on_join_and_every_change_after()
    {
        var red = await api.RegisterAsync("red");
        var blue = await api.RegisterAsync("blue");
        var gameId = await CreateGameAsync(red, fog: false, blue);

        await using var redHub = await api.ConnectGameHubAsync(red);
        await using var blueHub = await api.ConnectGameHubAsync(blue);
        var redUpdates = Collect(redHub);
        var blueUpdates = Collect(blueHub);

        await redHub.InvokeAsync("JoinGame", gameId);
        await blueHub.InvokeAsync("JoinGame", gameId);
        var joined = await NextAsync(blueUpdates, u => u.Game("state") == "Lobby");
        await NextAsync(redUpdates, u => u.Game("state") == "Lobby");

        (await red.Http.PutAsync($"/api/games/{gameId}/start", null)).EnsureSuccessStatusCode();

        foreach (var updates in new[] { redUpdates, blueUpdates })
        {
            var started = await NextAsync(updates, u => u.Game("state") == "InProgress");
            Assert.True(started.Version > joined.Version);
            Assert.Equal(42, started.Territories.GetArrayLength());
            Assert.Equal(red.UserName, UserNameOf(started.Players, red.Id));
            Assert.Equal(blue.UserName, UserNameOf(started.Players, blue.Id));
        }

        var rest = await blue.Http.GetFromJsonAsync<JsonElement>($"/api/games/{gameId}");
        Assert.Equal(red.UserName, UserNameOf(rest.GetProperty("players"), red.Id));
    }

    [Fact]
    public async Task With_fog_of_war_each_player_only_receives_what_they_can_see()
    {
        var red = await api.RegisterAsync("red");
        var blue = await api.RegisterAsync("blue");
        var gameId = await CreateGameAsync(red, fog: true, blue);

        await using var redHub = await api.ConnectGameHubAsync(red);
        var redUpdates = Collect(redHub);
        await redHub.InvokeAsync("JoinGame", gameId);

        (await red.Http.PutAsync($"/api/games/{gameId}/start", null)).EnsureSuccessStatusCode();
        var started = await NextAsync(redUpdates, u => u.Game("state") == "InProgress");

        // Exactly what the REST endpoint returns for the same player.
        var rest = await red.Http.GetFromJsonAsync<JsonElement>($"/api/games/{gameId}/territories");
        Assert.Equal(Identifiers(rest), Identifiers(started.Territories));

        // Fog of war: exactly red's own territories and their neighbours. (Not "fewer than 42":
        // with two players those neighbours can cover the whole board.)
        var redPlayerId = PlayerIdOf(started, red.Id);
        var map = await red.Http.GetFromJsonAsync<JsonElement>("/api/map");
        var neighbours = map.GetProperty("territories").EnumerateArray().ToDictionary(
            t => t.GetProperty("id").GetString()!,
            t => t.GetProperty("neighborIds").EnumerateArray().Select(n => n.GetString()!).ToList());
        var owned = started.Territories.EnumerateArray()
            .Where(t => t.GetProperty("ownerId").GetString() == redPlayerId)
            .Select(t => t.GetProperty("identifier").GetString()!)
            .ToList();
        Assert.NotEmpty(owned);
        Assert.Equal(
            owned.Concat(owned.SelectMany(id => neighbours[id])).Distinct().Order().ToList(),
            Identifiers(started.Territories));

        // The turn order is random, so only the player whose turn it is holds reinforcements.
        var currentPlayerId = started.Game("currentPlayerId");
        Assert.All(started.Players.EnumerateArray().Where(p => p.GetProperty("id").GetString() != currentPlayerId),
            p => Assert.Equal(0, p.GetProperty("reinforcementTroops").GetInt32()));
    }

    [Fact]
    public async Task Users_outside_the_game_cannot_watch_it()
    {
        var red = await api.RegisterAsync("red");
        var outsider = await api.RegisterAsync("outsider");
        var gameId = await CreateGameAsync(red, fog: false);

        await using var hub = await api.ConnectGameHubAsync(outsider);

        var error = await Assert.ThrowsAsync<HubException>(() => hub.InvokeAsync("JoinGame", gameId));
        Assert.Contains("not part of this game", error.Message);
    }

    [Fact]
    public async Task The_current_players_selection_is_relayed_live_to_the_others_only()
    {
        var red = await api.RegisterAsync("red");
        var blue = await api.RegisterAsync("blue");
        var gameId = await CreateGameAsync(red, fog: false, blue);
        var turn = await StartAsync(gameId, red, blue);

        await using var currentHub = await api.ConnectGameHubAsync(turn.Current);
        await using var waitingHub = await api.ConnectGameHubAsync(turn.Waiting);
        var currentSelections = CollectSelections(currentHub);
        var waitingSelections = CollectSelections(waitingHub);
        await currentHub.InvokeAsync("JoinGame", gameId);
        await waitingHub.InvokeAsync("JoinGame", gameId);
        var versionBefore = await VersionAsync(turn.Current, gameId);

        var source = turn.TerritoryOwnedBy(turn.CurrentPlayerId);
        var target = turn.TerritoryOwnedBy(turn.WaitingPlayerId);
        await currentHub.InvokeAsync("SelectTerritories", gameId,
            new { sourceTerritoryId = source, targetTerritoryId = target });

        var selected = await NextAsync(waitingSelections, _ => true);
        Assert.Equal(gameId, selected.GetProperty("gameId").GetGuid());
        Assert.Equal(turn.CurrentPlayerId, selected.GetProperty("playerId").GetGuid());
        Assert.Equal(source, selected.GetProperty("sourceTerritoryId").GetGuid());
        Assert.Equal(target, selected.GetProperty("targetTerritoryId").GetGuid());

        // Both null clears the selection; the nulls are on the wire, not left out.
        await currentHub.InvokeAsync("SelectTerritories", gameId,
            new { sourceTerritoryId = (Guid?)null, targetTerritoryId = (Guid?)null });
        var cleared = await NextAsync(waitingSelections, _ => true);
        Assert.Equal(JsonValueKind.Null, cleared.GetProperty("sourceTerritoryId").ValueKind);
        Assert.Equal(JsonValueKind.Null, cleared.GetProperty("targetTerritoryId").ValueKind);

        // The caller never receives their own selection, and nothing was saved.
        await Task.Delay(TimeSpan.FromMilliseconds(300));
        Assert.False(currentSelections.TryRead(out _));
        Assert.Equal(versionBefore, await VersionAsync(turn.Current, gameId));
    }

    [Fact]
    public async Task Only_the_current_player_may_select_and_only_this_games_territories()
    {
        var red = await api.RegisterAsync("red");
        var blue = await api.RegisterAsync("blue");
        var outsider = await api.RegisterAsync("outsider");
        var gameId = await CreateGameAsync(red, fog: false, blue);
        var turn = await StartAsync(gameId, red, blue);

        var otherGameId = await CreateGameAsync(outsider, fog: false, await api.RegisterAsync("other"));
        (await outsider.Http.PutAsync($"/api/games/{otherGameId}/start", null)).EnsureSuccessStatusCode();
        var foreignTerritory = (await outsider.Http.GetFromJsonAsync<JsonElement>($"/api/games/{otherGameId}/territories"))
            .EnumerateArray().First().GetProperty("id").GetGuid();

        await using var currentHub = await api.ConnectGameHubAsync(turn.Current);
        await using var waitingHub = await api.ConnectGameHubAsync(turn.Waiting);
        await using var outsiderHub = await api.ConnectGameHubAsync(outsider);
        var waitingSelections = CollectSelections(waitingHub);
        await waitingHub.InvokeAsync("JoinGame", gameId);

        var notYourTurn = await Assert.ThrowsAsync<HubException>(() => waitingHub.InvokeAsync(
            "SelectTerritories", gameId, new { sourceTerritoryId = (Guid?)null, targetTerritoryId = (Guid?)null }));
        Assert.Contains("not this player's turn", notYourTurn.Message);

        var notInGame = await Assert.ThrowsAsync<HubException>(() => outsiderHub.InvokeAsync(
            "SelectTerritories", gameId, new { sourceTerritoryId = (Guid?)null, targetTerritoryId = (Guid?)null }));
        Assert.Contains("not a player in this game", notInGame.Message);

        await Assert.ThrowsAsync<HubException>(() => currentHub.InvokeAsync(
            "SelectTerritories", gameId, new { sourceTerritoryId = foreignTerritory, targetTerritoryId = (Guid?)null }));

        await Task.Delay(TimeSpan.FromMilliseconds(300));
        Assert.False(waitingSelections.TryRead(out _));
    }

    // -------------------------------------------------------------------------

    private sealed record Turn(
        TestUser Current, TestUser Waiting, Guid CurrentPlayerId, Guid WaitingPlayerId, JsonElement Territories)
    {
        public Guid TerritoryOwnedBy(Guid playerId) =>
            Territories.EnumerateArray()
                .First(t => t.GetProperty("ownerId").GetString() == playerId.ToString())
                .GetProperty("id").GetGuid();
    }

    /// <summary>Starts a two-player game and works out whose turn it is.</summary>
    private static async Task<Turn> StartAsync(Guid gameId, TestUser host, TestUser other)
    {
        (await host.Http.PutAsync($"/api/games/{gameId}/start", null)).EnsureSuccessStatusCode();
        var game = await host.Http.GetFromJsonAsync<JsonElement>($"/api/games/{gameId}");
        var currentPlayerId = game.GetProperty("currentPlayerId").GetGuid();
        var players = game.GetProperty("players").EnumerateArray().ToList();
        var currentUserId = players.Single(p => p.GetProperty("id").GetGuid() == currentPlayerId)
            .GetProperty("userId").GetGuid();
        var (current, waiting) = currentUserId == host.Id ? (host, other) : (other, host);
        var waitingPlayerId = players.Single(p => p.GetProperty("userId").GetGuid() == waiting.Id)
            .GetProperty("id").GetGuid();
        var territories = await host.Http.GetFromJsonAsync<JsonElement>($"/api/games/{gameId}/territories");
        return new Turn(current, waiting, currentPlayerId, waitingPlayerId, territories);
    }

    private static async Task<long> VersionAsync(TestUser user, Guid gameId) =>
        (await user.Http.GetFromJsonAsync<JsonElement>($"/api/games/{gameId}")).GetProperty("version").GetInt64();

    private static ChannelReader<JsonElement> CollectSelections(HubConnection hub)
    {
        var channel = Channel.CreateUnbounded<JsonElement>();
        hub.On<JsonElement>("SelectionChanged", body => channel.Writer.TryWrite(body));
        return channel.Reader;
    }

    private static async Task<Guid> CreateGameAsync(TestUser host, bool fog, params TestUser[] others)
    {
        var created = await host.Http.PostAsJsonAsync("/api/games", new { hasFogOfWar = fog });
        created.EnsureSuccessStatusCode();
        var gameId = (await created.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("id").GetGuid();

        var colors = new Queue<string>(["red", "blue", "green", "yellow"]);
        foreach (var user in others.Prepend(host))
            (await user.Http.PostAsJsonAsync($"/api/games/{gameId}/players", new { color = colors.Dequeue() }))
                .EnsureSuccessStatusCode();
        return gameId;
    }

    private sealed record Update(JsonElement Body)
    {
        public JsonElement Players => Body.GetProperty("game").GetProperty("players");
        public JsonElement Territories => Body.GetProperty("territories");
        public long Version => Body.GetProperty("game").GetProperty("version").GetInt64();
        public string? Game(string property) => Body.GetProperty("game").GetProperty(property).GetString();
    }

    private static ChannelReader<Update> Collect(HubConnection hub)
    {
        var channel = Channel.CreateUnbounded<Update>();
        hub.On<JsonElement>("GameUpdated", body => channel.Writer.TryWrite(new Update(body)));
        return channel.Reader;
    }

    /// <summary>The next update matching the predicate. Earlier ones are skipped, later ones are kept.</summary>
    private static async Task<T> NextAsync<T>(ChannelReader<T> updates, Func<T, bool> match)
    {
        using var timeout = new CancellationTokenSource(Timeout);
        while (true)
        {
            var update = await updates.ReadAsync(timeout.Token);
            if (match(update))
                return update;
        }
    }

    private static string PlayerIdOf(Update update, Guid userId) =>
        update.Players.EnumerateArray()
            .Single(p => p.GetProperty("userId").GetGuid() == userId)
            .GetProperty("id").GetString()!;

    private static string? UserNameOf(JsonElement players, Guid userId) =>
        players.EnumerateArray()
            .Single(p => p.GetProperty("userId").GetGuid() == userId)
            .GetProperty("userName").GetString();

    private static List<string> Identifiers(JsonElement territories) =>
        territories.EnumerateArray().Select(t => t.GetProperty("identifier").GetString()!).Order().ToList();
}
