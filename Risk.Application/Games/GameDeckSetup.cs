using Risk.Application.Interfaces.Repository;
using Risk.Domain.Entities;
using Risk.Domain.Map;

namespace Risk.Application.Games;

/// <summary>Stores the card decks a game needs when it starts. What the decks contain is decided in the domain.</summary>
public sealed class GameDeckSetup(
    IBonusCardRepository bonusCards,
    IMissionCardRepository missionCards)
{
    public async Task CreateAsync(
        Game game, IReadOnlyList<MapTerritory> territories, CancellationToken cancellationToken = default)
    {
        if ((await bonusCards.GetByGameIdAsync(game.Id, null, cancellationToken)).Count == 0)
        {
            foreach (var card in BonusDeck.Create(game.Id, territories))
                bonusCards.Add(card);
        }

        if (game.Settings?.HasMissions != true)
            return;

        if ((await missionCards.GetByGameIdAsync(game.Id, null, cancellationToken)).Count > 0)
            return;

        foreach (var mission in MissionDeck.CreateFor(game, territories))
            missionCards.Add(mission);
    }
}
