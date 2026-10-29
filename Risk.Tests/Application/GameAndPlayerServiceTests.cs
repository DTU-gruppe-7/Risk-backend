using Risk.Application.Common;
using Risk.Application.Exceptions;
using Risk.Application.Games;
using Risk.Application.Players;
using Risk.Domain.Entities;

namespace Risk.Tests.Application;

public class GameAndPlayerServiceTests
{
    private readonly Guid _host = Guid.NewGuid();
    private readonly FakeGameRepository _games = new();
    private readonly FakeUnitOfWork _unitOfWork = new();
    private readonly FakeCurrentUser _currentUser;
    private readonly GameService _gameService;
    private readonly PlayerService _playerService;

    public GameAndPlayerServiceTests()
    {
        _currentUser = new FakeCurrentUser(_host);
        var access = new GameAccess(_games, _currentUser);
        _gameService = new GameService(_games, new FakeMapRepository(), _unitOfWork, _currentUser, access);
        _playerService = new PlayerService(new FakePlayerRepository(_games), _unitOfWork, _currentUser, access);
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
    public async Task Only_the_host_can_start_the_game()
    {
        var game = await CreateGameAsync();
        await _playerService.JoinAsync(game.Id, new JoinGameRequest("red"));
        _currentUser.UserId = Guid.NewGuid();
        await _playerService.JoinAsync(game.Id, new JoinGameRequest("blue"));

        await Assert.ThrowsAsync<ForbiddenException>(() => _gameService.StartAsync(game.Id));

        _currentUser.UserId = _host;
        var started = await _gameService.StartAsync(game.Id);
        Assert.Equal(GameState.InProgress, started.State);
        Assert.Equal(42, _games.Games.Single().Territories.Count);
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
        var player = await _playerService.JoinAsync(game.Id, new JoinGameRequest("red"));

        _currentUser.UserId = Guid.NewGuid();

        await Assert.ThrowsAsync<ForbiddenException>(() =>
            _playerService.ReinforceAsync(game.Id, player.Id, new ReinforceRequest(3)));
        await Assert.ThrowsAsync<ForbiddenException>(() =>
            _gameService.SetPhaseAsync(game.Id, new SetPhaseRequest(GamePhase.Attack)));
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

    [Fact]
    public async Task SetCurrentPlayer_rejects_a_player_from_another_game()
    {
        var game = await CreateGameAsync();
        await _playerService.JoinAsync(game.Id, new JoinGameRequest("red"));
        _currentUser.UserId = Guid.NewGuid();
        await _playerService.JoinAsync(game.Id, new JoinGameRequest("blue"));
        _currentUser.UserId = _host;
        await _gameService.StartAsync(game.Id);

        await Assert.ThrowsAsync<ValidationFailedException>(() =>
            _gameService.SetCurrentPlayerAsync(game.Id, new SetCurrentPlayerRequest(Guid.NewGuid())));
    }
}
