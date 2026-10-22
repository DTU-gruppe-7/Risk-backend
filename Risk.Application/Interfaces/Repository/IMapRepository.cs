using Risk.Domain.Map;

namespace Risk.Application.Interfaces.Repository;

/// <summary>Read access to the static board (seeded reference data).</summary>
public interface IMapRepository
{
    Task<IReadOnlyList<MapContinent>> GetContinentsAsync(CancellationToken cancellationToken = default);

    /// <summary>All map territories with their borders.</summary>
    Task<IReadOnlyList<MapTerritory>> GetTerritoriesAsync(CancellationToken cancellationToken = default);

    /// <summary>The whole board as a graph, for game rules (blizzards, movement, visibility).</summary>
    Task<MapGraph> GetGraphAsync(CancellationToken cancellationToken = default);
}
