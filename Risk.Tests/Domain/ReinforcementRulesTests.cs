using Risk.Domain.Exceptions;
using Risk.Domain.Services;

namespace Risk.Tests.Domain;

public class ReinforcementRulesTests
{
    [Theory]
    [InlineData(2, 40)]
    [InlineData(3, 35)]
    [InlineData(4, 30)]
    [InlineData(5, 25)]
    [InlineData(6, 20)]
    public void Starting_troops_follow_the_classic_table(int players, int expected) =>
        Assert.Equal(expected, ReinforcementRules.StartingTroops(players));

    [Theory]
    [InlineData(1)]
    [InlineData(7)]
    public void Starting_troops_reject_an_unsupported_number_of_players(int players) =>
        Assert.Throws<BusinessRuleException>(() => ReinforcementRules.StartingTroops(players));

    [Theory]
    [InlineData(1, 0, 3)]   // minimum 3
    [InlineData(11, 0, 3)]  // 11 / 3 = 3
    [InlineData(12, 0, 4)]
    [InlineData(21, 0, 7)]
    [InlineData(9, 5, 8)]   // continent bonus on top
    [InlineData(2, 2, 5)]   // continent bonus on top of the minimum
    public void Territory_reinforcements_are_a_third_of_the_territories_plus_continent_bonus(
        int territories, int continentBonus, int expected) =>
        Assert.Equal(expected, ReinforcementRules.ForTerritories(territories, continentBonus));

    [Theory]
    [InlineData(1, 0)]
    [InlineData(2, 0)]
    [InlineData(3, 0)]
    [InlineData(4, 1)]
    [InlineData(5, 2)]
    [InlineData(6, 3)]
    public void Only_seats_four_to_six_get_a_first_round_bonus(int turnOrder, int expected) =>
        Assert.Equal(expected, ReinforcementRules.FirstRoundBonus(turnOrder));
}
