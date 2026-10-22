using Microsoft.AspNetCore.Identity;
using Risk.Application.Exceptions;
using Risk.Application.Interfaces;

namespace Risk.Infrastructure.Identity;

/// <summary>Adapts ASP.NET Identity to <see cref="IIdentityService"/>.</summary>
public sealed class IdentityService(UserManager<ApplicationUser> userManager) : IIdentityService
{
    public async Task CreateAccountAsync(Guid domainUserId, string email, string password,
        CancellationToken cancellationToken = default)
    {
        var account = new ApplicationUser
        {
            UserName = email,
            Email = email,
            DomainUserId = domainUserId,
        };

        var result = await userManager.CreateAsync(account, password);
        if (!result.Succeeded)
        {
            throw new ValidationFailedException(result.Errors
                .GroupBy(e => e.Code)
                .ToDictionary(g => g.Key, g => g.Select(e => e.Description).ToArray()));
        }
    }

    public async Task<AuthenticatedAccount?> AuthenticateAsync(string email, string password,
        CancellationToken cancellationToken = default)
    {
        var account = await userManager.FindByEmailAsync(email);
        if (account is null || !await userManager.CheckPasswordAsync(account, password))
            return null;

        var roles = await userManager.GetRolesAsync(account);
        return new AuthenticatedAccount(account.DomainUserId, roles.ToList());
    }
}
