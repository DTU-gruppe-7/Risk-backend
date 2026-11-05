using Risk.Domain.Map;

namespace Risk.Domain.Entities;

/// <summary>Builds the secret missions of a game and deals one to every player.</summary>
public static class MissionDeck
{
    /// <summary>
    /// Every player gets a "control a continent" mission. The continents are handed out in turn order
    /// and reused if there are more players than continents.
    /// </summary>
    public static IReadOnlyList<MissionCard> CreateFor(Game game, IReadOnlyList<MapTerritory> territories)
    {
        var continents = territories
            .Select(territory => territory.ContinentId)
            .Distinct(StringComparer.Ordinal)
            .OrderBy(id => id, StringComparer.Ordinal)
            .ToList();
        if (continents.Count == 0)
            return [];

        var players = game.Players.OrderBy(player => player.TurnOrder).ToList();
        var missions = new List<MissionCard>(players.Count);
        for (var index = 0; index < players.Count; index++)
        {
            var continent = continents[index % continents.Count];
            var mission = new MissionCard(
                game.Id,
                $"Control {continent}",
                $"Control every territory in {continent}.",
                MissionObjectiveType.ControlContinent,
                continent);
            mission.AssignToPlayer(players[index].Id);
            missions.Add(mission);
        }

        return missions;
    }
}
