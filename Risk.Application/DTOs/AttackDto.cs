namespace Risk.Application.DTOs;

public record AttackDto(
    Guid GameId,
    Guid PlayerId,
    Guid FromTerritoryId,
    Guid ToTerritoryId,
    int AttackingTroopsCount
);

public record AttackResultDto(
    int AttackerLosses,
    int DefenderLosses,
    IReadOnlyList<int> AttackerDice,
    IReadOnlyList<int> DefenderDice,
    bool TerritoryConquered
);
