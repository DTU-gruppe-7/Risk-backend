using Risk.Application.DTOs;
using Risk.Application.Interfaces;
using Risk.Application.Interfaces.Repository;
using Risk.Application.Interfaces.Services;
using Risk.Domain.Entities;
using Risk.Domain.Services;

namespace Risk.Application.Services;

public class GameplayService(
    IGameRepository gameRepository,
    ITerritoryRepository territoryRepository,
    IMapRepository mapRepository,
    IUnitOfWork unitOfWork,
    IDiceRoller diceRoller,
    ICurrentUser currentUser) : IGameplayService
{
    public async Task<AttackResultDto> AttackAsync(AttackDto dto, CancellationToken cancellationToken = default)
    {
        var playerId = currentUser.UserId;
        var (fromTerritory, toTerritory) = await ValidateAttackPrerequisitesAsync(
            dto.GameId, playerId, dto.FromTerritoryId, dto.ToTerritoryId, cancellationToken);

        if (dto.AttackingTroopsCount < 1 || dto.AttackingTroopsCount > 3)
            throw new ArgumentOutOfRangeException(nameof(dto.AttackingTroopsCount), "Must attack with 1, 2, or 3 troops.");

        if (fromTerritory.TroopCount <= dto.AttackingTroopsCount)
            throw new InvalidOperationException("Originating territory must retain at least 1 troop.");

        int defenderDiceCount = Math.Min(2, toTerritory.TroopCount);
        var attackerDice = diceRoller.Roll(dto.AttackingTroopsCount);
        var defenderDice = diceRoller.Roll(defenderDiceCount);

        var battle = BattleService.Fight(attackerDice, defenderDice);

        if (battle.AttackerLosses > 0)
            fromTerritory.RemoveTroops(battle.AttackerLosses);

        if (battle.DefenderLosses > 0)
            toTerritory.RemoveTroops(battle.DefenderLosses);

        bool conquered = false;
        if (toTerritory.TroopCount == 0)
        {
            conquered = true;
            // Rettet: - tegn tilføjet
            int movingTroops = dto.AttackingTroopsCount - battle.AttackerLosses;
            fromTerritory.RemoveTroops(movingTroops);
            toTerritory.TransferOwnership(playerId, movingTroops);
        }

        await unitOfWork.SaveChangesAsync(cancellationToken);

        return new AttackResultDto(
            AttackerLosses: battle.AttackerLosses,
            DefenderLosses: battle.DefenderLosses,
            AttackerDice: battle.SortedAttackerDice,
            DefenderDice: battle.SortedDefenderDice,
            TerritoryConquered: conquered
        );
    }

    public async Task<BlitzResultDto> BlitzAttackAsync(BlitzAttackDto dto, CancellationToken cancellationToken = default)
    {
        var playerId = currentUser.UserId;
        var (fromTerritory, toTerritory) = await ValidateAttackPrerequisitesAsync(
            dto.GameId, playerId, dto.FromTerritoryId, dto.ToTerritoryId, cancellationToken);

        if (fromTerritory.TroopCount <= 1)
            throw new InvalidOperationException("Originating territory must have at least 2 troops to initiate an attack.");

        var rounds = new List<BlitzRoundDto>();
        int totalAttackerLosses = 0;
        int totalDefenderLosses = 0;

        while (fromTerritory.TroopCount > 1 && toTerritory.TroopCount > 0)
        {
            // Rettet: - tegn tilføjet
            int attackerDiceCount = Math.Min(3, fromTerritory.TroopCount - 1);
            int defenderDiceCount = Math.Min(2, toTerritory.TroopCount);

            var attackerDice = diceRoller.Roll(attackerDiceCount);
            var defenderDice = diceRoller.Roll(defenderDiceCount);

            var battle = BattleService.Fight(attackerDice, defenderDice);

            if (battle.AttackerLosses > 0)
                fromTerritory.RemoveTroops(battle.AttackerLosses);

            if (battle.DefenderLosses > 0)
                toTerritory.RemoveTroops(battle.DefenderLosses);

            totalAttackerLosses += battle.AttackerLosses;
            totalDefenderLosses += battle.DefenderLosses;

            rounds.Add(new BlitzRoundDto(
                AttackerDice: battle.SortedAttackerDice,
                DefenderDice: battle.SortedDefenderDice,
                AttackerLosses: battle.AttackerLosses,
                DefenderLosses: battle.DefenderLosses
            ));
        }

        bool conquered = toTerritory.TroopCount == 0;
        if (conquered)
        {
            // Rettet: - tegn tilføjet
            int movingTroops = fromTerritory.TroopCount - 1;
            fromTerritory.RemoveTroops(movingTroops);
            toTerritory.TransferOwnership(playerId, movingTroops);
        }

        await unitOfWork.SaveChangesAsync(cancellationToken);

        return new BlitzResultDto(
            TotalAttackerLosses: totalAttackerLosses,
            TotalDefenderLosses: totalDefenderLosses,
            TerritoryConquered: conquered,
            RemainingAttackingTerritoryTroops: fromTerritory.TroopCount,
            RemainingDefendingTerritoryTroops: toTerritory.TroopCount,
            Rounds: rounds
        );
    }

    private async Task<(Territory From, Territory To)> ValidateAttackPrerequisitesAsync(
        Guid gameId,
        Guid playerId,
        Guid fromTerritoryId,
        Guid toTerritoryId,
        CancellationToken cancellationToken)
    {
        var game = await gameRepository.GetByIdWithDetailsAsync(gameId, cancellationToken)
            ?? throw new KeyNotFoundException($"Game {gameId} not found.");

        if (game.State != GameState.InProgress)
            throw new InvalidOperationException("Attacks can only be initiated while the game is in progress.");
        if (game.CurrentPhase != GamePhase.Attack)
            throw new InvalidOperationException("Attacks can only be initiated during the Attack phase.");

        if (game.CurrentPlayerId != playerId)
            throw new InvalidOperationException("It is not this player's turn.");

        var fromTerritory = await territoryRepository.GetByIdAsync(fromTerritoryId)
            ?? throw new KeyNotFoundException($"Attacking territory {fromTerritoryId} not found.");

        var toTerritory = await territoryRepository.GetByIdAsync(toTerritoryId)
            ?? throw new KeyNotFoundException($"Defending territory {toTerritoryId} not found.");

        if (fromTerritory.OwnerId != playerId)
            throw new InvalidOperationException("Attacking player does not own the originating territory.");

        if (toTerritory.OwnerId == playerId)
            throw new InvalidOperationException("Cannot attack your own territory.");
        if (fromTerritory.GameId != game.Id || toTerritory.GameId != game.Id)
            throw new InvalidOperationException("Both territories must belong to the game.");

        var graph = await mapRepository.GetGraphAsync(cancellationToken);
        var neighbors = graph.NeighborsOf(fromTerritory.Identifier);

        if (!neighbors.Contains(toTerritory.Identifier))
            throw new InvalidOperationException("Target territory is not adjacent to the attacking territory.");

        return (fromTerritory, toTerritory);
    }
}