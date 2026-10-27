using System.Security.Claims;
using Risk.Application.Interfaces;

namespace Risk.Api.Auth;

/// <summary>Reads the current user from the JWT on the HTTP request.</summary>
public sealed class HttpCurrentUser(IHttpContextAccessor httpContextAccessor) : ICurrentUser
{
    public Guid UserId
    {
        get
        {
            var principal = httpContextAccessor.HttpContext?.User
                            ?? throw new InvalidOperationException("No HTTP context.");

            // The JWT handler maps "sub" to NameIdentifier by default; fall back to the raw claim.
            var subject = principal.FindFirstValue(ClaimTypes.NameIdentifier)
                          ?? principal.FindFirstValue("sub")
                          ?? throw new InvalidOperationException("No subject claim.");

            return Guid.Parse(subject);
        }
    }
}
