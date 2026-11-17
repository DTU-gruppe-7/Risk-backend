using System.Text.Json.Serialization;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.SignalR;
using Risk.Api.Realtime.Games;
using Risk.Application.Interfaces.Services;

namespace Risk.Api.Realtime;

/// <summary>
/// Registration of every realtime hub. A new hub is a client interface, a class inheriting
/// <see cref="RealtimeHub{TClient}"/> and one line in <see cref="MapRealtimeHubs"/>.
/// </summary>
public static class RealtimeEndpoints
{
    /// <summary>All hubs live under this path. Caddy and the Vite proxy forward it to the API.</summary>
    public const string HubsPath = "/hubs";

    public static IServiceCollection AddRealtime(this IServiceCollection services)
    {
        // Same enum format as the REST API, so the frontend can use the same types.
        services
            .AddSignalR(options => options.AddFilter<RealtimeExceptionFilter>())
            .AddJsonProtocol(options =>
                options.PayloadSerializerOptions.Converters.Add(new JsonStringEnumConverter()));

        services.AddSingleton<IGameRealtime, SignalRGameRealtime>();
        return services;
    }

    public static IEndpointRouteBuilder MapRealtimeHubs(this IEndpointRouteBuilder endpoints)
    {
        // Access tokens live 15 minutes. Closing the connection when it expires makes the client
        // reconnect with a fresh token, instead of staying connected on an expired one.
        endpoints.MapHub<GameHub>($"{HubsPath}/game", options => options.CloseOnAuthenticationExpiration = true);
        return endpoints;
    }

    /// <summary>
    /// Browsers cannot send an Authorization header on WebSockets, so the SignalR client sends the
    /// access token as <c>?access_token=</c>. It is only accepted on hub paths.
    /// </summary>
    public static JwtBearerEvents ReadTokenFromQueryOnHubs() => new()
    {
        OnMessageReceived = context =>
        {
            var token = context.Request.Query["access_token"].ToString();
            if (token.Length > 0 && context.HttpContext.Request.Path.StartsWithSegments(HubsPath))
                context.Token = token;
            return Task.CompletedTask;
        },
    };
}
