using Risk.Application.Common;
using Risk.Application.Exceptions;
using Risk.Application.Games;
using Risk.Application.Players;
using Risk.Application.Services;
using Risk.Domain.Entities;
using Risk.Domain.Map;

namespace Risk.Tests.Application;

public class GameAndPlayerServiceTests
{
    private readonly Guid _host = Guid.NewGuid();
    private readonly FakeGameRepository _games = new();
    private readonly FakeUnitOfWork _unitOfWork = new();
    private readonly FakeBonusCardRepository _bonusCards = new();
    private readonly FakeCurrentUser _currentUser;
    private readonly GameService _gameService;
    private readonly PlayerService _playerService;

    public GameAndPlayerServiceTests()
    {
        _currentUser = new FakeCurrentUser(_host);
        var access = new GameAccess(_games, _currentUser);
        var fogOfWar = new FogOfWar(_currentUser, new FakeMapRepository());
        _gameService = new GameService(
            _games,
            new FakeMapRepository(),
            new GameDeckSetup(_bonusCards, new FakeMissionCardRepository()),
            _unitOfWork,
            _currentUser,
            access,
            new ForcedCardTradeGuard(_bonusCards),
            new ConquestResolver(_bonusCards, new FakeMissionCardRepository(), new FakeMapRepository()),
            fogOfWar);
        _playerService = new PlayerService(
            new FakePlayerRepository(_games),
            new FakeBonusCardRepository(),
            new FakeMissionCardRepository(),
            _unitOfWork,
            _currentUser,
            access);
    }

    private async Task<GameDetailResponse> CreateGameAsync() =>
        await _gameService.CreateAsync(new CreateGameRequest(4, null, null, null, null));

    [Fact]
    public async Task Create_makes_the_caller_host_and_saves()
    {
        var game = await CreateGameAsync();

        Assert.Equal(_host, game.HostId);
        Assert.Equal(4, game.Settings!.MaxPlayers);
        Assert.Equal(1, _unitOfWork.SaveCount);
    }

    [Fact]
    public async Task The_card_trade_mode_is_chosen_at_creation_and_defaults_to_progressive()
    {
        var standard = await CreateGameAsync();
        var fixedGame = await _gameService.CreateAsync(
            new CreateGameRequest(4, null, null, null, null, CardTradeMode: CardTradeMode.Fixed));

        Assert.Equal(CardTradeMode.Progressive, standard.Settings!.CardTradeMode);
        Assert.Equal(CardTradeMode.Fixed, fixedGame.Settings!.CardTradeMode);

        var updated = await _gameService.UpdateSettingsAsync(standard.Id,
            new UpdateGameSettingRequest(4, 0, 0, false, false, CardTradeMode: CardTradeMode.Fixed));
        Assert.Equal(CardTradeMode.Fixed, updated.CardTradeMode);
    }

    [Fact]
    public async Task Only_the_host_can_start_the_game()
    {
        var game = await CreateGameAsync();
        var player = await _playerService.JoinAsync(game.Id, new JoinGameRequest("red"));
        _currentUser.UserId = Guid.NewGuid();
        await _playerService.JoinAsync(game.Id, new JoinGameRequest("blue"));

        await Assert.ThrowsAsync<ForbiddenException>(() => _gameService.StartAsync(game.Id));

        _currentUser.UserId = _host;
        var started = await _gameService.StartAsync(game.Id);
        Assert.Equal(GameState.InProgress, started.State);
        Assert.Equal(42, _games.Games.Single().Territories.Count);
    }

    /// <summary>Starts a two-player game and leaves the current player in their Attack phase.</summary>
    private async Task<(Game Game, Player Player)> StartGameInAttackPhaseAsync()
    {
        var created = await CreateGameAsync();
        await _playerService.JoinAsync(created.Id, new JoinGameRequest("red"));
        _currentUser.UserId = Guid.NewGuid();
        await _playerService.JoinAsync(created.Id, new JoinGameRequest("blue"));
        _currentUser.UserId = _host;
        await _gameService.StartAsync(created.Id);

        var game = _games.Games.Single();
        var player = game.Players.Single(p => p.Id == game.CurrentPlayerId);
        var territory = game.Territories.First(t => t.OwnerId == player.Id);
        // Placing the last reinforcement moves the game on to the Attack phase.
        game.DeployTroops(player.Id, territory, player.ReinforcementTroops);
        Assert.Equal(GamePhase.Attack, game.CurrentPhase);

        _currentUser.UserId = player.UserId;
        return (game, player);
    }

    [Fact]
    public async Task The_conquest_card_is_handed_out_when_the_turn_ends_not_when_the_attack_phase_ends()
    {
        var (game, player) = await StartGameInAttackPhaseAsync();
        game.RecordConquest(player.Id);

        await _gameService.CompletePhaseAsync(game.Id, new SetPhaseRequest(GamePhase.Fortify));
        Assert.Empty(await _bonusCards.GetByPlayerIdAsync(game.Id, player.Id));

        await _gameService.CompletePhaseAsync(game.Id, new SetPhaseRequest(GamePhase.Deployment));

        Assert.Single(await _bonusCards.GetByPlayerIdAsync(game.Id, player.Id));
        Assert.NotEqual(player.Id, game.CurrentPlayerId);
    }

    [Fact]
    public async Task Fortifying_ends_the_turn_and_hands_out_the_conquest_card()
    {
        var (game, player) = await StartGameInAttackPhaseAsync();
        game.RecordConquest(player.Id);
        await _gameService.CompletePhaseAsync(game.Id, new SetPhaseRequest(GamePhase.Fortify));
        var from = game.Territories.First(t =>
            t.OwnerId == player.Id && t.TroopCount > 1 &&
            ClassicMap.Graph.NeighborsOf(t.Identifier).Any(n => game.Territories.Single(o => o.Identifier == n).OwnerId == player.Id));
        var to = game.Territories.First(t =>
            t.OwnerId == player.Id && ClassicMap.Graph.NeighborsOf(from.Identifier).Contains(t.Identifier));

        await _gameService.FortifyAsync(game.Id, new FortifyRequest(from.Id, to.Id, 1));

        Assert.Single(await _bonusCards.GetByPlayerIdAsync(game.Id, player.Id));
        Assert.NotEqual(player.Id, game.CurrentPlayerId);
    }

    [Fact]
    public async Task A_turn_without_a_conquest_hands_out_no_card()
    {
        var (game, player) = await StartGameInAttackPhaseAsync();

        await _gameService.CompletePhaseAsync(game.Id, new SetPhaseRequest(GamePhase.Fortify));
        await _gameService.CompletePhaseAsync(game.Id, new SetPhaseRequest(GamePhase.Deployment));

        Assert.Empty(await _bonusCards.GetByPlayerIdAsync(game.Id, player.Id));
    }

    [Fact]
    public async Task Getting_the_game_includes_a_pending_conquest()
    {
        var (game, player) = await StartGameInAttackPhaseAsync();
        var from = game.Territories.First(t => t.OwnerId == player.Id);
        from.AddTroops(3);
        var to = game.Territories.First(t => t.OwnerId != player.Id && !t.IsBlizzard);
        to.RemoveTroops(to.TroopCount);
        game.BeginPendingConquest(player.Id, from, to);

        var response = await _gameService.GetByIdAsync(game.Id);

        Assert.Equal(new PendingConquestResponse(from.Id, to.Id, 1, from.TroopCount - 1), response.PendingConquest);
    }

    [Fact]
    public async Task Getting_the_game_includes_the_card_trade_state()
    {
        var (game, player) = await StartGameInAttackPhaseAsync();
        game.ReturnToDeploymentForCardTrade();
        for (var i = 0; i < Game.ForcedCardTradeThreshold; i++)
        {
            var card = new BonusCard(game.Id, BonusCardType.Wild);
            card.AssignToPlayer(player.Id);
            _bonusCards.Add(card);
        }

        var response = await _gameService.GetByIdAsync(game.Id);

        Assert.Equal(0, response.CardSetsTraded);
        Assert.Equal([4, 6, 8], response.UpcomingTradeValues);
        Assert.True(response.Players.Single(p => p.Id == player.Id).MustTradeCards);
        Assert.False(response.Players.Single(p => p.Id != player.Id).MustTradeCards);
    }

    [Fact]
    public async Task Unknown_game_is_not_found()
    {
        await Assert.ThrowsAsync<NotFoundException>(() => _gameService.GetByIdAsync(Guid.NewGuid()));
    }

    [Fact]
    public async Task Join_adds_the_current_user_as_player()
    {
        var game = await CreateGameAsync();
        var user = Guid.NewGuid();
        _currentUser.UserId = user;

        var player = await _playerService.JoinAsync(game.Id, new JoinGameRequest("red"));

        Assert.Equal(user, player.UserId);
        Assert.Equal(1, player.TurnOrder);
    }

    [Fact]
    public async Task Outsiders_cannot_change_a_game()
    {
        var game = await CreateGameAsync();
        _currentUser.UserId = Guid.NewGuid();
        await _playerService.JoinAsync(game.Id, new JoinGameRequest("red"));

        _currentUser.UserId = Guid.NewGuid();

        await Assert.ThrowsAsync<ForbiddenException>(() =>
            _gameService.CompletePhaseAsync(game.Id, new SetPhaseRequest(GamePhase.Attack)));
    }

    [Fact]
    public async Task A_player_can_only_remove_themselves()
    {
        var game = await CreateGameAsync();
        var owner = Guid.NewGuid();
        _currentUser.UserId = owner;
        var player = await _playerService.JoinAsync(game.Id, new JoinGameRequest("red"));

        _currentUser.UserId = Guid.NewGuid();
        await Assert.ThrowsAsync<ForbiddenException>(() => _playerService.LeaveAsync(game.Id, player.Id));

        _currentUser.UserId = owner;
        await _playerService.LeaveAsync(game.Id, player.Id);
        Assert.Empty(_games.Games.Single().Players);
    }

}
