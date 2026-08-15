using TownRpg.Core.Domain;
using TownRpg.Core.Services;

namespace TownRpg.Core.Tests;

public sealed class EventOutcomeServiceTests
{
    [Fact]
    public void Apply_IsIdempotentWhenOutcomeIsSubmittedTwice()
    {
        var state = new GameState();
        var service = new EventOutcomeService(state);
        var mutations = new[]
        {
            Mutation("change_trust", "resident", CharacterIds.Protagonist, "10"),
            Mutation("add_fact", "fact")
        };

        Assert.True(service.Apply("outcome", mutations));
        Assert.False(service.Apply("outcome", mutations));

        Assert.Equal(10, new RelationshipService(state).Get("resident", CharacterIds.Protagonist).Trust);
        Assert.Single(state.AppliedOutcomes);
    }

    [Fact]
    public void Apply_RejectsMalformedMutationBeforeAnyStateChange()
    {
        var state = new GameState();
        var service = new EventOutcomeService(state);
        var mutations = new[]
        {
            Mutation("add_fact", "would_have_been_added"),
            Mutation("change_trust", "resident", CharacterIds.Protagonist, "not-a-number")
        };

        Assert.Throws<FormatException>(() => service.Apply("invalid", mutations));

        Assert.Empty(state.World.Facts);
        Assert.Empty(state.Relationships);
        Assert.Empty(state.AppliedOutcomes);
    }

    [Fact]
    public void Apply_RejectsUnknownCommandBeforeAnyStateChange()
    {
        var state = new GameState();
        var service = new EventOutcomeService(state);

        Assert.Throws<InvalidOperationException>(() => service.Apply("invalid", [
            Mutation("add_fact", "would_have_been_added"),
            Mutation("arbitrary_state_write", "value")
        ]));

        Assert.Empty(state.World.Facts);
        Assert.Empty(state.AppliedOutcomes);
    }

    [Fact]
    public void Apply_RollsBackEarlierMutationsWhenAValidatedCommandFailsAtRuntime()
    {
        var state = new GameState();
        var service = new EventOutcomeService(state);

        Assert.Throws<KeyNotFoundException>(() => service.Apply("runtime-failure", [
            Mutation("add_fact", "must_be_rolled_back"),
            Mutation("resolve_promise", "missing_promise", "fulfilled")
        ]));

        Assert.DoesNotContain("must_be_rolled_back", state.World.Facts);
        Assert.Empty(state.AppliedOutcomes);
    }

    private static NarrativeMutation Mutation(string command, params string[] arguments) => new()
    {
        Command = command,
        Arguments = arguments.ToList()
    };
}
