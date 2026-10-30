using FluentAssertions;
using Risk.Infrastructure.Services;
using Xunit;
using Xunit.Abstractions;

namespace Risk.Tests.Risk.UnitTests.Services;

public class DiceRollerTests
{
    private readonly DiceRoller _sut = new();
    private readonly ITestOutputHelper _output;

    public DiceRollerTests(ITestOutputHelper output)
    {
        _output = output;
    }

    [Theory]
    [InlineData(1)]
    [InlineData(2)]
    [InlineData(3)]
    public void Roll_ReturnsExactRequestedNumberOfDice(int count)
    {
        var result = _sut.Roll(count);

        result.Should().HaveCount(count);
    }

    [Fact]
    public void Roll_AllDiceValuesAreAlwaysBetweenOneAndSix()
    {
        var result = _sut.Roll(1000);

        result.Should().OnlyContain(die => die >= 1 && die <= 6);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    [InlineData(-10)]
    public void Roll_WhenCountIsZeroOrNegative_ThrowsArgumentOutOfRangeException(int count)
    {
        var act = () => _sut.Roll(count);

        act.Should().Throw<ArgumentOutOfRangeException>()
            .WithMessage("*Must roll at least 1 die*");
    }

    [Fact]
    public void Roll_DistributionIsUniformAcrossAllFaces()
    {
        const int totalRolls = 100_000;
        const double expectedPerFace = totalRolls / 6.0; // ~16.666,67
        const double allowedTolerance = 0.05; // 5% tolerance

        var rolls = _sut.Roll(totalRolls);
        var counts = rolls.GroupBy(x => x).ToDictionary(g => g.Key, g => g.Count());

        // Udskriv det samlede resultat til konsollen
        _output.WriteLine("========================================");
        _output.WriteLine($"RESULTAT AF {totalRolls:N0} TERNINGEKAST:");
        _output.WriteLine("----------------------------------------");
        foreach (var face in counts.Keys.OrderBy(k => k))
        {
            double percentage = (counts[face] / (double)totalRolls) * 100;
            _output.WriteLine($"Slaaet {face}: {counts[face],6} gange ({percentage:F2}%)");
        }
        _output.WriteLine("========================================");

        counts.Keys.Should().BeEquivalentTo(new[] { 1, 2, 3, 4, 5, 6 });

        foreach (var face in counts.Keys)
        {
            counts[face].Should().BeInRange(
                (int)(expectedPerFace * (1 - allowedTolerance)),
                (int)(expectedPerFace * (1 + allowedTolerance)),
                because: $"Face {face} should appear roughly equally across {totalRolls} rolls"
            );
        }
    }
}
