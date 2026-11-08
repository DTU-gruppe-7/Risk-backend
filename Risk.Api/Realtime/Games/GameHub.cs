using Risk.Application.Common;
using Risk.Application.Games;

namespace Risk.Api.Realtime.Games;

/// <summary>What the game hub can call on the client. Method names must match the frontend's <c>GameHubEvents</c>.</summary>
public interface IGameClient
{
    /// <summary>The game and the territories the receiver may see, after any change.</summary>
    Task GameUpdated(GameUpdateMessage update);
}

/// <summary>
/// Live updates for one game at <c>/hubs/game</c>. Only the host and the players may watch.
/// Updates themselves are sent by <see cref="SignalRGameRealtime"/> after every saved change.
/// </summary>
public sealed class GameHub(GameAccess access, GameUpdatePublisher publisher) : RealtimeHub<IGameClient>
{
    /// <summary>Starts watching a game and immediately receives its current state.</summary>
    public async Task JoinGame(Guid gameId)
    {
        var userId = UserId;
        await access.RequireParticipantAsync(gameId, userId, Context.ConnectionAborted);
        await AddToGroupsAsync(RealtimeGroups.Game(gameId), RealtimeGroups.UserInGame(gameId, userId));

        // A (re)joining client may have missed updates, so it gets the current state right away.
        await Clients.Caller.GameUpdated(
            await publisher.SnapshotForAsync(gameId, userId, Context.ConnectionAborted));
    }

    public Task LeaveGame(Guid gameId) =>
        RemoveFromGroupsAsync(RealtimeGroups.Game(gameId), RealtimeGroups.UserInGame(gameId, UserId));
}
