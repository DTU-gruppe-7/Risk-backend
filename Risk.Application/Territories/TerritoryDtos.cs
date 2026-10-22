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

/// <summary>Payload to claim a territory for a player.</summary>
/// <param name="PlayerId">Player claiming the territory.</param>
/// <param name="InitialTroops">Troops to place on claim (minimum 1).</param>
public sealed record ClaimTerritoryRequest(Guid PlayerId, int InitialTroops = 1);

/// <summary>Payload to add or remove troops from a territory.</summary>
/// <param name="Delta">Positive to add troops, negative to remove.</param>
public sealed record UpdateTroopsRequest(int Delta);
