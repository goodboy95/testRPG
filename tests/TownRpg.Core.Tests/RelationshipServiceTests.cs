using TownRpg.Core.Domain;
using TownRpg.Core.Services;

namespace TownRpg.Core.Tests;

public sealed class RelationshipServiceTests
{
    [Theory]
    [InlineData(-10, 0)]
    [InlineData(0, 0)]
    [InlineData(24, 0)]
    [InlineData(25, 1)]
    [InlineData(49, 1)]
    [InlineData(50, 2)]
    [InlineData(74, 2)]
    [InlineData(75, 3)]
    [InlineData(100, 3)]
    [InlineData(110, 3)]
    public void StageIndex_UsesFourStableBands(int value, int expected)
    {
        Assert.Equal(expected, RelationshipState.StageIndex(value));
    }

    [Fact]
    public void Labels_DoNotExposeNumericValues()
    {
        var relationship = CreateRelationship("resident", "target", affinity: 25, trust: 75);

        Assert.Equal("熟悉", relationship.AffinityLabel);
        Assert.Equal("信赖", relationship.TrustLabel);
    }

    [Fact]
    public void Changes_ClampAffinityAndTrustToInclusiveRange()
    {
        var relationship = CreateRelationship("resident", "target", affinity: 95, trust: 4);

        relationship.ChangeAffinity(20);
        relationship.ChangeTrust(-10);

        Assert.Equal(100, relationship.Affinity);
        Assert.Equal(0, relationship.Trust);
    }

    [Fact]
    public void Relationships_AreDirectional()
    {
        var state = new GameState();
        var service = new RelationshipService(state);

        service.GetOrCreate("resident", CharacterIds.Protagonist).ChangeTrust(80);
        var reverse = service.GetOrCreate(CharacterIds.Protagonist, "resident");

        Assert.Equal(80, service.Get("resident", CharacterIds.Protagonist).Trust);
        Assert.Equal(0, reverse.Trust);
        Assert.Equal(2, state.Relationships.Count);
    }

    [Theory]
    [InlineData(75, 0, true)]
    [InlineData(0, 75, true)]
    [InlineData(50, 50, true)]
    [InlineData(74, 49, false)]
    [InlineData(49, 74, false)]
    public void CanJoinRescue_ImplementsEitherHighOrBothMediumTrust(
        int trustProtagonist,
        int trustAbilityHolder,
        bool expected)
    {
        var state = new GameState();
        state.Relationships.Add(CreateRelationship("resident", CharacterIds.Protagonist, trust: trustProtagonist));
        state.Relationships.Add(CreateRelationship("resident", CharacterIds.AbilityHolder, trust: trustAbilityHolder));
        var service = new RelationshipService(state);

        Assert.Equal(expected, service.CanJoinRescue("resident"));
    }

    [Fact]
    public void ConfirmRomance_LocksOneCandidateAndCannotBeSwitched()
    {
        var state = GameState.CreateVerticalSlice();
        var service = new RelationshipService(state);

        service.ConfirmRomance(CharacterIds.AbilityHolder);
        service.ConfirmRomance(CharacterIds.AbilityHolder);

        Assert.Equal(
            RomanceStatus.Confirmed,
            service.Get(CharacterIds.AbilityHolder, CharacterIds.Protagonist).Romance);
        Assert.Equal(
            RomanceStatus.Closed,
            service.Get(CharacterIds.TokenHolder, CharacterIds.Protagonist).Romance);
        Assert.Throws<InvalidOperationException>(() => service.ConfirmRomance(CharacterIds.TokenHolder));
    }

    [Fact]
    public void PreferredGift_OnlyAwardsOncePerGiftAndRelationshipStage()
    {
        var state = new GameState();
        var service = new RelationshipService(state);

        Assert.True(service.TryGivePreferredGift("resident", "preferred_gift"));
        Assert.False(service.TryGivePreferredGift("resident", "preferred_gift"));

        var relationship = service.Get("resident", CharacterIds.Protagonist);
        Assert.Equal(6, relationship.Affinity);
        Assert.Equal(0, relationship.Trust);
    }

    private static RelationshipState CreateRelationship(
        string sourceId,
        string targetId,
        int affinity = 0,
        int trust = 0) =>
        new()
        {
            SourceId = sourceId,
            TargetId = targetId,
            Affinity = affinity,
            Trust = trust
        };
}
