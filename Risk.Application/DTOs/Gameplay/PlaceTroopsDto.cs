namespace Risk.Application.DTOs.Gameplay;

public record PlaceTroopsDto(
    Guid GameId,
    Guid PlayerId,
    Guid TerritoryId,
    int TroopCount
);