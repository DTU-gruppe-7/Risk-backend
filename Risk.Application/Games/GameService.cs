using Risk.Application.Common;
using Risk.Application.Exceptions;
using Risk.Application.Interfaces;
using Risk.Application.Interfaces.Repository;
using Risk.Domain.Entities;
using Risk.Domain.Map;

namespace Risk.Application.Games;

/// <summary>Use cases for game sessions and their settings.</summary>
public sealed class GameService(
    IGameRepository games,
    IMapRepository map,
    IBonusCardRepository bonusCards,
    IMissionCardRepository missionCards,
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
            blizzardCount: request.BlizzardCount ?? 0,
            victoryMode:   request.VictoryMode);

        games.Add(game);
        await unitOfWork.SaveChangesAsync(ct);
        return GameDetailResponse.From(game);
    }

    /// <summary>Starts the game, lays out the board and places blizzards if the mode is on.</summary>
    public async Task<GameDetailResponse> StartAsync(Guid id, CancellationToken ct = default)
    {
        var game = await access.RequireHostAsync(id, ct);
        var graph = await map.GetGraphAsync(ct);
        var mapTerritories = await map.GetTerritoriesAsync(ct);
        game.Start(graph, Random.Shared);
        var existingCards = await bonusCards.GetByGameIdAsync(id, null, ct);
        if (existingCards.Count == 0)
        {
            for (var index = 0; index < mapTerritories.Count; index++)
            {
                var territory = mapTerritories[index];
                bonusCards.Add(new BonusCard(id, CardTypeFor(index), territory.Id));
            }

            bonusCards.Add(new BonusCard(id, BonusCardType.Wild));
            bonusCards.Add(new BonusCard(id, BonusCardType.Wild));
        }

        if (game.Settings?.HasMissions == true)
            await CreateAndAssignMissionsAsync(game, mapTerritories, ct);

        await unitOfWork.SaveChangesAsync(ct);
        return GameDetailResponse.From(game);
    }

    public Task<GameDetailResponse> EndAsync(Guid id, CancellationToken ct = default) =>
        AsHostAsync(id, game => game.End(), ct);

    public async Task<GameDetailResponse> CompletePhaseAsync(Guid id, SetPhaseRequest request, CancellationToken ct = default)
    {
        var game = await access.RequireParticipantWithDetailsAsync(id, ct);
        var graph = await map.GetGraphAsync(ct);
        var player = access.RequireCurrentPlayer(game);
        await access.EnsureForcedCardTradeCompletedAsync(game, player.Id, ct);

        game.EndPhase(player.Id, request.Phase, graph, DateTimeOffset.UtcNow);
        await unitOfWork.SaveChangesAsync(ct);
        return GameDetailResponse.From(game);
    }

    public async Task<GameDetailResponse> FortifyAsync(Guid id, FortifyRequest request, CancellationToken ct = default)
    {
        var game = await access.RequireParticipantWithDetailsAsync(id, ct);
        var graph = await map.GetGraphAsync(ct);
        var player = access.RequireCurrentPlayer(game);
        await access.EnsureForcedCardTradeCompletedAsync(game, player.Id, ct);
        var from = game.Territories.FirstOrDefault(t => t.Id == request.FromTerritoryId)
                   ?? throw new NotFoundException("Origin territory", request.FromTerritoryId);
        var to = game.Territories.FirstOrDefault(t => t.Id == request.ToTerritoryId)
                 ?? throw new NotFoundException("Destination territory", request.ToTerritoryId);

        game.Fortify(player.Id, from, to, request.Troops, graph, DateTimeOffset.UtcNow);
        await unitOfWork.SaveChangesAsync(ct);
        return GameDetailResponse.From(game);
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
            request.HasFogOfWar, request.HasMissions, request.BlizzardCount, request.VictoryMode);

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

    private static BonusCardType CardTypeFor(int index) =>
        (index % 3) switch
        {
            0 => BonusCardType.Infantry,
            1 => BonusCardType.Cavalry,
            _ => BonusCardType.Artillery
        };

    private async Task CreateAndAssignMissionsAsync(
        Game game, IReadOnlyList<MapTerritory> mapTerritories, CancellationToken ct)
    {
        var existingMissions = await missionCards.GetByGameIdAsync(game.Id, null, ct);
        if (existingMissions.Count > 0)
            return;

        var continents = mapTerritories
            .Select(territory => territory.ContinentId)
            .Distinct(StringComparer.Ordinal)
            .OrderBy(id => id, StringComparer.Ordinal)
            .ToList();
        var players = game.Players.OrderBy(player => player.TurnOrder).ToList();

        for (var index = 0; index < players.Count; index++)
        {
            var player = players[index];
            var continent = continents[index % continents.Count];
            var mission = new MissionCard(
                game.Id,
                $"Control {continent}",
                $"Control every territory in {continent}.",
                MissionObjectiveType.ControlContinent,
                continent);
            mission.AssignToPlayer(player.Id);
            missionCards.Add(mission);
        }
    }
}
