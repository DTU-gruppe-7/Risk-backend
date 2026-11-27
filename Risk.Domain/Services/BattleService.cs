namespace Risk.Domain.Services;

/// <summary>The side that won a pair of dice.</summary>
public enum BattleSide
{
    Attacker,
    Defender
}

/// <summary>One pair of dice compared in a battle, and who won it (a tie goes to the defender).</summary>
public sealed record DiceComparison(int AttackerDie, int DefenderDie, BattleSide Winner);

/// <summary>
/// The outcome of one roll. The losses are counted from <see cref="Comparisons"/>, so they can never
/// disagree with the pairs: every pair the defender wins costs the attacker a troop, and the other way round.
/// </summary>
/// <param name="Comparisons">One entry per pair compared, highest dice first. Unpaired dice are not included.</param>
public sealed record BattleResult(
    IReadOnlyList<int> SortedAttackerDice,
    IReadOnlyList<int> SortedDefenderDice,
    IReadOnlyList<DiceComparison> Comparisons)
{
    public int AttackerLosses => Comparisons.Count(comparison => comparison.Winner == BattleSide.Defender);
    public int DefenderLosses => Comparisons.Count(comparison => comparison.Winner == BattleSide.Attacker);
}

public static class BattleService
{
    /// <summary>The most dice the attacker may roll.</summary>
    public const int MaxAttackerDice = 3;

    /// <summary>The most dice the defender rolls.</summary>
    public const int MaxDefenderDice = 2;

    /// <summary>
    /// The defender always rolls as many dice as they may: 2, or 1 when the territory holds a single troop.
    /// The server decides this; a client cannot choose to defend with fewer.
    /// </summary>
    public static int DefenderDiceFor(int defendingTroops) =>
        Math.Min(MaxDefenderDice, defendingTroops);

    /// <summary>The most dice the attacker can roll: up to 3, always leaving one troop behind.</summary>
    public static int AttackerDiceFor(int attackingTroops) =>
        Math.Min(MaxAttackerDice, attackingTroops - 1);

    public static BattleResult Fight(IReadOnlyList<int> attackerDice, IReadOnlyList<int> defenderDice)
    {
        if (attackerDice == null || attackerDice.Count == 0)
            throw new ArgumentException("Attacker must roll at least 1 die.", nameof(attackerDice));

        if (defenderDice == null || defenderDice.Count == 0)
            throw new ArgumentException("Defender must roll at least 1 die.", nameof(defenderDice));

        // Risk-regler: Sorter altid faldende (højeste terning først)
        var sortedAttacker = attackerDice.OrderByDescending(d => d).ToList();
        var sortedDefender = defenderDice.OrderByDescending(d => d).ToList();

        // Sammenlign parvis (højest mod højest, evt. næsthøjest mod næsthøjest)
        var comparisons = sortedAttacker
            .Zip(sortedDefender, (attacker, defender) => new DiceComparison(attacker, defender, WinnerOf(attacker, defender)))
            .ToList();

        return new BattleResult(sortedAttacker, sortedDefender, comparisons);
    }

    // Uafgjort går altid til forsvareren i Risk
    private static BattleSide WinnerOf(int attackerDie, int defenderDie) =>
        attackerDie > defenderDie ? BattleSide.Attacker : BattleSide.Defender;
}
