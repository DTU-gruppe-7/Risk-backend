using Risk.Application.DTOs.Gameplay;
using Risk.Application.Interfaces.Repository;
using Risk.Application.Interfaces.Services;
using Risk.Domain.Entities;

namespace Risk.Application.Services;

public class GameplayService(
    IGameRepository gameRepository,
    IPlayerRepository playerRepository,
    ITerritoryRepository territoryRepository) : IGameplayService
{
    private static readonly Random _random = new();
    public async Task PlaceTroopsAsync(PlaceTroopsDto dto, CancellationToken cancellationToken = default)
    {
        if (dto.TroopCount <= 0)
            throw new ArgumentException("Troop count must be greater than zero.", nameof(dto.TroopCount));

        var game = await gameRepository.GetByIdAsync(dto.GameId)
            ?? throw new KeyNotFoundException($"Game '{dto.GameId}' not found.");

        ValidateTurnAndPhase(game, dto.PlayerId, GamePhase.Deployment);

        var player = await playerRepository.GetByIdAsync(dto.PlayerId)
            ?? throw new KeyNotFoundException($"Player '{dto.PlayerId}' not found.");

        var territory = await territoryRepository.GetByIdAsync(dto.TerritoryId)
            ?? throw new KeyNotFoundException($"Territory '{dto.TerritoryId}' not found.");

        if (territory.GameId != game.Id)
            throw new InvalidOperationException("Territory does not belong to this game.");

        if (territory.OwnerId != player.Id)
            throw new InvalidOperationException("Player does not own this territory.");

        // Domænemetoder eksekveres og validerer interne tilstande
        player.PlaceTroops(dto.TroopCount);
        territory.AddTroops(dto.TroopCount);

        await playerRepository.UpdateAsync(player);
        await territoryRepository.UpdateAsync(territory);
    }

    public async Task FortifyTroopsAsync(FortifyTroopsDto dto, CancellationToken cancellationToken = default)
    {
        if (dto.TroopCount <= 0)
            throw new ArgumentException("Troop count must be greater than zero.", nameof(dto.TroopCount));

        if (dto.FromTerritoryId == dto.ToTerritoryId)
            throw new InvalidOperationException("Cannot move troops to the same territory.");

        var game = await gameRepository.GetByIdAsync(dto.GameId)
            ?? throw new KeyNotFoundException($"Game '{dto.GameId}' not found.");

        ValidateTurnAndPhase(game, dto.PlayerId, GamePhase.Fortify);

        var fromTerritory = await territoryRepository.GetByIdAsync(dto.FromTerritoryId)
            ?? throw new KeyNotFoundException($"Source territory '{dto.FromTerritoryId}' not found.");

        var toTerritory = await territoryRepository.GetByIdAsync(dto.ToTerritoryId)
            ?? throw new KeyNotFoundException($"Destination territory '{dto.ToTerritoryId}' not found.");

        if (fromTerritory.GameId != game.Id || toTerritory.GameId != game.Id)
            throw new InvalidOperationException("Both territories must belong to this game.");

        if (fromTerritory.OwnerId != dto.PlayerId || toTerritory.OwnerId != dto.PlayerId)
            throw new InvalidOperationException("Player must own both source and destination territories.");

        if (fromTerritory.TroopCount - dto.TroopCount < 1)
            throw new InvalidOperationException("At least 1 troop must remain on the source territory.");

        // Valider at de to territorier er naboer
        var neighbors = await territoryRepository.GetNeighborsAsync(fromTerritory.Id);
        if (!neighbors.Any(n => n.Id == toTerritory.Id))
            throw new InvalidOperationException("Territories are not adjacent.");

        fromTerritory.RemoveTroops(dto.TroopCount);
        toTerritory.AddTroops(dto.TroopCount);

        await territoryRepository.UpdateAsync(fromTerritory);
        await territoryRepository.UpdateAsync(toTerritory);
    }

    private static void ValidateTurnAndPhase(Game game, Guid playerId, GamePhase expectedPhase)
    {
        if (game.State != GameState.InProgress)
            throw new InvalidOperationException($"Game is not in progress (Current state: {game.State}).");

        if (game.CurrentPlayerId != playerId)
            throw new InvalidOperationException("It is not this player's turn.");

        if (game.CurrentPhase != expectedPhase)
            throw new InvalidOperationException($"Action invalid for current game phase. Expected: {expectedPhase}, Actual: {game.CurrentPhase}.");
    }
    
    public async Task<AttackResultDto> AttackAsync(AttackDto dto, CancellationToken cancellationToken = default)
    {
        if (dto.FromTerritoryId == dto.ToTerritoryId)
            throw new InvalidOperationException("Cannot attack your own territory.");

        var game = await gameRepository.GetByIdAsync(dto.GameId)
            ?? throw new KeyNotFoundException($"Game '{dto.GameId}' not found.");

        ValidateTurnAndPhase(game, dto.PlayerId, GamePhase.Attack);

        var fromTerritory = await territoryRepository.GetByIdAsync(dto.FromTerritoryId)
            ?? throw new KeyNotFoundException($"Source territory '{dto.FromTerritoryId}' not found.");

        var toTerritory = await territoryRepository.GetByIdAsync(dto.ToTerritoryId)
            ?? throw new KeyNotFoundException($"Target territory '{dto.ToTerritoryId}' not found.");

        if (fromTerritory.GameId != game.Id || toTerritory.GameId != game.Id)
            throw new InvalidOperationException("Both territories must belong to this game.");

        if (fromTerritory.OwnerId != dto.PlayerId)
            throw new InvalidOperationException("Player does not own source territory.");

        if (toTerritory.OwnerId == dto.PlayerId)
            throw new InvalidOperationException("Cannot attack your own territory.");

        if (fromTerritory.TroopCount < 2)
            throw new InvalidOperationException("Must have at least 2 troops on source territory to attack.");

        if (dto.AttackingTroopsCount < 1 || dto.AttackingTroopsCount > 3)
            throw new InvalidOperationException("Attacking troops must be between 1 and 3.");

        if (dto.AttackingTroopsCount >= fromTerritory.TroopCount)
            throw new InvalidOperationException("At least 1 troop must remain on source territory.");

        var neighbors = await territoryRepository.GetNeighborsAsync(fromTerritory.Id);
        if (!neighbors.Any(n => n.Id == toTerritory.Id))
            throw new InvalidOperationException("Territories are not adjacent.");

        // 1. Rul terninger
        int defenderDiceCount = Math.Min(2, toTerritory.TroopCount);

        var attackerDice = Enumerable.Range(0, dto.AttackingTroopsCount)
            .Select(_ => _random.Next(1, 7))
            .OrderByDescending(d => d)
            .ToList();

        var defenderDice = Enumerable.Range(0, defenderDiceCount)
            .Select(_ => _random.Next(1, 7))
            .OrderByDescending(d => d)
            .ToList();

        // 2. Sammenlign parvise terninger
        int comparisons = Math.Min(attackerDice.Count, defenderDice.Count);
        int attackerLosses = 0;
        int defenderLosses = 0;

        for (int i = 0; i < comparisons; i++)
        {
            if (attackerDice[i] > defenderDice[i])
                defenderLosses++;
            else
                attackerLosses++; // Defender vinder uafgjort
        }

        fromTerritory.RemoveTroops(attackerLosses);
        toTerritory.RemoveTroops(defenderLosses);

        // 3. Erobring (Conquest)
        bool conquered = toTerritory.TroopCount == 0;
        if (conquered)
        {
            int survivingAttackingTroops = dto.AttackingTroopsCount - attackerLosses;
            fromTerritory.RemoveTroops(survivingAttackingTroops);
            toTerritory.Claim(dto.PlayerId, survivingAttackingTroops);
        }

        await territoryRepository.UpdateAsync(fromTerritory);
        await territoryRepository.UpdateAsync(toTerritory);

        return new AttackResultDto(
            conquered,
            attackerLosses,
            defenderLosses,
            attackerDice,
            defenderDice
        );
    }
}