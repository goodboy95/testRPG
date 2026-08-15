using TownRpg.Core.Domain;

namespace TownRpg.Core.Services;

public sealed class EventOutcomeService(GameState state)
{
    private readonly NarrativeCommandService _commands = new(state);

    public bool Apply(string outcomeId, IReadOnlyList<NarrativeMutation> mutations)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(outcomeId);
        if (state.AppliedOutcomes.Contains(outcomeId))
        {
            return false;
        }

        ValidateAll(mutations);
        var snapshot = SaveGameSerializer.Serialize(state);
        try
        {
            foreach (var mutation in mutations)
            {
                _commands.Execute(mutation.Command, mutation.Arguments.ToArray());
            }

            state.AppliedOutcomes.Add(outcomeId);
            return true;
        }
        catch
        {
            RestoreFromSnapshot(snapshot);
            throw;
        }
    }

    private static void ValidateAll(IReadOnlyList<NarrativeMutation> mutations)
    {
        foreach (var mutation in mutations)
        {
            NarrativeCommandService.ValidateMutation(mutation.Command, mutation.Arguments);
        }
    }

    private void RestoreFromSnapshot(string snapshot)
    {
        var restored = SaveGameSerializer.Deserialize(snapshot);
        state.Player = restored.Player;
        state.World = restored.World;
        state.Relationships = restored.Relationships;
        state.Promises = restored.Promises;
        state.Investigation = restored.Investigation;
        state.Inventory = restored.Inventory;
        state.Progression = restored.Progression;
        state.CompletedEvents = restored.CompletedEvents;
        state.AppliedOutcomes = restored.AppliedOutcomes;
        state.ReadDialogueLines = restored.ReadDialogueLines;
        state.Settings = restored.Settings;
    }
}

public sealed class NarrativeMutation
{
    public required string Command { get; init; }
    public List<string> Arguments { get; init; } = [];
}
