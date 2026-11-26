using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using System.Threading.Channels;
using Microsoft.AspNetCore.SignalR.Client;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Risk.Application.Interfaces;
using Risk.Infrastructure.Persistence;

namespace Risk.Tests.Integration;

[Collection(ApiCollection.Name)]
public sealed class CardTests(ApiFixture api)
{
    private static readonly TimeSpan Timeout = TimeSpan.FromSeconds(10);

    [Fact]
    public async Task Players_cannot_take_a_bonus_card_themselves()
    {
        var red = await api.RegisterAsync("red");
        var blue = await api.RegisterAsync("blue");
        var gameId = await StartGameAsync(red, blue);
        // The draw pile is secret, so the card is picked from the database.
        var cardId = await DrawPileCardAsync(gameId);

        var response = await blue.Http.PutAsJsonAsync(
            $"/api/games/{gameId}/bonus-cards/{cardId}/assign",
            new { playerId = await PlayerIdAsync(blue, gameId) });

        Assert.False(response.IsSuccessStatusCode);
        Assert.Null(await OwnerOfAsync(cardId));
    }

    [Fact]
    public async Task A_player_only_sees_their_own_cards()
    {
        var red = await api.RegisterAsync("red");
        var blue = await api.RegisterAsync("blue");
        var gameId = await StartGameAsync(red, blue);
        var redCard = await GiveCardAsync(gameId, await DrawPileCardAsync(gameId), await PlayerIdAsync(red, gameId));

        var redHand = await red.Http.GetFromJsonAsync<JsonElement>($"/api/games/{gameId}/bonus-cards");
        var blueHand = await blue.Http.GetFromJsonAsync<JsonElement>($"/api/games/{gameId}/bonus-cards");
        var peek = await blue.Http.GetAsync($"/api/games/{gameId}/bonus-cards?playerId={await PlayerIdAsync(red, gameId)}");

        Assert.Equal([redCard], redHand.EnumerateArray().Select(c => c.GetProperty("id").GetGuid()));
        Assert.Equal(0, blueHand.GetArrayLength());   // not red's card, and not the draw pile either
        Assert.Equal(HttpStatusCode.Forbidden, peek.StatusCode);
    }

    [Fact]
    public async Task Only_the_player_who_gets_cards_is_told_about_them()
    {
        var red = await api.RegisterAsync("red");
        var blue = await api.RegisterAsync("blue");
        var gameId = await StartGameAsync(red, blue);
        await using var redHub = await api.ConnectGameHubAsync(red);
        await using var blueHub = await api.ConnectGameHubAsync(blue);
        var redReceived = CollectCardsReceived(redHub);
        var blueReceived = CollectCardsReceived(blueHub);
        await redHub.InvokeAsync("JoinGame", gameId);
        await blueHub.InvokeAsync("JoinGame", gameId);

        // From the draw pile to red, as the card for conquering is drawn …
        var cardId = await GiveCardAsync(gameId, await DrawPileCardAsync(gameId), await PlayerIdAsync(red, gameId));
        var conquest = await NextAsync(redReceived);
        // … then from red to blue, as when blue knocks red out.
        await GiveCardAsync(gameId, cardId, await PlayerIdAsync(blue, gameId));
        var elimination = await NextAsync(blueReceived);

        Assert.Equal("Conquest", conquest.GetProperty("reason").GetString());
        Assert.Equal("Elimination", elimination.GetProperty("reason").GetString());
        foreach (var message in new[] { conquest, elimination })
        {
            Assert.Equal(gameId, message.GetProperty("gameId").GetGuid());
            Assert.Equal([cardId], message.GetProperty("cards").EnumerateArray().Select(c => c.GetProperty("id").GetGuid()));
        }

        // Each message went to its receiver only.
        await Task.Delay(300);
        Assert.False(redReceived.TryRead(out _));
        Assert.False(blueReceived.TryRead(out _));
    }

    private async Task<Guid> DrawPileCardAsync(Guid gameId)
    {
        using var scope = api.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        return await db.BonusCards
            .Where(card => card.GameId == gameId && card.PlayerId == null && !card.Used)
            .Select(card => card.Id)
            .FirstAsync();
    }

    private async Task<Guid?> OwnerOfAsync(Guid cardId)
    {
        using var scope = api.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        return await db.BonusCards.Where(card => card.Id == cardId).Select(card => card.PlayerId).SingleAsync();
    }

    /// <summary>Hands a card to a player and saves through the unit of work, as the game's own use cases do.</summary>
    private async Task<Guid> GiveCardAsync(Guid gameId, Guid cardId, Guid playerId)
    {
        using var scope = api.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var card = await db.BonusCards.SingleAsync(c => c.GameId == gameId && c.Id == cardId);
        card.AssignToPlayer(playerId);
        await scope.ServiceProvider.GetRequiredService<IUnitOfWork>().SaveChangesAsync();
        return cardId;
    }

    private static ChannelReader<JsonElement> CollectCardsReceived(HubConnection hub)
    {
        var channel = Channel.CreateUnbounded<JsonElement>();
        hub.On<JsonElement>("CardsReceived", body => channel.Writer.TryWrite(body));
        return channel.Reader;
    }

    private static async Task<JsonElement> NextAsync(ChannelReader<JsonElement> messages)
    {
        using var timeout = new CancellationTokenSource(Timeout);
        return await messages.ReadAsync(timeout.Token);
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
