using FluentAssertions;
using Risk.Domain.Services;
using Xunit;

namespace Risk.Tests.Risk.UnitTests.Domain;

public class BattleServiceTests
{
    [Fact]
    public void Fight_WhenDefenderHasHigherDie_AttackerLosesTroop()
    {
        var result = BattleService.Fight(new[] { 3 }, new[] { 5 });

        result.AttackerLosses.Should().Be(1);
        result.DefenderLosses.Should().Be(0);
    }

    [Fact]
    public void Fight_WhenAttackerHasHigherDie_DefenderLosesTroop()
    {
        var result = BattleService.Fight(new[] { 6 }, new[] { 2 });

        result.AttackerLosses.Should().Be(0);
        result.DefenderLosses.Should().Be(1);
    }

    [Fact]
    public void Fight_WhenTieOnHighestDie_DefenderWinsTie()
    {
        // Risk-regel: Forsvarer vinder altid ved uafgjort
        var result = BattleService.Fight(new[] { 5 }, new[] { 5 });

        result.AttackerLosses.Should().Be(1);
        result.DefenderLosses.Should().Be(0);
    }

    [Fact]
    public void Fight_UnsortedDice_SortsDescendingBeforeComparison()
    {
        // Input: Atk [2, 6], Def [3, 5]
        // Sorteret: Atk [6, 2], Def [5, 3]
        // 6 mod 5 -> Forsvarer taber 1
        // 2 mod 3 -> Angriber taber 1
        var result = BattleService.Fight(new[] { 2, 6 }, new[] { 3, 5 });

        result.SortedAttackerDice.Should().Equal(6, 2);
        result.SortedDefenderDice.Should().Equal(5, 3);
        result.AttackerLosses.Should().Be(1);
        result.DefenderLosses.Should().Be(1);
    }

    [Fact]
    public void Fight_WhenAttackerHasMoreDiceThanDefender_ExtraDiceAreIgnored()
    {
        // Angriber ruller 3 terninger [6, 5, 1], forsvarer kun 1 [4]
        // Kun 6 mod 4 sammenlignes -> Forsvarer taber 1
        var result = BattleService.Fight(new[] { 6, 5, 1 }, new[] { 4 });

        result.AttackerLosses.Should().Be(0);
        result.DefenderLosses.Should().Be(1);
    }

    [Theory]
    [InlineData(new[] { 6, 4 }, new[] { 6, 4 }, 2, 0)]      // Begge uafgjort -> Forsvarer vinder begge
    [InlineData(new[] { 5, 3 }, new[] { 4, 2 }, 0, 2)]      // Angriber vinder begge
    [InlineData(new[] { 6, 2, 1 }, new[] { 5, 3 }, 1, 1)]   // 6>5 (def taber), 2<3 (atk taber), 1 ignoreres
    public void Fight_VariousCombinations_CalculatesLossesCorrectly(
        int[] attacker, int[] defender, int expectedAttackerLoss, int expectedDefenderLoss)
    {
        var result = BattleService.Fight(attacker, defender);

        result.AttackerLosses.Should().Be(expectedAttackerLoss);
        result.DefenderLosses.Should().Be(expectedDefenderLoss);
    }

    [Fact]
    public void Fight_WhenAttackerDiceIsEmpty_ThrowsArgumentException()
    {
        var act = () => BattleService.Fight(Array.Empty<int>(), new[] { 4 });

        act.Should().Throw<ArgumentException>()
            .WithMessage("*at least 1 die*");
    }

    [Fact]
    public void Fight_WhenDefenderDiceIsEmpty_ThrowsArgumentException()
    {
        var act = () => BattleService.Fight(new[] { 4 }, Array.Empty<int>());

        act.Should().Throw<ArgumentException>()
            .WithMessage("*at least 1 die*");
    }
}
