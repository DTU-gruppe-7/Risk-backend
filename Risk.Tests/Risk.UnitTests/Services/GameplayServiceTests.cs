using FluentAssertions;
using NSubstitute;
using Risk.Application.DTOs.Gameplay;
using Risk.Application.Interfaces.Repository;
using Risk.Application.Services;
using Risk.Domain.Entities;
using Xunit;

namespace Risk.Tests.Risk.UnitTests.Services;

public class GameplayServiceTests
{
    private readonly IGameRepository _gameRepo = Substitute.For<IGameRepository>();
    private readonly IPlayerRepository _playerRepo = Substitute.For<IPlayerRepository>();
    private readonly ITerritoryRepository _territoryRepo = Substitute.For<ITerritoryRepository>();
    private readonly GameplayService _sut;

    public GameplayServiceTests()
    {
        _sut = new GameplayService(_gameRepo, _playerRepo, _territoryRepo);
    }

    // ========================================== //
    //              PLACE TROOPS TESTS            //
    // ========================================== //

    [Fact]
    public async Task PlaceTroops_ValidInput_AddsTroopsAndDecrementsPlayerReinforcements()
    {
        var (game, player, territory) = CreateValidGameSetup(GamePhase.Deployment);
        player.AddReinforcements(5);
        territory.Claim(player.Id, 2);

        SetupRepositories(game, player, territory);

        var dto = new PlaceTroopsDto(game.Id, player.Id, territory.Id, 3);
        await _sut.PlaceTroopsAsync(dto);

        territory.TroopCount.Should().Be(5);
        player.ReinforcementTroops.Should().Be(2);
        await _territoryRepo.Received(1).UpdateAsync(territory);
        await _playerRepo.Received(1).UpdateAsync(player);
    }

    [Fact]
    public async Task PlaceTroops_WhenNotCurrentPlayerTurn_ThrowsInvalidOperationException()
    {
        var (game, player, territory) = CreateValidGameSetup(GamePhase.Deployment);
        game.SetCurrentPlayer(Guid.NewGuid()); // En anden spillers tur

        SetupRepositories(game, player, territory);

        var dto = new PlaceTroopsDto(game.Id, player.Id, territory.Id, 1);
        var act = () => _sut.PlaceTroopsAsync(dto);

        await act.Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("*not this player's turn*");
    }

    [Fact]
    public async Task PlaceTroops_WhenNotInDeploymentPhase_ThrowsInvalidOperationException()
    {
        var (game, player, territory) = CreateValidGameSetup(GamePhase.Attack);

        SetupRepositories(game, player, territory);

        var dto = new PlaceTroopsDto(game.Id, player.Id, territory.Id, 1);
        var act = () => _sut.PlaceTroopsAsync(dto);

        await act.Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("*Action invalid for current game phase*");
    }

    [Fact]
    public async Task PlaceTroops_WhenPlayerDoesNotOwnTerritory_ThrowsInvalidOperationException()
    {
        var (game, player, territory) = CreateValidGameSetup(GamePhase.Deployment);
        var opponentId = Guid.NewGuid();
        territory.Claim(opponentId, 2); // Modstander ejer feltet

        SetupRepositories(game, player, territory);

        var dto = new PlaceTroopsDto(game.Id, player.Id, territory.Id, 1);
        var act = () => _sut.PlaceTroopsAsync(dto);

        await act.Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("*Player does not own this territory*");
    }

    [Fact]
    public async Task PlaceTroops_WhenExceedingReinforcementTroops_ThrowsInvalidOperationException()
    {
        var (game, player, territory) = CreateValidGameSetup(GamePhase.Deployment);
        player.AddReinforcements(2);
        territory.Claim(player.Id, 1);

        SetupRepositories(game, player, territory);

        // Forsøger at placere 5 tropper men har kun 2
        var dto = new PlaceTroopsDto(game.Id, player.Id, territory.Id, 5);
        var act = () => _sut.PlaceTroopsAsync(dto);

        await act.Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("*Invalid troop placement amount*");
    }

    // ========================================== //
    //             FORTIFY TROOPS TESTS           //
    // ========================================== //

    [Fact]
    public async Task FortifyTroops_ValidMoveBetweenNeighbors_MovesTroopsCorrectly()
    {
        var (game, player, fromTerritory, toTerritory) = CreateFortifySetup();
        fromTerritory.Claim(player.Id, 4);
        toTerritory.Claim(player.Id, 1);

        SetupFortifyRepositories(game, fromTerritory, toTerritory, areNeighbors: true);

        var dto = new FortifyTroopsDto(game.Id, player.Id, fromTerritory.Id, toTerritory.Id, 2);
        await _sut.FortifyTroopsAsync(dto);

        fromTerritory.TroopCount.Should().Be(2);
        toTerritory.TroopCount.Should().Be(3);
        await _territoryRepo.Received(1).UpdateAsync(fromTerritory);
        await _territoryRepo.Received(1).UpdateAsync(toTerritory);
    }

    [Fact]
    public async Task FortifyTroops_WhenLeavingLessThanOneTroop_ThrowsInvalidOperationException()
    {
        var (game, player, fromTerritory, toTerritory) = CreateFortifySetup();
        fromTerritory.Claim(player.Id, 3);
        toTerritory.Claim(player.Id, 1);

        SetupFortifyRepositories(game, fromTerritory, toTerritory, areNeighbors: true);

        // Prøver at flytte 3 ud af 3 tropper
        var dto = new FortifyTroopsDto(game.Id, player.Id, fromTerritory.Id, toTerritory.Id, 3);
        var act = () => _sut.FortifyTroopsAsync(dto);

        await act.Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("*At least 1 troop must remain*");
    }

    [Fact]
    public async Task FortifyTroops_WhenPlayerDoesNotOwnDestinationTerritory_ThrowsInvalidOperationException()
    {
        var (game, player, fromTerritory, toTerritory) = CreateFortifySetup();
        var enemyPlayerId = Guid.NewGuid();
        fromTerritory.Claim(player.Id, 3);
        toTerritory.Claim(enemyPlayerId, 1); // Fjendtligt territorium

        SetupFortifyRepositories(game, fromTerritory, toTerritory, areNeighbors: true);

        var dto = new FortifyTroopsDto(game.Id, player.Id, fromTerritory.Id, toTerritory.Id, 1);
        var act = () => _sut.FortifyTroopsAsync(dto);

        await act.Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("*Player must own both source and destination territories*");
    }

    [Fact]
    public async Task FortifyTroops_WhenTerritoriesAreNotNeighbors_ThrowsInvalidOperationException()
    {
        var (game, player, fromTerritory, toTerritory) = CreateFortifySetup();
        fromTerritory.Claim(player.Id, 4);
        toTerritory.Claim(player.Id, 1);

        // areNeighbors = false
        SetupFortifyRepositories(game, fromTerritory, toTerritory, areNeighbors: false);

        var dto = new FortifyTroopsDto(game.Id, player.Id, fromTerritory.Id, toTerritory.Id, 1);
        var act = () => _sut.FortifyTroopsAsync(dto);

        await act.Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("*Territories are not adjacent*");
    }

    [Fact]
    public async Task FortifyTroops_WhenSourceAndDestinationAreSame_ThrowsInvalidOperationException()
    {
        var sameId = Guid.NewGuid();
        var dto = new FortifyTroopsDto(Guid.NewGuid(), Guid.NewGuid(), sameId, sameId, 1);

        var act = () => _sut.FortifyTroopsAsync(dto);

        await act.Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("*Cannot move troops to the same territory*");
    }

    // ========================================== //
    //                 HJÆLPEMETODER              //
    // ========================================== //

    private static (Game Game, Player Player, Territory Territory) CreateValidGameSetup(GamePhase phase)
    {
        var game = new Game(Guid.NewGuid());
        game.Start();
        game.SetPhase(phase);

        var player = new Player(game.Id, Guid.NewGuid(), "Red", 1);
        game.SetCurrentPlayer(player.Id);

        var territory = new Territory(Guid.NewGuid(), "scandinavia");
        typeof(Territory).GetProperty("GameId")!.SetValue(territory, game.Id);

        return (game, player, territory);
    }

    private static (Game Game, Player Player, Territory From, Territory To) CreateFortifySetup()
    {
        var game = new Game(Guid.NewGuid());
        game.Start();
        game.SetPhase(GamePhase.Fortify);

        var player = new Player(game.Id, Guid.NewGuid(), "Blue", 1);
        game.SetCurrentPlayer(player.Id);

        var fromTerritory = new Territory(Guid.NewGuid(), "iceland");
        typeof(Territory).GetProperty("GameId")!.SetValue(fromTerritory, game.Id);

        var toTerritory = new Territory(Guid.NewGuid(), "great-britain");
        typeof(Territory).GetProperty("GameId")!.SetValue(toTerritory, game.Id);

        return (game, player, fromTerritory, toTerritory);
    }

    private void SetupRepositories(Game game, Player player, Territory territory)
    {
        _gameRepo.GetByIdAsync(game.Id).Returns(game);
        _playerRepo.GetByIdAsync(player.Id).Returns(player);
        _territoryRepo.GetByIdAsync(territory.Id).Returns(territory);
    }

    private void SetupFortifyRepositories(Game game, Territory from, Territory to, bool areNeighbors)
    {
        _gameRepo.GetByIdAsync(game.Id).Returns(game);
        _territoryRepo.GetByIdAsync(from.Id).Returns(from);
        _territoryRepo.GetByIdAsync(to.Id).Returns(to);
        _territoryRepo.GetNeighborsAsync(from.Id).Returns(
            areNeighbors ? new List<Territory> { to } : new List<Territory>()
        );
    }
}
