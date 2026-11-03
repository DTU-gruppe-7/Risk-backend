namespace Risk.Application.DTOs;

public record BlitzAttackDto(
    Guid GameId,
    Guid PlayerId,
    Guid FromTerritoryId,
    Guid ToTerritoryId,
    int? MovingTroopsCount = null
);

public record BlitzRoundDto(
    IReadOnlyList<int> AttackerDice,
    IReadOnlyList<int> DefenderDice,
    int AttackerLosses,
    int DefenderLosses
);

public record BlitzResultDto(
    int TotalAttackerLosses,
    int TotalDefenderLosses,
    bool TerritoryConquered,
    int RemainingAttackingTerritoryTroops,
    int RemainingDefendingTerritoryTroops,
    IReadOnlyList<BlitzRoundDto> Rounds,
    int TroopsMoved = 0,
    bool BonusCardAwarded = false
);
