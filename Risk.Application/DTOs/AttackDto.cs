namespace Risk.Application.DTOs;

public record AttackDto(
    Guid GameId,
    Guid PlayerId,
    Guid FromTerritoryId,
    Guid ToTerritoryId,
    int AttackingTroopsCount,
    int DefendingTroopsCount = 2,
    int? MovingTroopsCount = null
);

public record AttackResultDto(
    int AttackerLosses,
    int DefenderLosses,
    IReadOnlyList<int> AttackerDice,
    IReadOnlyList<int> DefenderDice,
    bool TerritoryConquered,
    int TroopsMoved = 0,
    bool BonusCardAwarded = false
);
