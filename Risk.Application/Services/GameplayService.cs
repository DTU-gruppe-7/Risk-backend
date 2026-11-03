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
    IBonusCardRepository bonusCards,
    IUnitOfWork unitOfWork,
    IDiceRoller diceRoller,
    ICurrentUser currentUser) : IGameplayService
{
    public async Task<AttackResultDto> AttackAsync(AttackDto dto, CancellationToken cancellationToken = default)
    {
        var playerId = currentUser.UserId;
        var (fromTerritory, toTerritory) = await ValidateAttackPrerequisitesAsync(
            dto.GameId, playerId, dto.FromTerritoryId, dto.ToTerritoryId, cancellationToken);

        if (dto.AttackingTroopsCount is < 1 or > 3)
            throw new ArgumentOutOfRangeException(nameof(dto.AttackingTroopsCount), "Must attack with 1, 2, or 3 troops.");

        if (dto.AttackingTroopsCount >= fromTerritory.TroopCount)
            throw new InvalidOperationException("Originating territory must retain at least 1 troop.");

        if (dto.DefendingTroopsCount is < 1 or > 2 || dto.DefendingTroopsCount > toTerritory.TroopCount)
            throw new ArgumentOutOfRangeException(nameof(dto.DefendingTroopsCount),
                "The defender must choose 1 or 2 troops, up to the number stationed in the territory.");

        int defenderDiceCount = dto.DefendingTroopsCount;
        var attackerDice = diceRoller.Roll(dto.AttackingTroopsCount);
        var defenderDice = diceRoller.Roll(defenderDiceCount);

        var battle = BattleService.Fight(attackerDice, defenderDice);

        if (battle.AttackerLosses > 0)
            fromTerritory.RemoveTroops(battle.AttackerLosses);

        if (battle.DefenderLosses > 0)
            toTerritory.RemoveTroops(battle.DefenderLosses);

        bool conquered = false;
        var troopsMoved = 0;
        var bonusCardAwarded = false;
        if (toTerritory.TroopCount == 0)
        {
            conquered = true;
            var minimumTroopsToMove = dto.AttackingTroopsCount;
            if (dto.MovingTroopsCount is null ||
                dto.MovingTroopsCount < minimumTroopsToMove ||
                dto.MovingTroopsCount > fromTerritory.TroopCount - 1)
                throw new ArgumentOutOfRangeException(nameof(dto.MovingTroopsCount),
                    $"Conquest requires moving between {minimumTroopsToMove} and {fromTerritory.TroopCount - 1} troops.");

            troopsMoved = dto.MovingTroopsCount.Value;
            fromTerritory.RemoveTroops(troopsMoved);
            toTerritory.TransferOwnership(playerId, troopsMoved);
            bonusCardAwarded = await AwardConquestCardAsync(dto.GameId, playerId, cancellationToken);
        }

        await unitOfWork.SaveChangesAsync(cancellationToken);

        return new AttackResultDto(
            AttackerLosses: battle.AttackerLosses,
            DefenderLosses: battle.DefenderLosses,
            AttackerDice: battle.SortedAttackerDice,
            DefenderDice: battle.SortedDefenderDice,
            TerritoryConquered: conquered,
            TroopsMoved: troopsMoved,
            BonusCardAwarded: bonusCardAwarded
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
        var troopsMoved = 0;
        var bonusCardAwarded = false;
        if (conquered)
        {
            if (dto.MovingTroopsCount is null ||
                dto.MovingTroopsCount < 1 ||
                dto.MovingTroopsCount > fromTerritory.TroopCount - 1)
                throw new ArgumentOutOfRangeException(nameof(dto.MovingTroopsCount),
                    $"Conquest requires moving between 1 and {fromTerritory.TroopCount - 1} troops.");

            troopsMoved = dto.MovingTroopsCount.Value;
            fromTerritory.RemoveTroops(troopsMoved);
            toTerritory.TransferOwnership(playerId, troopsMoved);
            bonusCardAwarded = await AwardConquestCardAsync(dto.GameId, playerId, cancellationToken);
        }

        await unitOfWork.SaveChangesAsync(cancellationToken);

        return new BlitzResultDto(
            TotalAttackerLosses: totalAttackerLosses,
            TotalDefenderLosses: totalDefenderLosses,
            TerritoryConquered: conquered,
            RemainingAttackingTerritoryTroops: fromTerritory.TroopCount,
            RemainingDefendingTerritoryTroops: toTerritory.TroopCount,
            Rounds: rounds,
            TroopsMoved: troopsMoved,
            BonusCardAwarded: bonusCardAwarded
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
        var board = game.BoardOn(graph);
        if (!board.CanAttack(fromTerritory.Identifier, toTerritory.Identifier, playerId))
            throw new InvalidOperationException("The attack is not allowed from the selected territories.");

        return (fromTerritory, toTerritory);
    }

    private async Task<bool> AwardConquestCardAsync(Guid gameId, Guid playerId, CancellationToken cancellationToken)
    {
        var game = await gameRepository.GetByIdWithDetailsAsync(gameId, cancellationToken)
                   ?? throw new KeyNotFoundException($"Game {gameId} not found.");
        if (!game.RecordConquest(playerId))
            return false;

        var card = await bonusCards.GetNextUnassignedAsync(gameId, cancellationToken);
        if (card is null)
            return false;

        var player = game.FindPlayer(playerId)
                     ?? throw new InvalidOperationException("Attacking player is not part of the game.");
        card.AssignToPlayer(player.Id);
        return true;
    }
}