using Risk.Domain.Entities;

namespace Risk.Domain.Services;

/// <summary>
/// What a set of three bonus cards is worth. Two official rule sets exist, chosen per game
/// (<see cref="CardTradeMode"/>). The territory bonus is the same in both
/// (<see cref="TerritoryBonusTarget"/>); <see cref="Game"/> only tracks that it is given once per turn.
/// </summary>
public static class CardTradeRules
{
    /// <summary>Extra troops placed on a territory shown on a traded card that the player owns.</summary>
    public const int TerritoryBonusTroops = 2;

    /// <summary>Fixed mode: a set with one card of each type.</summary>
    public const int FixedOneOfEachValue = 10;

    /// <summary>
    /// Three of a kind, one of each, or any set with a wild card (which stands in for whatever
    /// type makes the set valid).
    /// </summary>
    public static bool IsValidSet(IReadOnlyList<BonusCard> cards)
    {
        var nonWild = NonWildTypes(cards);
        var hasWild = nonWild.Count != cards.Count;
        var threeOfAKind = nonWild.Count == 3 && nonWild.Distinct().Count() == 1;
        var oneOfEach = nonWild.Count == 3 && nonWild.Distinct().Count() == 3;
        return hasWild || threeOfAKind || oneOfEach;
    }

    /// <summary>
    /// The territory that gets <see cref="TerritoryBonusTroops"/>: the one shown on the first card in
    /// <paramref name="cards"/> that <paramref name="playerId"/> owns, or null when they own none of them.
    /// </summary>
    public static Territory? TerritoryBonusTarget(
        IReadOnlyList<BonusCard> cards, IEnumerable<Territory> territories, Guid playerId)
    {
        var owned = territories.Where(territory => territory.OwnerId == playerId).ToList();
        return cards
            .Where(card => card.TerritoryIdentifier is not null)
            .Select(card => owned.FirstOrDefault(territory => territory.Identifier == card.TerritoryIdentifier))
            .FirstOrDefault(territory => territory is not null);
    }

    /// <summary>
    /// The troops a valid set is worth.
    /// </summary>
    /// <param name="tradeNumber">How many sets have been traded in this game before (Progressive only).</param>
    public static int SetValue(CardTradeMode mode, IReadOnlyList<BonusCard> cards, int tradeNumber) =>
        mode switch
        {
            CardTradeMode.Progressive => ProgressiveValue(tradeNumber),
            CardTradeMode.Fixed => FixedValue(cards),
            _ => throw new ArgumentOutOfRangeException(nameof(mode), mode, "Unknown card trade mode."),
        };

    /// <summary>Progressive: every set traded in the game, by anyone, is worth more than the last.</summary>
    private static int ProgressiveValue(int tradeNumber) =>
        tradeNumber switch
        {
            0 => 4,
            1 => 6,
            2 => 8,
            3 => 10,
            4 => 12,
            5 => 15,
            _ => 15 + (tradeNumber - 5) * 5
        };

    /// <summary>
    /// Fixed: 3 infantry = 4, 3 cavalry = 6, 3 artillery = 8, one of each = 10.
    /// Wild cards count as whatever makes the set worth the most.
    /// </summary>
    private static int FixedValue(IReadOnlyList<BonusCard> cards)
    {
        var nonWild = NonWildTypes(cards);
        var options = new List<int>();

        // Three of a kind: every non-wild card has the same type (wilds fill the rest).
        foreach (var type in new[] { BonusCardType.Infantry, BonusCardType.Cavalry, BonusCardType.Artillery })
            if (nonWild.All(cardType => cardType == type))
                options.Add(FixedThreeOfAKindValue(type));

        // One of each: no type appears twice among the non-wild cards (wilds fill the missing types).
        if (nonWild.Distinct().Count() == nonWild.Count)
            options.Add(FixedOneOfEachValue);

        return options.Count > 0
            ? options.Max()
            : throw new InvalidOperationException("The cards do not form a valid set.");
    }

    private static int FixedThreeOfAKindValue(BonusCardType type) =>
        type switch
        {
            BonusCardType.Infantry => 4,
            BonusCardType.Cavalry => 6,
            BonusCardType.Artillery => 8,
            _ => throw new ArgumentOutOfRangeException(nameof(type), type, "Only unit types form three of a kind."),
        };

    private static List<BonusCardType> NonWildTypes(IReadOnlyList<BonusCard> cards) =>
        cards.Where(card => card.Type != BonusCardType.Wild).Select(card => card.Type).ToList();
}

/// <summary>What a card trade gave the player.</summary>
/// <param name="ReserveTroops">The set's value, added to the player's reserve.</param>
/// <param name="BonusTerritoryId">The territory that got the territory bonus, or null when none was given.</param>
/// <param name="BonusTroops">Troops placed directly on <paramref name="BonusTerritoryId"/> (0 when none).</param>
public sealed record CardTradeResult(int ReserveTroops, Guid? BonusTerritoryId, int BonusTroops);
