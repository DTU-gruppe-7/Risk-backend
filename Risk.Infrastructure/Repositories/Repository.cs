using Microsoft.EntityFrameworkCore;
using Risk.Application.Interfaces.Repository;
using Risk.Infrastructure.Persistence;

namespace Risk.Infrastructure.Repositories;

public class Repository<T>(AppDbContext context) : IRepository<T> where T : class
{
    protected readonly AppDbContext _context = context;
    protected readonly DbSet<T> _dbSet = context.Set<T>();

    public virtual async Task AddAsync(T entity)
    {
        await _dbSet.AddAsync(entity);
    }

    public virtual async Task<T?> GetByUuidAsync(Guid? uuid)
    {
        if (uuid is null) return null;
        return await _dbSet.FindAsync(uuid);
    }

    public virtual async Task<IEnumerable<T>> GetAllAsync()
    {
        return await _dbSet.ToListAsync();
    }

    public virtual Task UpdateAsync(T entity)
    {
        _context.Entry(entity).State = EntityState.Modified;
        return Task.CompletedTask;
    }

    public virtual Task DeleteAsync(T entity)
    {
        if (entity is not null)
        {
            _dbSet.Remove(entity);
        }
        return Task.CompletedTask;
    }

    public virtual async Task<bool> ExistsAsync(Guid uuid)
    {
        return await _dbSet.FindAsync(uuid) != null;
    }

    public virtual async Task SaveChangesAsync()
    {
        await _context.SaveChangesAsync();
    }
}
