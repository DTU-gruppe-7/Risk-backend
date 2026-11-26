using System.Net.Http.Json;
using System.Text.Json;

namespace Risk.Tests.Integration;

[Collection(ApiCollection.Name)]
public sealed class CardTests(ApiFixture api)
{
    [Fact]
    public async Task Players_cannot_take_a_bonus_card_themselves()
    {
        var red = await api.RegisterAsync("red");
        var blue = await api.RegisterAsync("blue");
        var gameId = await StartGameAsync(red, blue);

        var cards = await blue.Http.GetFromJsonAsync<JsonElement>($"/api/games/{gameId}/bonus-cards");
        var cardId = cards.EnumerateArray()
            .First(c => c.GetProperty("playerId").ValueKind == JsonValueKind.Null)
            .GetProperty("id").GetGuid();

        var response = await blue.Http.PutAsJsonAsync(
            $"/api/games/{gameId}/bonus-cards/{cardId}/assign",
            new { playerId = await PlayerIdAsync(blue, gameId) });

        Assert.False(response.IsSuccessStatusCode);
        var after = await blue.Http.GetFromJsonAsync<JsonElement>($"/api/games/{gameId}/bonus-cards");
        var card = after.EnumerateArray().Single(c => c.GetProperty("id").GetGuid() == cardId);
        Assert.Equal(JsonValueKind.Null, card.GetProperty("playerId").ValueKind);
    }

    private static async Task<Guid> StartGameAsync(TestUser host, TestUser other)
    {
        var created = await host.Http.PostAsJsonAsync("/api/games", new { hasFogOfWar = false });
        created.EnsureSuccessStatusCode();
        var gameId = (await created.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("id").GetGuid();

        (await host.Http.PostAsJsonAsync($"/api/games/{gameId}/players", new { color = "red" })).EnsureSuccessStatusCode();
        (await other.Http.PostAsJsonAsync($"/api/games/{gameId}/players", new { color = "blue" })).EnsureSuccessStatusCode();
        (await host.Http.PutAsync($"/api/games/{gameId}/start", null)).EnsureSuccessStatusCode();
        return gameId;
    }

    private static async Task<Guid> PlayerIdAsync(TestUser user, Guid gameId)
    {
        var game = await user.Http.GetFromJsonAsync<JsonElement>($"/api/games/{gameId}");
        return game.GetProperty("players").EnumerateArray()
            .Single(p => p.GetProperty("userId").GetGuid() == user.Id)
            .GetProperty("id").GetGuid();
    }
}