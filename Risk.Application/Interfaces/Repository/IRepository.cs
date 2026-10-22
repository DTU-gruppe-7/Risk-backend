namespace Risk.Application.Interfaces.Repository;

/// <summary>
/// The operations every aggregate repository shares. Queries live on the specific
/// repositories, and saving lives on <see cref="IUnitOfWork"/>, so no caller is forced
/// to depend on methods it does not use.
/// </summary>
public interface IRepository<T> where T : class
{
    Task<T?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default);
    void Add(T entity);
    void Remove(T entity);
}
