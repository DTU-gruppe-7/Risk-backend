using Microsoft.Extensions.DependencyInjection;
using Risk.Application.Interfaces.Services;
using Risk.Application.Services;

namespace Risk.Application;

public static class DependencyInjection
{
    public static IServiceCollection AddApplication(this IServiceCollection services)
    {
        services.AddScoped<IGameplayService, GameplayService>();

        return services;
    }
}
