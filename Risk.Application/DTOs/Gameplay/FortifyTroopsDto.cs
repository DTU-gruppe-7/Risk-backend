namespace Risk.Application.DTOs.Gameplay;

public record FortifyTroopsDto(
    Guid GameId,
    Guid PlayerId,
    Guid FromTerritoryId,
    Guid ToTerritoryId,
    int TroopCount
);