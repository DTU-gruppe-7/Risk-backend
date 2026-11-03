
using Microsoft.Extensions.DependencyInjection;
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
    public static IServiceCollection AddApplication(this IServiceCollection services, bool includeBackgroundServices = true)
    {

        services.AddScoped<IGameplayService, GameplayService>();
        services.AddScoped<GameAccess>();
        services.AddScoped<AuthService>();
        services.AddScoped<UserService>();
        services.AddScoped<GameService>();
        services.AddScoped<PlayerService>();
        services.AddScoped<TerritoryService>();
        services.AddScoped<MapService>();
        services.AddScoped<BonusCardService>();
        services.AddScoped<MissionCardService>();
        if (includeBackgroundServices)
            services.AddHostedService<TurnTimeoutWorker>();


        return services;
    }
}
