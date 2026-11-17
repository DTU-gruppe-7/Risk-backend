using Risk.Application.Games;

namespace Risk.Application.Interfaces.Services;

/// <summary>
/// Pushes game state to the clients watching a game. Implemented in the API with SignalR,
/// so Application decides what each viewer may see without knowing how it is delivered.
/// </summary>
public interface IGameRealtime
{
    /// <summary>Sends the same update to every connection watching the game.</summary>
    Task SendToGameAsync(Guid gameId, GameUpdateMessage update);

    /// <summary>Sends an update only to the given user's connections watching the game.</summary>
    Task SendToUserInGameAsync(Guid gameId, Guid userId, GameUpdateMessage update);
}
