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
    ICurrentUser currentUser)
{
    public async Task<IReadOnlyList<TerritoryResponse>> GetAllAsync(Guid gameId, Guid? ownerId, CancellationToken ct = default)
    {
        await access.EnsureGameExistsAsync(gameId, ct);
        var list = await territories.GetByGameIdAsync(gameId, ownerId, ct);
        return list.Select(TerritoryResponse.From).ToList();
    }

    public async Task<TerritoryResponse> GetByIdAsync(Guid gameId, Guid id, CancellationToken ct = default) =>
        TerritoryResponse.From(await RequireTerritoryAsync(gameId, id, ct));

    public async Task<TerritoryResponse> ClaimAsync(Guid gameId, Guid id, ClaimTerritoryRequest request, CancellationToken ct = default)
    {
        var game = await access.RequireParticipantAsync(gameId, ct);
        GameAccess.RequirePlayerInGame(game, request.PlayerId);

        return await ChangeAsync(gameId, id, t => t.Claim(request.PlayerId, request.InitialTroops), ct);
    }

    public async Task<TerritoryResponse> UpdateTroopsAsync(Guid gameId, Guid id, UpdateTroopsRequest request, CancellationToken ct = default)
    {
        await access.RequireParticipantAsync(gameId, ct);
        return await ChangeAsync(gameId, id, t => t.AdjustTroops(request.Delta), ct);
    }

    public async Task<TerritoryResponse> DeployTroopsAsync(Guid gameId, Guid id, DeployTroopsRequest request,
        CancellationToken ct = default)
    {
        var game = await access.RequireParticipantWithDetailsAsync(gameId, ct);
        var territory = game.Territories.FirstOrDefault(t => t.Id == id)
                        ?? throw new NotFoundException("Territory", id);

        var player = game.Players.FirstOrDefault(p => p.UserId == currentUser.UserId)
                     ?? throw new ForbiddenException("Only a player can deploy troops.");
        game.DeployTroops(player.Id, territory, request.Troops);

        await unitOfWork.SaveChangesAsync(ct);
        return TerritoryResponse.From(territory);
    }

    private async Task<TerritoryResponse> ChangeAsync(Guid gameId, Guid id, Action<Territory> change, CancellationToken ct)
    {
        var territory = await RequireTerritoryAsync(gameId, id, ct);
        change(territory);
        await unitOfWork.SaveChangesAsync(ct);
        return TerritoryResponse.From(territory);
    }

    private async Task<Territory> RequireTerritoryAsync(Guid gameId, Guid id, CancellationToken ct) =>
        await territories.GetAsync(gameId, id, ct) ?? throw new NotFoundException("Territory", id);
}
