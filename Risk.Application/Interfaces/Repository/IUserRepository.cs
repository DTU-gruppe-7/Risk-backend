using Risk.Domain.Entities;

namespace Risk.Application.Interfaces.Repository;

public interface IUserRepository : IRepository<User>
{
    Task<User?> GetByEmailAsync(string email);
}
