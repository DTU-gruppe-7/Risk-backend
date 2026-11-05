namespace Risk.Domain.Services;

public record BattleResult(
    int AttackerLosses,
    int DefenderLosses,
    IReadOnlyList<int> SortedAttackerDice,
    IReadOnlyList<int> SortedDefenderDice
);

public static class BattleService
{
    public static BattleResult Fight(IReadOnlyList<int> attackerDice, IReadOnlyList<int> defenderDice)
    {
        if (attackerDice == null || attackerDice.Count == 0)
            throw new ArgumentException("Attacker must roll at least 1 die.", nameof(attackerDice));

        if (defenderDice == null || defenderDice.Count == 0)
            throw new ArgumentException("Defender must roll at least 1 die.", nameof(defenderDice));

        // Risk-regler: Sorter altid faldende (højeste terning først)
        var sortedAttacker = attackerDice.OrderByDescending(d => d).ToList();
        var sortedDefender = defenderDice.OrderByDescending(d => d).ToList();

        int attackerLosses = 0;
        int defenderLosses = 0;

        // Sammenlign parvis (højest mod højest, evt. næsthøjest mod næsthøjest)
        int pairsToCompare = Math.Min(sortedAttacker.Count, sortedDefender.Count);

        for (int i = 0; i < pairsToCompare; i++)
        {
            if (sortedAttacker[i] > sortedDefender[i])
            {
                defenderLosses++;
            }
            else
            {
                // Uafgjort går altid til forsvareren i Risk
                attackerLosses++;
            }
        }

        return new BattleResult(
            attackerLosses,
            defenderLosses,
            sortedAttacker,
            sortedDefender
        );
    }
}
