namespace Risk.Application.DTOs;

public record BlitzAttackDto(
    Guid GameId,
    Guid PlayerId,
    Guid FromTerritoryId,
    Guid ToTerritoryId,
    int? MovingTroopsCount = null
);

public record CompleteConquestDto(Guid GameId, Guid FromTerritoryId, Guid ToTerritoryId, int MovingTroopsCount);

/// <param name="Comparisons">Each pair of dice compared in this round, highest first, and who won it.</param>
public record BlitzRoundDto(
    IReadOnlyList<int> AttackerDice,
    IReadOnlyList<int> DefenderDice,
    int AttackerLosses,
    int DefenderLosses,
    IReadOnlyList<DiceComparisonDto> Comparisons
);

public record BlitzResultDto(
    int TotalAttackerLosses,
    int TotalDefenderLosses,
    bool TerritoryConquered,
    int RemainingAttackingTerritoryTroops,
    int RemainingDefendingTerritoryTroops,
    IReadOnlyList<BlitzRoundDto> Rounds,
    int TroopsMoved = 0
);
