using Microsoft.EntityFrameworkCore;
using Risk.Application.Interfaces.Repository;
using Risk.Domain.Map;
using Risk.Infrastructure.Persistence;

namespace Risk.Infrastructure.Repositories;

public class MapRepository(AppDbContext context) : IMapRepository
{
    public async Task<IReadOnlyList<MapContinent>> GetContinentsAsync(CancellationToken cancellationToken = default) =>
        await context.MapContinents.AsNoTracking().OrderBy(c => c.Id).ToListAsync(cancellationToken);

    public async Task<IReadOnlyList<MapTerritory>> GetTerritoriesAsync(CancellationToken cancellationToken = default) =>
        await context.MapTerritories
            .AsNoTracking()
            .Include(t => t.Borders)
            .OrderBy(t => t.Id)
            .ToListAsync(cancellationToken);

    public async Task<MapGraph> GetGraphAsync(CancellationToken cancellationToken = default)
    {
        var continents = await GetContinentsAsync(cancellationToken);
        var territories = await context.MapTerritories.AsNoTracking().ToListAsync(cancellationToken);
        var borders = await context.MapBorders.AsNoTracking().ToListAsync(cancellationToken);
        return new MapGraph(continents, territories, borders);
    }
}
