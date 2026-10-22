using Risk.Domain.Map;

namespace Risk.Application.Map;

/// <summary>The static board: the same for every game.</summary>
/// <param name="Continents">All continents.</param>
/// <param name="Territories">All territories with their neighbours.</param>
public sealed record MapResponse(
    IEnumerable<MapContinentResponse> Continents,
    IEnumerable<MapTerritoryResponse> Territories);

/// <summary>A continent on the board.</summary>
/// <param name="Id">Stable slug, e.g. "north-america".</param>
/// <param name="Name">Display name.</param>
/// <param name="CaptureBonus">Extra troops per turn for holding every territory in it.</param>
public sealed record MapContinentResponse(string Id, string Name, int CaptureBonus)
{
    internal static MapContinentResponse From(MapContinent c) => new(c.Id, c.Name, c.CaptureBonus);
}

/// <summary>A territory on the board.</summary>
/// <param name="Id">Stable slug, e.g. "north-africa". Matches the frontend geometry ids.</param>
/// <param name="ContinentId">The continent it belongs to.</param>
/// <param name="NeighborIds">Ids of every territory it borders.</param>
public sealed record MapTerritoryResponse(string Id, string ContinentId, IEnumerable<string> NeighborIds)
{
    internal static MapTerritoryResponse From(MapTerritory t) =>
        new(t.Id, t.ContinentId, t.Borders.Select(b => b.NeighborId).ToList());
}
