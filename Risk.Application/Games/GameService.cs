using Risk.Application.Common;
using Risk.Application.Exceptions;
using Risk.Application.Interfaces;
using Risk.Application.Interfaces.Repository;
using Risk.Domain.Entities;

namespace Risk.Application.Games;

/// <summary>Use cases for game sessions and their settings.</summary>
public sealed class GameService(
    IGameRepository games,
    IMapRepository map,
    IUnitOfWork unitOfWork,
    ICurrentUser currentUser,
    GameAccess access)
{
    public async Task<IReadOnlyList<GameSummaryResponse>> GetAllAsync(GameState? state, CancellationToken ct = default)
    {
        var list = await games.ListAsync(state, ct);
        return list.Select(GameSummaryResponse.From).ToList();
    }

    public async Task<GameDetailResponse> GetByIdAsync(Guid id, CancellationToken ct = default) =>
        GameDetailResponse.From(await access.RequireGameAsync(id, ct));

    /// <summary>Creates a new game. The caller becomes host.</summary>
    public async Task<GameDetailResponse> CreateAsync(CreateGameRequest request, CancellationToken ct = default)
    {
        var game = new Game(currentUser.UserId);
        game.ConfigureSettings(
            maxPlayers:    request.MaxPlayers ?? GameSetting.DefaultMaxPlayers,
            timer:         request.Timer ?? 0,
            turnTimeLimit: request.TurnTimeLimit ?? 0,
            hasFogOfWar:   request.HasFogOfWar ?? false,
            hasMissions:   request.HasMissions ?? false,
            blizzardCount: request.BlizzardCount ?? 0);

        games.Add(game);
        await unitOfWork.SaveChangesAsync(ct);
        return GameDetailResponse.From(game);
    }

    /// <summary>Starts the game, lays out the board and places blizzards if the mode is on.</summary>
    public async Task<GameDetailResponse> StartAsync(Guid id, CancellationToken ct = default)
    {
        var game = await access.RequireHostAsync(id, ct);
        var graph = await map.GetGraphAsync(ct);
        return await ApplyAsync(game, g => g.Start(graph, Random.Shared), ct);
    }

    public Task<GameDetailResponse> EndAsync(Guid id, CancellationToken ct = default) =>
        AsHostAsync(id, game => game.End(), ct);

    public Task<GameDetailResponse> SetPhaseAsync(Guid id, SetPhaseRequest request, CancellationToken ct = default) =>
        AsParticipantAsync(id, game => game.SetPhase(request.Phase), ct);

    public Task<GameDetailResponse> SetCurrentPlayerAsync(Guid id, SetCurrentPlayerRequest request, CancellationToken ct = default) =>
        AsParticipantAsync(id, game =>
        {
            GameAccess.RequirePlayerInGame(game, request.PlayerId);
            game.SetCurrentPlayer(request.PlayerId);
        }, ct);

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
            request.HasFogOfWar, request.HasMissions, request.BlizzardCount);

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
        return GameDetailResponse.From(game);
    }
}
