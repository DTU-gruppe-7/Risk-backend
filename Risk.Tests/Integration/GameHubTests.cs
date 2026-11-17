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
        }
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
        Assert.True(started.Territories.GetArrayLength() < 42);

        var redPlayerId = PlayerIdOf(started, red.Id);
        Assert.All(started.Players.EnumerateArray().Where(p => p.GetProperty("id").GetString() != redPlayerId),
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

    // -------------------------------------------------------------------------

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
    private static async Task<Update> NextAsync(ChannelReader<Update> updates, Func<Update, bool> match)
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

    private static List<string> Identifiers(JsonElement territories) =>
        territories.EnumerateArray().Select(t => t.GetProperty("identifier").GetString()!).Order().ToList();
}
