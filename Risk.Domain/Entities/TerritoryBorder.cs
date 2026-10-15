namespace Risk.Domain.Entities;

/// <summary>
/// Models the border (adjacency) between two territories.
/// The relationship is directional in the table but logically bidirectional —
/// both directions are inserted when a border is created.
/// </summary>
public sealed class TerritoryBorder
{
    public Guid TerritoryId { get; private set; }
    public Guid BorderingTerritoryId { get; private set; }

    // Navigation properties
    public Territory? Territory { get; private set; }
    public Territory? BorderingTerritory { get; private set; }

    // EF Core requires a parameterless constructor
    private TerritoryBorder() { }

    public TerritoryBorder(Guid territoryId, Guid borderingTerritoryId)
    {
        if (territoryId == Guid.Empty)
            throw new ArgumentException("TerritoryId must be a valid GUID.", nameof(territoryId));
        if (borderingTerritoryId == Guid.Empty)
            throw new ArgumentException("BorderingTerritoryId must be a valid GUID.", nameof(borderingTerritoryId));
        if (territoryId == borderingTerritoryId)
            throw new ArgumentException("A territory cannot border itself.");

        TerritoryId = territoryId;
        BorderingTerritoryId = borderingTerritoryId;
    }
}
