using Risk.Application.Common;
using Risk.Application.Exceptions;
using Risk.Application.Interfaces;
using Risk.Application.Interfaces.Repository;
using Risk.Application.Services;
using Risk.Domain.Entities;
using Risk.Domain.Map;

namespace Risk.Application.Games;

/// <summary>Use cases for game sessions and their settings.</summary>
public sealed class GameService(
    IGameRepository games,
    IMapRepository map,
    GameDeckSetup deckSetup,
    IUnitOfWork unitOfWork,
    ICurrentUser currentUser,
    GameAccess access,
    ForcedCardTradeGuard forcedCardTrade,
    ConquestResolver conquestResolver,
    FogOfWar fogOfWar)
{
    public async Task<IReadOnlyList<GameSummaryResponse>> GetAllAsync(GameState? state, CancellationToken ct = default)
    {
        var list = await games.ListAsync(state, ct);
        return list.Select(GameSummaryResponse.From).ToList();
    }

    public async Task<GameDetailResponse> GetByIdAsync(Guid id, CancellationToken ct = default)
    {
        var game = await access.RequireGameWithDetailsAsync(id, ct);
        if (fogOfWar.IsActive(game) && !game.HasParticipant(currentUser.UserId))
            throw new ForbiddenException("You are not part of this game.");

        return await ToResponseAsync(game, ct);
    }

    /// <summary>Creates a new game. The caller becomes host, and joins it too if a color is given.</summary>
    public async Task<GameDetailResponse> CreateAsync(CreateGameRequest request, CancellationToken ct = default)
    {
        var game = new Game(currentUser.UserId);
        game.ConfigureSettings(
            maxPlayers: request.MaxPlayers ?? GameSetting.DefaultMaxPlayers,
            timer: request.Timer ?? 0,
            turnTimeLimit: request.TurnTimeLimit ?? 0,
            hasFogOfWar: request.HasFogOfWar ?? false,
            hasMissions: request.HasMissions ?? false,
            blizzardCount: request.BlizzardCount ?? 0,
            victoryMode: request.VictoryMode,
            cardTradeMode: request.CardTradeMode);

        // Joining in the same save as the create: either both happen or neither does.
        if (!string.IsNullOrWhiteSpace(request.Color))
            game.AddPlayer(currentUser.UserId, request.Color);

        games.Add(game);
        await unitOfWork.SaveChangesAsync(ct);
        return await ToResponseAsync(game, ct);
    }

    /// <summary>Starts the game, lays out the board and places blizzards if the mode is on.</summary>
    public async Task<GameDetailResponse> StartAsync(Guid id, CancellationToken ct = default)
    {
        var game = await access.RequireHostAsync(id, ct);
        var graph = await map.GetGraphAsync(ct);
        var mapTerritories = await map.GetTerritoriesAsync(ct);
        game.Start(graph, Random.Shared);
        await deckSetup.CreateAsync(game, mapTerritories, ct);

        await unitOfWork.SaveChangesAsync(ct);
        return await ToResponseAsync(game, ct);
    }

    public Task<GameDetailResponse> EndAsync(Guid id, CancellationToken ct = default) =>
        AsHostAsync(id, game => game.End(), ct);

    public async Task<GameDetailResponse> CompletePhaseAsync(Guid id, SetPhaseRequest request, CancellationToken ct = default)
    {
        var game = await access.RequireParticipantWithDetailsAsync(id, ct);
        var graph = await map.GetGraphAsync(ct);
        var player = access.RequireCurrentPlayer(game);
        await forcedCardTrade.EnsureCompletedAsync(game, player.Id, ct);

        await AwardConquestCardIfTurnEndsAsync(game, player.Id, ct);
        game.EndPhase(player.Id, request.Phase, graph, DateTimeOffset.UtcNow);

        await unitOfWork.SaveChangesAsync(ct);
        return await ToResponseAsync(game, ct);
    }

    public async Task<GameDetailResponse> FortifyAsync(Guid id, FortifyRequest request, CancellationToken ct = default)
    {
        var game = await access.RequireParticipantWithDetailsAsync(id, ct);
        var graph = await map.GetGraphAsync(ct);
        var player = access.RequireCurrentPlayer(game);
        var from = game.Territories.FirstOrDefault(t => t.Id == request.FromTerritoryId)
                   ?? throw new NotFoundException("Origin territory", request.FromTerritoryId);
        var to = game.Territories.FirstOrDefault(t => t.Id == request.ToTerritoryId)
                 ?? throw new NotFoundException("Destination territory", request.ToTerritoryId);

        // Fortifying ends the turn.
        await AwardConquestCardIfTurnEndsAsync(game, player.Id, ct);
        game.Fortify(player.Id, from, to, request.Troops, graph, DateTimeOffset.UtcNow);
        await unitOfWork.SaveChangesAsync(ct);
        return await ToResponseAsync(game, ct);
    }

    /// <summary>
    /// The card for conquering is handed out when the turn ends (official rule), which happens from the
    /// Fortify phase. It must be given before the turn passes on, while the game still knows whether the
    /// player conquered. Only for the player whose turn it is, so anyone else gets the usual errors from
    /// the game. If the action then fails, nothing is saved.
    /// </summary>
    private async Task AwardConquestCardIfTurnEndsAsync(Game game, Guid playerId, CancellationToken ct)
    {
        if (game.CurrentPhase == GamePhase.Fortify && game.CurrentPlayerId == playerId)
            await conquestResolver.AwardConquestCardAsync(game, playerId, ct);
    }

    public async Task<GameSettingResponse> GetSettingsAsync(Guid id, CancellationToken ct = default)
    {
        var game = await access.RequireGameAsync(id, ct);
        var settings = game.Settings ?? throw new NotFoundException("Settings for game", id);
        return GameSettingResponse.From(settings);
    }

    public async Task<GameSettingResponse> UpdateSettingsAsync(Guid id, UpdateGameSettingRequest request, CancellationToken ct = default)
    {
        var game = await access.RequireHostAsync(id, ct);
        game.ConfigureSettings(request.MaxPlayers, request.Timer, request.TurnTimeLimit,
            request.HasFogOfWar, request.HasMissions, request.BlizzardCount, request.VictoryMode,
            request.CardTradeMode);

        await unitOfWork.SaveChangesAsync(ct);
        return GameSettingResponse.From(game.Settings!);
    }

    private async Task<GameDetailResponse> AsHostAsync(Guid id, Action<Game> change, CancellationToken ct) =>
        await ApplyAsync(await access.RequireHostAsync(id, ct), change, ct);

    private async Task<GameDetailResponse> AsParticipantAsync(Guid id, Action<Game> change, CancellationToken ct) =>
        await ApplyAsync(await access.RequireParticipantAsync(id, ct), change, ct);

    private async Task<GameDetailResponse> ApplyAsync(Game game, Action<Game> change, CancellationToken ct)
    {
        change(game);
        await unitOfWork.SaveChangesAsync(ct);
        return await ToResponseAsync(game, ct);
    }

    /// <summary>The game as the current user may see it under fog of war.</summary>
    private async Task<GameDetailResponse> ToResponseAsync(Game game, CancellationToken ct) =>
        GameDetailResponse.From(
            game,
            await fogOfWar.VisibleToCurrentUserAsync(game, ct),
            await forcedCardTrade.CurrentPlayerHandSizeAsync(game, ct));

}
