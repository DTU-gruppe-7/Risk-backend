using FluentAssertions;
using NSubstitute;
using Risk.Application.DTOs;
using Risk.Application.Interfaces.Repository;
using Risk.Application.Interfaces.Services;
using Risk.Application.Services;
using Risk.Domain.Entities;
using Xunit;

namespace Risk.Tests.Risk.UnitTests.Services;

public class GameplayServiceTests
{
    private readonly IGameRepository _gameRepository = Substitute.For<IGameRepository>();
    private readonly ITerritoryRepository _territoryRepository = Substitute.For<ITerritoryRepository>();
    private readonly IDiceRoller _diceRoller = Substitute.For<IDiceRoller>();
    private readonly GameplayService _sut;

    public GameplayServiceTests()
    {
        _sut = new GameplayService(_gameRepository, _territoryRepository, _diceRoller);
    }

    [Fact]
    public async Task AttackAsync_WhenAttackerWinsAndDefenderReachesZero_ConquersTerritory()
    {
        // Arrange
        var playerId = Guid.NewGuid();
        var defenderId = Guid.NewGuid();
        var continentId = Guid.NewGuid();

        var game = new Game(Guid.NewGuid());
        game.Start();
        game.SetCurrentPlayer(playerId);
        game.SetPhase(GamePhase.Attack);

        var fromTerritory = new Territory(continentId, "North-Africa");
        fromTerritory.Claim(playerId, 4);

        var toTerritory = new Territory(continentId, "Egypt");
        toTerritory.Claim(defenderId, 1);

        _gameRepository.GetByIdAsync(game.Id).Returns(game);
        _territoryRepository.GetByIdAsync(fromTerritory.Id).Returns(fromTerritory);
        _territoryRepository.GetByIdAsync(toTerritory.Id).Returns(toTerritory);
        _territoryRepository.GetNeighborsAsync(fromTerritory.Id).Returns(new[] { toTerritory });

        // Mock: Angriber slaar [6], Forsvarer slaar [1] -> Forsvarer mister sin sidste troppe
        _diceRoller.Roll(1).Returns(new[] { 6 }, new[] { 1 });

        var dto = new AttackDto(game.Id, playerId, fromTerritory.Id, toTerritory.Id, 1);

        // Act
        var result = await _sut.AttackAsync(dto);

        // Assert
        result.TerritoryConquered.Should().BeTrue();
        result.AttackerLosses.Should().Be(0);
        result.DefenderLosses.Should().Be(1);
        toTerritory.OwnerId.Should().Be(playerId);
        toTerritory.TroopCount.Should().Be(1);
        fromTerritory.TroopCount.Should().Be(3);
        await _territoryRepository.Received(1).SaveChangesAsync();
    }

    [Fact]
    public async Task AttackAsync_WhenDefenderWinsTie_AttackerLosesTroop()
    {
        // Arrange
        var playerId = Guid.NewGuid();
        var defenderId = Guid.NewGuid();
        var continentId = Guid.NewGuid();

        var game = new Game(Guid.NewGuid());
        game.Start();
        game.SetCurrentPlayer(playerId);
        game.SetPhase(GamePhase.Attack);

        var fromTerritory = new Territory(continentId, "Western-Europe");
        fromTerritory.Claim(playerId, 4);

        var toTerritory = new Territory(continentId, "Southern-Europe");
        toTerritory.Claim(defenderId, 2);

        _gameRepository.GetByIdAsync(game.Id).Returns(game);
        _territoryRepository.GetByIdAsync(fromTerritory.Id).Returns(fromTerritory);
        _territoryRepository.GetByIdAsync(toTerritory.Id).Returns(toTerritory);
        _territoryRepository.GetNeighborsAsync(fromTerritory.Id).Returns(new[] { toTerritory });

        // Mock: Angriber slaar [5], Forsvarer slaar [5, 2] -> Uafgjort gaar til forsvarer
        _diceRoller.Roll(1).Returns(new[] { 5 });
        _diceRoller.Roll(2).Returns(new[] { 5, 2 });

        var dto = new AttackDto(game.Id, playerId, fromTerritory.Id, toTerritory.Id, 1);

        // Act
        var result = await _sut.AttackAsync(dto);

        // Assert
        result.TerritoryConquered.Should().BeFalse();
        result.AttackerLosses.Should().Be(1);
        result.DefenderLosses.Should().Be(0);
        fromTerritory.TroopCount.Should().Be(3);
        toTerritory.TroopCount.Should().Be(2);
        toTerritory.OwnerId.Should().Be(defenderId);
    }
}
