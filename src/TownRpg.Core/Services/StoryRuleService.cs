using TownRpg.Core.Domain;

namespace TownRpg.Core.Services;

public sealed class StoryRuleService(GameState state)
{
    public StoryRoute DetermineMidgameRoute()
    {
        state.World.Route = state.World.Exposure == ExposureStage.Identified
            ? StoryRoute.Rescue
            : StoryRoute.Cautious;
        return state.World.Route;
    }

    public EndingTier DetermineEnding(int requiredEvidence = 4)
    {
        var highTrustSupporters = state.Relationships.Count(relationship =>
            relationship.TargetId == CharacterIds.Protagonist && relationship.Trust >= 75);
        var allKeysAvailable =
            state.World.HasFact("ability_holder_available") &&
            state.World.HasFact("true_history_recovered") &&
            state.World.HasFact("railway_watch_activated");

        if (allKeysAvailable &&
            state.World.RitualNodesRemaining <= 1 &&
            state.World.Evidence.Count >= requiredEvidence &&
            highTrustSupporters >= 3)
        {
            return EndingTier.SealRestored;
        }

        if (allKeysAvailable && state.World.RitualNodesRemaining <= 2)
        {
            return EndingTier.CostlySuppression;
        }

        return EndingTier.CurrentAwakeningStopped;
    }

    public void ApplyRescueRevelation(bool evidenceComplete, IEnumerable<string> companions)
    {
        var relationships = new RelationshipService(state);
        var companionSet = companions.ToHashSet(StringComparer.Ordinal);
        foreach (var residentId in CharacterIds.CoreResidents)
        {
            var relationship = relationships.GetOrCreate(residentId, CharacterIds.Protagonist);
            if (evidenceComplete)
            {
                relationship.ChangeAffinity(18);
                relationship.ChangeTrust(15);
            }
            else if (relationship.Trust >= 75)
            {
                relationship.ChangeAffinity(8);
            }

            if (companionSet.Contains(residentId))
            {
                relationship.ChangeTrust(12);
                relationship.Memories.Add("joined_rescue_without_proof");
            }
        }
    }
}
