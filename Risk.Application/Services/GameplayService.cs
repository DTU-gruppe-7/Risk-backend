using Risk.Application.Common;
using Risk.Application.DTOs;
using Risk.Application.Exceptions;
using Risk.Application.Interfaces;
using Risk.Application.Interfaces.Repository;
using Risk.Application.Interfaces.Services;
using Risk.Domain.Entities;
using Risk.Domain.Exceptions;
using Risk.Domain.Services;

namespace Risk.Application.Services;

public sealed class GameplayService(
    GameAccess access,
    IMapRepository mapRepository,
    IBonusCardRepository bonusCards,
    IMissionCardRepository missionCards,
    IUnitOfWork unitOfWork,
    IDiceRoller diceRoller) : IGameplayService
{
    public async Task<AttackResultDto> AttackAsync(
        AttackDto dto, CancellationToken cancellationToken = default)
    {
        var context = await ValidateAttackPrerequisitesAsync(
            dto.GameId, dto.FromTerritoryId, dto.ToTerritoryId, cancellationToken);

        if (dto.AttackingTroopsCount is < 1 or > 3)
            throw new DomainValidationException("Must attack with 1, 2, or 3 troops.");
        if (dto.AttackingTroopsCount >= context.From.TroopCount)
            throw new BusinessRuleException("Originating territory must retain at least 1 troop.");
        if (dto.DefendingTroopsCount is < 1 or > 2 ||
            dto.DefendingTroopsCount > context.To.TroopCount)
        {
            throw new DomainValidationException(
                "The defender must choose 1 or 2 troops, up to the number stationed in the territory.");
        }

        var attackerDice = diceRoller.Roll(dto.AttackingTroopsCount);
        var defenderDice = diceRoller.Roll(dto.DefendingTroopsCount);
        var battle = BattleService.Fight(attackerDice, defenderDice);

        ApplyBattleLosses(context.From, context.To, battle);

        var conquered = context.To.TroopCount == 0;
        var troopsMoved = 0;
        var bonusCardAwarded = false;
        if (conquered)
        {
            var minimumTroopsToMove = dto.AttackingTroopsCount;
            if (dto.MovingTroopsCount is null ||
                dto.MovingTroopsCount < minimumTroopsToMove ||
                dto.MovingTroopsCount > context.From.TroopCount - 1)
            {
                throw new DomainValidationException(
                    $"Conquest requires moving between {minimumTroopsToMove} and " +
                    $"{context.From.TroopCount - 1} troops.");
            }

            troopsMoved = dto.MovingTroopsCount.Value;
            var defeatedPlayerId = context.To.OwnerId
                                   ?? throw new BusinessRuleException(
                                       "A conquered territory must have an owner.");
            context.From.RemoveTroops(troopsMoved);
            context.To.TransferOwnership(context.PlayerId, troopsMoved);
            await HandleEliminationAsync(
                context.Game, defeatedPlayerId, context.PlayerId, cancellationToken);
            bonusCardAwarded = await AwardConquestCardAsync(
                context.Game, context.PlayerId, cancellationToken);
            await EvaluateVictoryAsync(context.Game, context.PlayerId, cancellationToken);
        }

        await unitOfWork.SaveChangesAsync(cancellationToken);

        return new AttackResultDto(
            AttackerLosses: battle.AttackerLosses,
            DefenderLosses: battle.DefenderLosses,
            AttackerDice: battle.SortedAttackerDice,
            DefenderDice: battle.SortedDefenderDice,
            TerritoryConquered: conquered,
            TroopsMoved: troopsMoved,
            BonusCardAwarded: bonusCardAwarded);
    }

    public async Task<BlitzResultDto> BlitzAttackAsync(
        BlitzAttackDto dto, CancellationToken cancellationToken = default)
    {
        var context = await ValidateAttackPrerequisitesAsync(
            dto.GameId, dto.FromTerritoryId, dto.ToTerritoryId, cancellationToken);

        if (context.From.TroopCount <= 1)
            throw new BusinessRuleException(
                "Originating territory must have at least 2 troops to initiate an attack.");

        var rounds = new List<BlitzRoundDto>();
        var totalAttackerLosses = 0;
        var totalDefenderLosses = 0;

        while (context.From.TroopCount > 1 && context.To.TroopCount > 0)
        {
            var attackerDice = diceRoller.Roll(Math.Min(3, context.From.TroopCount - 1));
            var defenderDice = diceRoller.Roll(Math.Min(2, context.To.TroopCount));
            var battle = BattleService.Fight(attackerDice, defenderDice);

            ApplyBattleLosses(context.From, context.To, battle);
            totalAttackerLosses += battle.AttackerLosses;
            totalDefenderLosses += battle.DefenderLosses;
            rounds.Add(new BlitzRoundDto(
                AttackerDice: battle.SortedAttackerDice,
                DefenderDice: battle.SortedDefenderDice,
                AttackerLosses: battle.AttackerLosses,
                DefenderLosses: battle.DefenderLosses));
        }

        var conquered = context.To.TroopCount == 0;
        var troopsMoved = 0;
        var bonusCardAwarded = false;
        if (conquered)
        {
            if (dto.MovingTroopsCount is null)
            {
                context.Game.BeginPendingConquest(
                    context.PlayerId, context.From, context.To);
            }
            else
            {
                var defeatedPlayerId = context.To.OwnerId
                                       ?? throw new BusinessRuleException(
                                           "A conquered territory must have an owner.");
                CompletePendingConquest(
                    context.Game, context.PlayerId, context.From, context.To,
                    dto.MovingTroopsCount.Value);
                await HandleEliminationAsync(
                    context.Game, defeatedPlayerId, context.PlayerId, cancellationToken);
                troopsMoved = dto.MovingTroopsCount.Value;
                bonusCardAwarded = await AwardConquestCardAsync(
                    context.Game, context.PlayerId, cancellationToken);
                await EvaluateVictoryAsync(context.Game, context.PlayerId, cancellationToken);
            }
        }

        await unitOfWork.SaveChangesAsync(cancellationToken);

        return new BlitzResultDto(
            TotalAttackerLosses: totalAttackerLosses,
            TotalDefenderLosses: totalDefenderLosses,
            TerritoryConquered: conquered,
            RemainingAttackingTerritoryTroops: context.From.TroopCount,
            RemainingDefendingTerritoryTroops: context.To.TroopCount,
            Rounds: rounds,
            TroopsMoved: troopsMoved,
            BonusCardAwarded: bonusCardAwarded);
    }

    public async Task CompleteConquestAsync(
        CompleteConquestDto dto, CancellationToken cancellationToken = default)
    {
        var game = await access.RequireParticipantWithDetailsAsync(
            dto.GameId, cancellationToken);
        var playerId = access.RequireCurrentPlayer(game).Id;
        var from = FindTerritory(game, dto.FromTerritoryId, "Origin territory");
        var to = FindTerritory(game, dto.ToTerritoryId, "Target territory");

        var defeatedPlayerId = to.OwnerId
                               ?? throw new BusinessRuleException(
                                   "A conquered territory must have an owner.");
        CompletePendingConquest(game, playerId, from, to, dto.MovingTroopsCount);
        await HandleEliminationAsync(game, defeatedPlayerId, playerId, cancellationToken);
        await AwardConquestCardAsync(game, playerId, cancellationToken);
        await EvaluateVictoryAsync(game, playerId, cancellationToken);
        await unitOfWork.SaveChangesAsync(cancellationToken);
    }

    private async Task<AttackContext> ValidateAttackPrerequisitesAsync(
        Guid gameId,
        Guid fromTerritoryId,
        Guid toTerritoryId,
        CancellationToken cancellationToken)
    {
        var game = await access.RequireParticipantWithDetailsAsync(
            gameId, cancellationToken);

        if (game.State != GameState.InProgress)
            throw new BusinessRuleException(
                "Attacks can only be initiated while the game is in progress.");
        if (game.CurrentPhase != GamePhase.Attack)
            throw new BusinessRuleException(
                "Attacks can only be initiated during the Attack phase.");

        var player = access.RequireCurrentPlayer(game);
        await access.EnsureForcedCardTradeCompletedAsync(game, player.Id, cancellationToken);
        if (game.CurrentPlayerId != player.Id)
            throw new BusinessRuleException("It is not this player's turn.");

        var from = FindTerritory(game, fromTerritoryId, "Attacking territory");
        var to = FindTerritory(game, toTerritoryId, "Defending territory");
        if (from.OwnerId != player.Id)
            throw new BusinessRuleException(
                "Attacking player does not own the originating territory.");
        if (to.OwnerId == player.Id)
            throw new BusinessRuleException("Cannot attack your own territory.");

        var graph = await mapRepository.GetGraphAsync(cancellationToken);
        if (!game.BoardOn(graph).CanAttack(from.Identifier, to.Identifier, player.Id))
            throw new BusinessRuleException(
                "The attack is not allowed from the selected territories.");

        return new AttackContext(game, from, to, player.Id);
    }

    private async Task<bool> AwardConquestCardAsync(
        Game game, Guid playerId, CancellationToken cancellationToken)
    {
        if (!game.RecordConquest(playerId))
            return false;

        var card = await bonusCards.GetNextUnassignedAsync(
            game.Id, cancellationToken);
        if (card is null)
            return false;

        var player = game.FindPlayer(playerId)
                     ?? throw new BusinessRuleException(
                         "Attacking player is not part of the game.");
        card.AssignToPlayer(player.Id);
        return true;
    }

    private async Task HandleEliminationAsync(
        Game game,
        Guid defeatedPlayerId,
        Guid attackerId,
        CancellationToken cancellationToken)
    {
        if (!game.EliminatePlayer(defeatedPlayerId))
            return;

        var defeatedCards = await bonusCards.GetByPlayerIdAsync(
            game.Id, defeatedPlayerId, cancellationToken);
        foreach (var card in defeatedCards)
            card.AssignToPlayer(attackerId);

        var mission = await missionCards.GetByPlayerIdAsync(
            game.Id, defeatedPlayerId, cancellationToken);
        mission?.ReleaseFromPlayer();
    }

    private async Task EvaluateVictoryAsync(
        Game game, Guid playerId, CancellationToken cancellationToken)
    {
        if (game.ControlsAllTerritories(playerId))
        {
            game.FinishWithWinner(playerId);
            return;
        }

        if (game.Settings?.HasMissions != true)
            return;

        var mission = await missionCards.GetByPlayerIdAsync(
            game.Id, playerId, cancellationToken);
        if (mission is null || mission.IsCompleted)
            return;

        var map = await mapRepository.GetGraphAsync(cancellationToken);
        if (!mission.IsSatisfiedBy(game, map, playerId))
            return;

        mission.Complete(game, map);
        game.FinishWithWinner(playerId);
    }

    private static void ApplyBattleLosses(
        Territory from, Territory to, BattleResult battle)
    {
        if (battle.AttackerLosses > 0)
            from.RemoveTroops(battle.AttackerLosses);
        if (battle.DefenderLosses > 0)
            to.RemoveTroops(battle.DefenderLosses);
    }

    private static Territory FindTerritory(
        Game game, Guid territoryId, string name) =>
        game.Territories.FirstOrDefault(territory => territory.Id == territoryId)
        ?? throw new NotFoundException(name, territoryId);

    private static void CompletePendingConquest(
        Game game, Guid playerId, Territory from, Territory to, int troops) =>
        game.CompletePendingConquest(playerId, from, to, troops);

    private sealed record AttackContext(
        Game Game, Territory From, Territory To, Guid PlayerId);
}
