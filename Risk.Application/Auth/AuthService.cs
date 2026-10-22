using Risk.Application.Exceptions;
using Risk.Application.Interfaces;
using Risk.Application.Interfaces.Repository;
using Risk.Domain.Entities;

namespace Risk.Application.Auth;

/// <summary>Registration and login. Issues a signed JWT on success.</summary>
public sealed class AuthService(
    IUserRepository users,
    IIdentityService identity,
    IJwtTokenService tokens,
    IUnitOfWork unitOfWork)
{
    /// <summary>
    /// Creates the domain user and its login credentials in one transaction,
    /// so a failed password check never leaves an orphaned domain user behind.
    /// </summary>
    public async Task<AuthResponse> RegisterAsync(RegisterRequest request, CancellationToken ct = default)
    {
        if (await users.GetByEmailAsync(request.Email, ct) is not null)
            throw new ValidationFailedException("DuplicateEmail", $"Email '{request.Email}' is already taken.");

        await using var transaction = await unitOfWork.BeginTransactionAsync(ct);

        var user = new User(request.UserName, request.Email);
        users.Add(user);
        await unitOfWork.SaveChangesAsync(ct);

        await identity.CreateAccountAsync(user.Id, user.Email, request.Password, ct);
        await transaction.CommitAsync(ct);

        return Issue(user, roles: []);
    }

    public async Task<AuthResponse> LoginAsync(LoginRequest request, CancellationToken ct = default)
    {
        var account = await identity.AuthenticateAsync(request.Email, request.Password, ct)
                      ?? throw new AuthenticationFailedException();

        var user = await users.GetByIdAsync(account.DomainUserId, ct)
                   ?? throw new AuthenticationFailedException();

        return Issue(user, account.Roles);
    }

    private AuthResponse Issue(User user, IEnumerable<string> roles) =>
        new(tokens.CreateToken(user, roles), AuthUserResponse.From(user));
}
