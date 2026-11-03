using Risk.Domain.Entities;

namespace Risk.Application.Players;

/// <summary>Full player details.</summary>
/// <param name="Id">Player ID.</param>
/// <param name="GameId">The game this player belongs to.</param>
/// <param name="UserId">Domain user ID.</param>
/// <param name="Color">Board colour.</param>
/// <param name="ReinforcementTroops">Troops available to place.</param>
/// <param name="IsEliminated">Whether the player has been eliminated.</param>
/// <param name="TurnOrder">1-indexed turn order.</param>
/// <param name="JoinedAt">When the player joined.</param>
public sealed record PlayerDetailResponse(
    Guid Id, Guid GameId, Guid UserId, string Color,
    int ReinforcementTroops, bool IsEliminated, int TurnOrder, DateTimeOffset JoinedAt)
{
    internal static PlayerDetailResponse From(Player p) =>
        new(p.Id, p.GameId, p.UserId, p.Color,
            p.ReinforcementTroops, p.IsEliminated, p.TurnOrder, p.JoinedAt);
}

/// <summary>Payload to join a game.</summary>
/// <param name="Color">Requested board colour (e.g. "red", "blue").</param>
public sealed record JoinGameRequest(string Color);
