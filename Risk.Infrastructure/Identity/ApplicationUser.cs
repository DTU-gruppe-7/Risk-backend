using Microsoft.AspNetCore.Identity;

namespace Risk.Infrastructure.Identity;

/// <summary>
/// Login-siden af en bruger. Lever kun i Infrastructure, så Domain
/// aldrig behøver kende til ASP.NET Identity.
/// Peger på den tilsvarende domæne-User via <see cref="DomainUserId"/>.
/// </summary>
public sealed class ApplicationUser : IdentityUser<Guid>
{
    public Guid DomainUserId { get; set; }
}
