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
    
    public Task<AttackResultDto> AttackAsync(AttackDto dto)
    {
        throw new NotImplementedException();
    }
}