using Microsoft.EntityFrameworkCore;
using Risk.Application.Interfaces.Repository;
using Risk.Infrastructure.Persistence;

namespace Risk.Infrastructure.Repositories;

public abstract class Repository<T>(AppDbContext context) : IRepository<T> where T : class
{
    protected readonly AppDbContext _context = context;
    protected readonly DbSet<T> _dbSet = context.Set<T>();

    public virtual async Task<T?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default) =>
        await _dbSet.FindAsync([id], cancellationToken);

    public void Add(T entity) => _dbSet.Add(entity);

    public void Remove(T entity) => _dbSet.Remove(entity);
}
