using Microsoft.EntityFrameworkCore;
using Risk.Application.Interfaces.Repository;
using Risk.Domain.Entities;
using Risk.Infrastructure.Persistence;

namespace Risk.Infrastructure.Repositories;

public class UserRepository(AppDbContext context) : Repository<User>(context), IUserRepository
{
    public Task<User?> GetByEmailAsync(string email, CancellationToken cancellationToken = default)
    {
        var normalizedEmail = User.NormalizeEmail(email);
        return _dbSet.FirstOrDefaultAsync(u => u.Email == normalizedEmail, cancellationToken);
    }
}
