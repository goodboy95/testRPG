using TownRpg.Core.Domain;
using TownRpg.Core.Services;

namespace TownRpg.Core.Tests;

public sealed class StoryRuleServiceTests
{
    [Theory]
    [InlineData(ExposureStage.Unnoticed, StoryRoute.Cautious)]
    [InlineData(ExposureStage.Watched, StoryRoute.Cautious)]
    [InlineData(ExposureStage.Suspected, StoryRoute.Cautious)]
    [InlineData(ExposureStage.Identified, StoryRoute.Rescue)]
    public void DetermineMidgameRoute_UsesConfirmedIdentityAsRescueBoundary(
        ExposureStage exposure,
        StoryRoute expected)
    {
        var state = new GameState();
        state.World.Exposure = exposure;

        var result = new StoryRuleService(state).DetermineMidgameRoute();

        Assert.Equal(expected, result);
        Assert.Equal(expected, state.World.Route);
    }

    [Fact]
    public void DetermineEnding_ReturnsSealRestoredWhenEveryRequirementIsMet()
    {
        var state = CreateStateWithAllStoryKeys();
        state.World.RitualNodesRemaining = 1;
        foreach (var evidence in new[] { "e1", "e2", "e3", "e4" })
        {
            state.World.AddEvidence(evidence);
        }

        AddHighTrustSupporter(state, "resident-1");
        AddHighTrustSupporter(state, "resident-2");
        AddHighTrustSupporter(state, "resident-3");

        Assert.Equal(EndingTier.SealRestored, new StoryRuleService(state).DetermineEnding());
    }

    [Fact]
    public void DetermineEnding_FallsBackToCostlySuppressionWithKeysAndAtMostTwoNodes()
    {
        var state = CreateStateWithAllStoryKeys();
        state.World.RitualNodesRemaining = 2;

        Assert.Equal(EndingTier.CostlySuppression, new StoryRuleService(state).DetermineEnding());
    }

    [Fact]
    public void DetermineEnding_FallsBackToStoppingCurrentAwakeningWithoutAllKeys()
    {
        var state = new GameState();
        state.World.RitualNodesRemaining = 0;

        Assert.Equal(EndingTier.CurrentAwakeningStopped, new StoryRuleService(state).DetermineEnding());
    }

    [Fact]
    public void ApplyRescueRevelation_ClampsRewardsAndAddsCompanionMemory()
    {
        var state = GameState.CreateVerticalSlice();
        var relationships = new RelationshipService(state);
        var companion = relationships.Get(CharacterIds.Shopkeeper, CharacterIds.Protagonist);
        companion.Affinity = 95;
        companion.Trust = 95;

        new StoryRuleService(state).ApplyRescueRevelation(true, [CharacterIds.Shopkeeper]);

        Assert.Equal(100, companion.Affinity);
        Assert.Equal(100, companion.Trust);
        Assert.Contains("joined_rescue_without_proof", companion.Memories);
    }

    private static GameState CreateStateWithAllStoryKeys()
    {
        var state = new GameState();
        state.World.AddFact("ability_holder_available");
        state.World.AddFact("true_history_recovered");
        state.World.AddFact("railway_watch_activated");
        return state;
    }

    private static void AddHighTrustSupporter(GameState state, string residentId)
    {
        state.Relationships.Add(new RelationshipState
        {
            SourceId = residentId,
            TargetId = CharacterIds.Protagonist,
            Trust = 75
        });
    }
}
