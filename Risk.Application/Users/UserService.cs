using Risk.Application.Exceptions;
using Risk.Application.Interfaces;
using Risk.Application.Interfaces.Repository;
using Risk.Domain.Entities;

namespace Risk.Application.Users;

/// <summary>Use cases for the current user's own profile.</summary>
public sealed class UserService(IUserRepository users, IUnitOfWork unitOfWork, ICurrentUser currentUser)
{
    public async Task<UserResponse> GetMeAsync(CancellationToken ct = default) =>
        UserResponse.From(await RequireMeAsync(ct));

    public async Task<UserResponse> UpdateMeAsync(UpdateUserRequest request, CancellationToken ct = default)
    {
        var user = await RequireMeAsync(ct);
        user.Rename(request.UserName);
        await unitOfWork.SaveChangesAsync(ct);
        return UserResponse.From(user);
    }

    private async Task<User> RequireMeAsync(CancellationToken ct) =>
        await users.GetByIdAsync(currentUser.UserId, ct)
        ?? throw new NotFoundException("User", currentUser.UserId);
}
