using Risk.Application.DTOs;
using Risk.Application.Interfaces.Repository;
using Risk.Application.Interfaces.Services;
using Risk.Domain.Entities;
using Risk.Domain.Services;

namespace Risk.Application.Services;

public class GameplayService(
    IGameRepository gameRepository,
    ITerritoryRepository territoryRepository,
    IDiceRoller diceRoller) : IGameplayService
{
    public async Task<AttackResultDto> AttackAsync(AttackDto dto, CancellationToken cancellationToken = default)
    {
        // 1. Hent og valider Game
        var game = await gameRepository.GetByIdAsync(dto.GameId)
            ?? throw new KeyNotFoundException($"Game {dto.GameId} not found.");

        if (game.CurrentPhase != GamePhase.Attack)
            throw new InvalidOperationException("Attacks can only be initiated during the Attack phase.");

        if (game.CurrentPlayerId != dto.PlayerId)
            throw new InvalidOperationException("It is not this player's turn.");

        // 2. Hent og valider territorier
        var fromTerritory = await territoryRepository.GetByIdAsync(dto.FromTerritoryId)
            ?? throw new KeyNotFoundException($"Attacking territory {dto.FromTerritoryId} not found.");

        var toTerritory = await territoryRepository.GetByIdAsync(dto.ToTerritoryId)
            ?? throw new KeyNotFoundException($"Defending territory {dto.ToTerritoryId} not found.");

        if (fromTerritory.OwnerId != dto.PlayerId)
            throw new InvalidOperationException("Attacking player does not own the originating territory.");

        if (toTerritory.OwnerId == dto.PlayerId)
            throw new InvalidOperationException("Cannot attack your own territory.");

        var neighbors = await territoryRepository.GetNeighborsAsync(fromTerritory.Id, cancellationToken);
        if (!neighbors.Any(n => n.Id == toTerritory.Id))
            throw new InvalidOperationException("Target territory is not adjacent to the attacking territory.");

        if (dto.AttackingTroopsCount < 1 || dto.AttackingTroopsCount > 3)
            throw new ArgumentOutOfRangeException(nameof(dto.AttackingTroopsCount), "Must attack with 1, 2, or 3 troops.");

        if (fromTerritory.TroopCount <= dto.AttackingTroopsCount)
            throw new InvalidOperationException("Originating territory must retain at least 1 troop.");

        // 3. Rul terninger via IDiceRoller
        int defenderDiceCount = Math.Min(2, toTerritory.TroopCount);
        var attackerDice = diceRoller.Roll(dto.AttackingTroopsCount);
        var defenderDice = diceRoller.Roll(defenderDiceCount);

        // 4. Afgoer slaget via BattleService
        var battle = BattleService.Fight(attackerDice, defenderDice);

        // 5. Opdater territorierne kun hvis der er faktiske tab (> 0)
        if (battle.AttackerLosses > 0)
        {
            fromTerritory.RemoveTroops(battle.AttackerLosses);
        }

        if (battle.DefenderLosses > 0)
        {
            toTerritory.RemoveTroops(battle.DefenderLosses);
        }

        bool conquered = false;
        if (toTerritory.TroopCount == 0)
        {
            conquered = true;
            int movingTroops = dto.AttackingTroopsCount - battle.AttackerLosses;
            fromTerritory.RemoveTroops(movingTroops);
            toTerritory.TransferOwnership(dto.PlayerId, movingTroops);
        }

        await territoryRepository.UpdateAsync(fromTerritory);
        await territoryRepository.UpdateAsync(toTerritory);
        await territoryRepository.SaveChangesAsync();

        return new AttackResultDto(
            AttackerLosses: battle.AttackerLosses,
            DefenderLosses: battle.DefenderLosses,
            AttackerDice: battle.SortedAttackerDice,
            DefenderDice: battle.SortedDefenderDice,
            TerritoryConquered: conquered
        );
    }
}
