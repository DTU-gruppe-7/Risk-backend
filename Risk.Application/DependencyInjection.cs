
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Risk.Application.Interfaces.Services;
using Risk.Application.Services;
using Risk.Application.Auth;
using Risk.Application.Cards;
using Risk.Application.Common;
using Risk.Application.Games;
using Risk.Application.Map;
using Risk.Application.Players;
using Risk.Application.Territories;
using Risk.Application.Users;

namespace Risk.Application;

public static class DependencyInjection
{
    public static IServiceCollection AddApplication(this IServiceCollection services)
    {
        services.TryAddSingleton(TimeProvider.System);
        services.AddScoped<GameAccess>();

        services.AddScoped<IGameplayService, GameplayService>();
        services.AddScoped<ForcedCardTradeGuard>();
        services.AddScoped<ConquestResolver>();
        services.AddScoped<GameDeckSetup>();
        services.AddScoped<FogOfWar>();
        services.AddScoped<AuthService>();
        services.AddScoped<UserService>();
        services.AddScoped<GameService>();
        services.AddScoped<PlayerService>();
        services.AddScoped<TerritoryService>();
        services.AddScoped<MapService>();
        services.AddScoped<BonusCardService>();
        services.AddScoped<MissionCardService>();
        services.AddScoped<TurnTimeoutService>();
        services.AddScoped<GameUpdatePublisher>();


        return services;
    }
}
