using FluentAssertions;
using Risk.Domain.Services;
using Xunit;

namespace Risk.Tests.Risk.UnitTests.Domain;

public class BattleServiceTests
{
    [Theory]
    [InlineData(1, 1)]
    [InlineData(2, 2)]
    [InlineData(5, 2)]
    public void The_defender_rolls_two_dice_or_one_with_a_single_troop(int troops, int expected) =>
        BattleService.DefenderDiceFor(troops).Should().Be(expected);

    [Theory]
    [InlineData(2, 1)]
    [InlineData(3, 2)]
    [InlineData(4, 3)]
    [InlineData(10, 3)]
    public void The_attacker_rolls_up_to_three_dice_and_leaves_one_troop_behind(int troops, int expected) =>
        BattleService.AttackerDiceFor(troops).Should().Be(expected);

    [Fact]
    public void Fight_ThreeAgainstTwo_ComparesTheTwoHighestPairsInOrder()
    {
        var result = BattleService.Fight(new[] { 2, 6, 5 }, new[] { 2, 6 });

        result.Comparisons.Should().Equal(
            new DiceComparison(6, 6, BattleSide.Defender),
            new DiceComparison(5, 2, BattleSide.Attacker));
        // The attacker's third die (2) has no pair, but is still rolled.
        result.SortedAttackerDice.Should().Equal(6, 5, 2);
    }

    [Fact]
    public void Fight_ATie_IsWonByTheDefender()
    {
        var result = BattleService.Fight(new[] { 4 }, new[] { 4 });

        result.Comparisons.Should().ContainSingle()
            .Which.Winner.Should().Be(BattleSide.Defender);
    }

    [Theory]
    [InlineData(new[] { 6, 6, 6 }, new[] { 1, 1 })]
    [InlineData(new[] { 1, 1, 1 }, new[] { 6, 6 })]
    [InlineData(new[] { 6, 1 }, new[] { 5, 2 })]
    [InlineData(new[] { 3 }, new[] { 3, 3 })]
    public void Fight_LossesAlwaysMatchTheComparisons(int[] attacker, int[] defender)
    {
        var result = BattleService.Fight(attacker, defender);

        result.AttackerLosses.Should().Be(result.Comparisons.Count(c => c.Winner == BattleSide.Defender));
        result.DefenderLosses.Should().Be(result.Comparisons.Count(c => c.Winner == BattleSide.Attacker));
        (result.AttackerLosses + result.DefenderLosses).Should().Be(Math.Min(attacker.Length, defender.Length));
    }

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
