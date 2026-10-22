using Risk.Application.Interfaces.Repository;

namespace Risk.Application.Map;

/// <summary>Serves the static board.</summary>
public sealed class MapService(IMapRepository map)
{
    public async Task<MapResponse> GetMapAsync(CancellationToken ct = default)
    {
        var continents = await map.GetContinentsAsync(ct);
        var territories = await map.GetTerritoriesAsync(ct);

        return new MapResponse(
            continents.Select(MapContinentResponse.From).ToList(),
            territories.Select(MapTerritoryResponse.From).ToList());
    }
}
