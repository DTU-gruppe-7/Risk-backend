using Microsoft.AspNetCore.SignalR;
using Risk.Application.Cards;
using Risk.Application.Games;
using Risk.Application.Interfaces.Services;

namespace Risk.Api.Realtime.Games;

/// <summary>Delivers game updates through <see cref="GameHub"/>. Works outside hub calls too (requests, background work).</summary>
public sealed class SignalRGameRealtime(IHubContext<GameHub, IGameClient> hub) : IGameRealtime
{
    public Task SendToGameAsync(Guid gameId, GameUpdateMessage update) =>
        hub.Clients.Group(RealtimeGroups.Game(gameId)).GameUpdated(update);

    public Task SendToUserInGameAsync(Guid gameId, Guid userId, GameUpdateMessage update) =>
        hub.Clients.Group(RealtimeGroups.UserInGame(gameId, userId)).GameUpdated(update);

    public Task SendSelectionToUserInGameAsync(Guid gameId, Guid userId, TerritorySelectionDto selection) =>
        hub.Clients.Group(RealtimeGroups.UserInGame(gameId, userId)).SelectionChanged(selection);

    public Task SendCardsReceivedToUserInGameAsync(Guid gameId, Guid userId, CardsReceivedMessage received) =>
        hub.Clients.Group(RealtimeGroups.UserInGame(gameId, userId)).CardsReceived(received);
}
