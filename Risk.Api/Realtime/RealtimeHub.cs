using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.SignalR;

namespace Risk.Api.Realtime;

/// <summary>
/// Base for every realtime hub (game, and later lobby, chat, …). Requires a logged-in user and
/// gives hubs the user's id and group helpers. Hubs never use <c>ICurrentUser</c>: they pass
/// <see cref="UserId"/> to Application explicitly, since a hub call is not an HTTP request.
/// </summary>
/// <typeparam name="TClient">The methods the server can call on the client.</typeparam>
[Authorize]
public abstract class RealtimeHub<TClient> : Hub<TClient> where TClient : class
{
    /// <summary>The domain user id from the JWT's <c>sub</c> claim.</summary>
    protected Guid UserId =>
        Guid.TryParse(Context.UserIdentifier, out var userId)
            ? userId
            : throw new HubException("The connection has no user.");

    protected async Task AddToGroupsAsync(params string[] groups)
    {
        foreach (var group in groups)
            await Groups.AddToGroupAsync(Context.ConnectionId, group, Context.ConnectionAborted);
    }

    protected async Task RemoveFromGroupsAsync(params string[] groups)
    {
        foreach (var group in groups)
            await Groups.RemoveFromGroupAsync(Context.ConnectionId, group, Context.ConnectionAborted);
    }
}
