using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Risk.Infrastructure.Identity;
using Risk.Infrastructure.Persistence;

namespace Risk.Infrastructure;

public static class DependencyInjection
{
    public static IServiceCollection AddInfrastructure(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        services.AddDbContext<AppDbContext>(options =>
            options.UseNpgsql(configuration.GetConnectionString("Default")));

        services.AddIdentityCore<ApplicationUser>(options =>
            {
                options.User.RequireUniqueEmail = true;
                options.Password.RequiredLength = 12;
            })
            .AddRoles<ApplicationRole>()
            .AddEntityFrameworkStores<AppDbContext>();

        return services;
    }
}
