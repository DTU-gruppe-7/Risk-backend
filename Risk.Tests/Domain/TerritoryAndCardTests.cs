using Risk.Domain.Entities;
using Risk.Domain.Exceptions;
using Risk.Domain.Map;

namespace Risk.Tests.Domain;

public class TerritoryTests
{
    private static Territory NewTerritory() => new(Guid.NewGuid(), " North-Africa ");

    [Fact]
    public void Identifier_is_normalised()
    {
        Assert.Equal("north-africa", NewTerritory().Identifier);
    }

    [Fact]
    public void Constructor_requires_a_game()
    {
        Assert.Throws<DomainValidationException>(() => new Territory(Guid.Empty, "alaska"));
    }

    [Fact]
    public void Claim_sets_owner_and_troops_and_cannot_be_repeated()
    {
        var territory = NewTerritory();
        var player = Guid.NewGuid();

        territory.Claim(player, 3);

        Assert.Equal(player, territory.OwnerId);
        Assert.Equal(3, territory.TroopCount);
        Assert.Throws<BusinessRuleException>(() => territory.Claim(Guid.NewGuid()));
    }

    [Fact]
    public void AdjustTroops_adds_and_removes()
    {
        var territory = NewTerritory();
        territory.Claim(Guid.NewGuid(), 2);

        territory.AdjustTroops(5);
        territory.AdjustTroops(-3);
        territory.AdjustTroops(0);

        Assert.Equal(4, territory.TroopCount);
    }

    [Fact]
    public void AdjustTroops_cannot_remove_more_than_present()
    {
        var territory = NewTerritory();
        territory.Claim(Guid.NewGuid(), 2);

        Assert.Throws<BusinessRuleException>(() => territory.AdjustTroops(-3));
    }

    [Fact]
    public void Blizzard_cannot_be_claimed_reinforced_or_conquered()
    {
        var territory = NewTerritory();

        territory.MakeBlizzard();

        Assert.True(territory.IsBlizzard);
        Assert.Throws<BusinessRuleException>(() => territory.Claim(Guid.NewGuid()));
        Assert.Throws<BusinessRuleException>(() => territory.AddTroops(1));
        Assert.Throws<BusinessRuleException>(() => territory.TransferOwnership(Guid.NewGuid(), 1));
    }

    [Fact]
    public void Occupied_territory_cannot_become_a_blizzard()
    {
        var territory = NewTerritory();
        territory.Claim(Guid.NewGuid());

        Assert.Throws<BusinessRuleException>(territory.MakeBlizzard);
    }
}

public class CardTests
{
    [Fact]
    public void Used_bonus_card_cannot_be_assigned_or_used_again()
    {
        var card = new BonusCard(Guid.NewGuid(), BonusCardType.Cavalry, "Alaska");
        card.AssignToPlayer(Guid.NewGuid());

        card.MarkAsUsed();

        Assert.Null(card.PlayerId);
        Assert.Throws<BusinessRuleException>(card.MarkAsUsed);
        Assert.Throws<BusinessRuleException>(() => card.AssignToPlayer(Guid.NewGuid()));
    }

    [Fact]
    public void Mission_must_be_assigned_before_it_can_be_completed()
    {
        var game = new Game(Guid.NewGuid());
        var player = game.AddPlayer(Guid.NewGuid(), "red");
        var opponent = game.AddPlayer(Guid.NewGuid(), "blue");
        game.Start(ClassicMap.Graph, new Random(1));
        var mission = new MissionCard(
            game.Id, "Conquer the world", "Hold every territory.",
            MissionObjectiveType.ControlAllTerritories);

        Assert.Throws<BusinessRuleException>(() =>
            mission.Complete(game, ClassicMap.Graph));

        mission.AssignToPlayer(player.Id);
        foreach (var territory in game.Territories.Where(t => t.OwnerId == opponent.Id).ToList())
            territory.TransferOwnership(player.Id, 1);

        mission.Complete(game, ClassicMap.Graph);
        Assert.True(mission.IsCompleted);
        Assert.Throws<BusinessRuleException>(() =>
            mission.Complete(game, ClassicMap.Graph));
    }
}
