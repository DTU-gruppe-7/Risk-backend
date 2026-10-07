namespace Risk.Application.DTOs.Gameplay;

public record AttackDto(
    Guid GameId,
    Guid PlayerId,
    Guid FromTerritoryId,
    Guid ToTerritoryId,
    int AttackingTroopsCount
);

public record AttackResultDto(
    bool TerritoryConquered,
    int AttackerLosses,
    int DefenderLosses,
    IReadOnlyList<int> AttackerDice,
    IReadOnlyList<int> DefenderDice
);
