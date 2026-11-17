namespace Risk.Api.Realtime;

/// <summary>Group names for all hubs, in one place so senders and hubs can never disagree.</summary>
public static class RealtimeGroups
{
    /// <summary>Every connection watching a game.</summary>
    public static string Game(Guid gameId) => $"game:{gameId}";

    /// <summary>One user's connections watching a game. Used for per-viewer (fog of war) updates.</summary>
    public static string UserInGame(Guid gameId, Guid userId) => $"game:{gameId}:user:{userId}";
}
