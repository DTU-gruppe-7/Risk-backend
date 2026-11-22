using Risk.Domain.Entities;
using Risk.Domain.Map;

namespace Risk.Tests.Domain;

/// <summary>Shared steps for tests that drive a started game on the classic map.</summary>
internal static class GameTestHelpers
{
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
