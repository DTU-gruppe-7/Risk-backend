using Risk.Application.Common;
using Risk.Application.Exceptions;
using Risk.Application.Interfaces;
using Risk.Application.Interfaces.Repository;
using Risk.Domain.Entities;

namespace Risk.Application.Territories;

/// <summary>Use cases for the board: reading territories, claiming them and moving troops.</summary>
public sealed class TerritoryService(
    ITerritoryRepository territories,
    IUnitOfWork unitOfWork,
    GameAccess access,
    ICurrentUser currentUser,
    FogOfWar fogOfWar,
    ForcedCardTradeGuard forcedCardTrade)
{
    public async Task<IReadOnlyList<TerritoryResponse>> GetAllAsync(Guid gameId, Guid? ownerId, CancellationToken ct = default)
    {
        var game = await access.RequireParticipantWithDetailsAsync(gameId, ct);
        var list = await territories.GetByGameIdAsync(gameId, ownerId, ct);
        var visible = await fogOfWar.VisibleTerritoriesAsync(game, ct);
        if (visible is not null)
            list = list.Where(territory => visible.Contains(territory.Identifier)).ToList();

        return list.Select(TerritoryResponse.From).ToList();
    }

    public async Task<TerritoryResponse> GetByIdAsync(Guid gameId, Guid id, CancellationToken ct = default)
    {
        var game = await access.RequireParticipantWithDetailsAsync(gameId, ct);
        var territory = await RequireTerritoryAsync(gameId, id, ct);
        var visible = await fogOfWar.VisibleTerritoriesAsync(game, ct);
        if (visible is not null && !visible.Contains(territory.Identifier))
            throw new NotFoundException("Territory", id);

        return TerritoryResponse.From(territory);
    }

    public async Task<TerritoryResponse> DeployTroopsAsync(Guid gameId, Guid id, DeployTroopsRequest request,
        CancellationToken ct = default)
    {
        var game = await access.RequireParticipantWithDetailsAsync(gameId, ct);
        var territory = game.Territories.FirstOrDefault(t => t.Id == id)
                        ?? throw new NotFoundException("Territory", id);

        var player = game.Players.FirstOrDefault(p => p.UserId == currentUser.UserId)
                     ?? throw new ForbiddenException("Only a player can deploy troops.");
        await forcedCardTrade.EnsureCompletedAsync(game, player.Id, ct);
        game.DeployTroops(player.Id, territory, request.Troops);

        await unitOfWork.SaveChangesAsync(ct);
        return TerritoryResponse.From(territory);
    }

    private async Task<Territory> RequireTerritoryAsync(Guid gameId, Guid id, CancellationToken ct) =>
        await territories.GetAsync(gameId, id, ct) ?? throw new NotFoundException("Territory", id);
}
