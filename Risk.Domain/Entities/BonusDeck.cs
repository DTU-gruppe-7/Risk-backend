using Risk.Domain.Map;

namespace Risk.Domain.Entities;

/// <summary>Builds the bonus card deck for a game: one card per territory plus two wild cards.</summary>
public static class BonusDeck
{
    public const int WildCardCount = 2;

    public static IReadOnlyList<BonusCard> Create(Guid gameId, IReadOnlyList<MapTerritory> territories)
    {
        var cards = new List<BonusCard>(territories.Count + WildCardCount);
        for (var index = 0; index < territories.Count; index++)
            cards.Add(new BonusCard(gameId, TypeFor(index), territories[index].Id));

        for (var wild = 0; wild < WildCardCount; wild++)
            cards.Add(new BonusCard(gameId, BonusCardType.Wild));

        return cards;
    }

    /// <summary>The three symbols are dealt round-robin, so they are spread evenly over the deck.</summary>
    private static BonusCardType TypeFor(int index) =>
        (index % 3) switch
        {
            0 => BonusCardType.Infantry,
            1 => BonusCardType.Cavalry,
            _ => BonusCardType.Artillery
        };
}
