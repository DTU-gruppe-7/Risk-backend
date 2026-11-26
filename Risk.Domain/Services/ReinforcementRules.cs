using Risk.Domain.Exceptions;

namespace Risk.Domain.Services;

/// <summary>
/// How many troops players get: at the start of the game and when their turn starts.
/// <see cref="Entities.Game"/> works out the numbers from its state and asks these rules for the result.
/// </summary>
public static class ReinforcementRules
{
    /// <summary>A player never gets fewer troops than this for their territories.</summary>
    public const int MinimumTerritoryReinforcements = 3;

    /// <summary>The first seats in the turn order get no first-round bonus.</summary>
    public const int SeatsWithoutFirstRoundBonus = 3;

    /// <summary>Classic starting troops per player, including the one placed on each territory.</summary>
    public static int StartingTroops(int playerCount) =>
        playerCount switch
        {
            2 => 40,
            3 => 35,
            4 => 30,
            5 => 25,
            6 => 20,
            _ => throw new BusinessRuleException("Unsupported number of players for classic Risk."),
        };

    /// <summary>Territories / 3 (at least <see cref="MinimumTerritoryReinforcements"/>) plus continent bonus.</summary>
    public static int ForTerritories(int ownedTerritories, int continentBonus) =>
        Math.Max(MinimumTerritoryReinforcements, ownedTerritories / 3) + continentBonus;

    /// <summary>
    /// Extra troops in the first round for players late in the turn order, since they may lose
    /// territories before their first turn: the first three seats get none, then 4th +1 … 6th +3.
    /// </summary>
    public static int FirstRoundBonus(int turnOrder) =>
        Math.Max(0, turnOrder - SeatsWithoutFirstRoundBonus);
}
