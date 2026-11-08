using Risk.Domain.Entities;

namespace Risk.Application.Territories;

/// <summary>
/// One territory's state in a game. Continent and neighbours are static and
/// come from <c>GET /api/map</c>, matched on <paramref name="Identifier"/>.
/// </summary>
/// <param name="Id">Territory state ID.</param>
/// <param name="Identifier">Map territory slug (e.g. "north-africa").</param>
/// <param name="OwnerId">Player ID of current owner, or null if unoccupied.</param>
/// <param name="TroopCount">Troops currently stationed here.</param>
/// <param name="IsBlizzard">Blizzard mode: impassable for the whole game.</param>
public sealed record TerritoryResponse(
    Guid Id, string Identifier,
    Guid? OwnerId, int TroopCount, bool IsBlizzard)
{
    internal static TerritoryResponse From(Territory t) =>
        new(t.Id, t.Identifier, t.OwnerId, t.TroopCount, t.IsBlizzard);
}

/// <summary>Payload to deploy troops from the current player's reserve.</summary>
public sealed record DeployTroopsRequest(int Troops);
