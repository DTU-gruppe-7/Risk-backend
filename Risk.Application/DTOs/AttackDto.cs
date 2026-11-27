using Risk.Domain.Services;

namespace Risk.Application.DTOs;

/// <summary>
/// An attack with the chosen number of attacking dice. The defender's dice are not part of it:
/// the server always rolls as many as the defender may (<see cref="BattleService.DefenderDiceFor"/>).
/// </summary>
public record AttackDto(
    Guid GameId,
    Guid PlayerId,
    Guid FromTerritoryId,
    Guid ToTerritoryId,
    int AttackingTroopsCount,
    int? MovingTroopsCount = null
);

/// <param name="Comparisons">Each pair of dice compared, highest first, and who won it.</param>
public record AttackResultDto(
    int AttackerLosses,
    int DefenderLosses,
    IReadOnlyList<int> AttackerDice,
    IReadOnlyList<int> DefenderDice,
    IReadOnlyList<DiceComparisonDto> Comparisons,
    bool TerritoryConquered,
    int TroopsMoved = 0
);

/// <summary>
/// One pair of dice the server compared. A die without a pair (e.g. the attacker's third die against
/// two defending dice) is not included. A tie goes to the defender.
/// </summary>
public record DiceComparisonDto(int AttackerDie, int DefenderDie, BattleSide Winner)
{
    internal static IReadOnlyList<DiceComparisonDto> From(BattleResult battle) =>
        battle.Comparisons
            .Select(comparison => new DiceComparisonDto(comparison.AttackerDie, comparison.DefenderDie, comparison.Winner))
            .ToList();
}
