namespace Risk.Application.Interfaces.Repository;

public interface IRepository<T> where T : class
{
    Task AddAsync(T entity);
    Task<T?> GetByUuidAsync(Guid? uuid);
    Task<IEnumerable<T>> GetAllAsync();
    Task UpdateAsync(T entity);
    Task DeleteAsync(T entity);
    Task<bool> ExistsAsync(Guid uuid);
    Task SaveChangesAsync();
}
