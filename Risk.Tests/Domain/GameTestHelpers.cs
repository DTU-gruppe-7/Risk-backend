using Risk.Domain.Entities;
using Risk.Domain.Map;
using Risk.Tests.Application;

namespace Risk.Tests.Domain;

/// <summary>Shared steps for tests that drive a started game on the classic map.</summary>
internal static class GameTestHelpers
{
    /// <summary>
    /// Starts the game with the players seated in the order they joined, so tests can rely on the
    /// first player to join having the first turn. The board is laid out as with <c>new Random(1)</c>.
    /// </summary>
    public static void StartInJoinOrder(this Game game, MapGraph map) =>
        game.Start(map, new KeepJoinOrderRandom(game.Players.Count));

    /// <summary>
    /// Plays <paramref name="player"/>'s whole turn without attacking: deploys the reserve on their
    /// first territory, then ends Attack and Fortify so the turn passes to the next player.
    /// </summary>
    public static void PlayTurn(this Game game, Player player)
    {
        var territory = game.Territories.First(t => t.OwnerId == player.Id);
        game.DeployTroops(player.Id, territory, player.ReinforcementTroops);
        game.EndPhase(player.Id, GamePhase.Fortify, ClassicMap.Graph, DateTimeOffset.UtcNow);
        game.EndPhase(player.Id, GamePhase.Deployment, ClassicMap.Graph, DateTimeOffset.UtcNow);
    }
}
