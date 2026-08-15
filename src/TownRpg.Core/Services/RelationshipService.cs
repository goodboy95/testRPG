using TownRpg.Core.Domain;

namespace TownRpg.Core.Services;

public sealed class RelationshipService(GameState state)
{
    public RelationshipState Get(string sourceId, string targetId)
    {
        var relationship = state.Relationships.FirstOrDefault(item =>
            item.SourceId == sourceId && item.TargetId == targetId);

        return relationship ?? throw new KeyNotFoundException(
            $"Relationship '{sourceId}' -> '{targetId}' does not exist.");
    }

    public RelationshipState GetOrCreate(string sourceId, string targetId)
    {
        var existing = state.Relationships.FirstOrDefault(item =>
            item.SourceId == sourceId && item.TargetId == targetId);
        if (existing is not null)
        {
            return existing;
        }

        var created = new RelationshipState { SourceId = sourceId, TargetId = targetId };
        state.Relationships.Add(created);
        return created;
    }

    public bool CanJoinRescue(string residentId)
    {
        var trustProtagonist = GetOrCreate(residentId, CharacterIds.Protagonist).Trust;
        var trustAbilityHolder = GetOrCreate(residentId, CharacterIds.AbilityHolder).Trust;
        return trustProtagonist >= 75 ||
               trustAbilityHolder >= 75 ||
               (trustProtagonist >= 50 && trustAbilityHolder >= 50);
    }

    public bool TryGivePreferredGift(string residentId, string giftId)
    {
        var relationship = GetOrCreate(residentId, CharacterIds.Protagonist);
        var stageKey = $"{giftId}:{RelationshipState.StageIndex(relationship.Affinity)}";
        if (!relationship.GiftedAffinityStages.Add(stageKey))
        {
            return false;
        }

        relationship.ChangeAffinity(6);
        return true;
    }

    public void ConfirmRomance(string selectedResidentId)
    {
        if (selectedResidentId is not (CharacterIds.AbilityHolder or CharacterIds.TokenHolder))
        {
            throw new InvalidOperationException("This resident is not a romance candidate.");
        }

        var alreadyConfirmed = state.Relationships.FirstOrDefault(relationship =>
            relationship.TargetId == CharacterIds.Protagonist &&
            relationship.Romance == RomanceStatus.Confirmed);
        if (alreadyConfirmed is not null)
        {
            if (alreadyConfirmed.SourceId == selectedResidentId)
            {
                return;
            }

            throw new InvalidOperationException(
                $"Romance is already confirmed with '{alreadyConfirmed.SourceId}'.");
        }

        foreach (var candidateId in new[] { CharacterIds.AbilityHolder, CharacterIds.TokenHolder })
        {
            var relationship = GetOrCreate(candidateId, CharacterIds.Protagonist);
            relationship.Romance = candidateId == selectedResidentId
                ? RomanceStatus.Confirmed
                : RomanceStatus.Closed;
        }
    }
}
