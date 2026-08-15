using TownRpg.Core.Domain;
using TownRpg.Core.Services;

namespace TownRpg.Core.Tests;

public sealed class NarrativeCommandServiceTests
{
    [Fact]
    public void Execute_AppliesEveryWhitelistedStateMutation()
    {
        var state = new GameState();
        var service = new NarrativeCommandService(state);

        service.Execute("add_fact", "railway_notice_read");
        service.Execute("add_evidence", "altered_work_order");
        service.Execute("add_lead", "visit_abandoned_line");
        service.Execute("timeline", "timeline.notice_read");
        service.Execute("change_affinity", "resident", CharacterIds.Protagonist, "12");
        service.Execute("change_trust", "resident", CharacterIds.Protagonist, "75");
        service.Execute("remember", "resident", CharacterIds.Protagonist, "kept_secret");
        service.Execute("create_promise", "keep_secret", CharacterIds.Protagonist, "resident", "保守秘密");
        service.Execute("resolve_promise", "keep_secret", "fulfilled");
        service.Execute("raise_exposure", "1");
        service.Execute("complete_event", "event.notice");
        service.Execute("grant_xp", "60");

        var relationship = new RelationshipService(state).Get("resident", CharacterIds.Protagonist);
        Assert.Contains("railway_notice_read", state.World.Facts);
        Assert.Contains("altered_work_order", state.World.Evidence);
        Assert.Contains("visit_abandoned_line", state.Investigation.Leads);
        Assert.Contains(state.Investigation.Timeline, item => item.TextKey == "timeline.notice_read");
        Assert.Equal(12, relationship.Affinity);
        Assert.Equal(75, relationship.Trust);
        Assert.Contains("kept_secret", relationship.Memories);
        Assert.Equal(PromiseStatus.Fulfilled, Assert.Single(state.Promises).Status);
        Assert.Equal(ExposureStage.Watched, state.World.Exposure);
        Assert.Contains("event.notice", state.CompletedEvents);
        Assert.Equal(2, state.Progression.Level);
        Assert.Equal(0, state.Progression.Experience);
    }

    [Fact]
    public void Evaluate_ReadsOnlyWhitelistedConditions()
    {
        var state = new GameState();
        state.World.Day = 2;
        var service = new NarrativeCommandService(state);
        service.Execute("add_fact", "fact");
        service.Execute("add_lead", "lead");
        service.Execute("complete_event", "event");
        service.Execute("change_trust", "resident", CharacterIds.Protagonist, "50");
        service.Execute("change_affinity", "resident", CharacterIds.Protagonist, "25");

        Assert.True(service.Evaluate("has_fact", "fact"));
        Assert.True(service.Evaluate("has_lead", "lead"));
        Assert.True(service.Evaluate("event_complete", "event"));
        Assert.True(service.Evaluate("trust_at_least", "resident", CharacterIds.Protagonist, "50"));
        Assert.True(service.Evaluate("affinity_at_least", "resident", CharacterIds.Protagonist, "25"));
        Assert.True(service.Evaluate("day_at_least", "2"));
        Assert.False(service.Evaluate("day_at_least", "3"));
    }

    [Fact]
    public void Execute_RejectsUnknownCommandWithoutChangingState()
    {
        var state = new GameState();
        var service = new NarrativeCommandService(state);

        Assert.Throws<InvalidOperationException>(() => service.Execute("set_arbitrary_state", "value"));
        Assert.Empty(state.World.Facts);
        Assert.Empty(state.Relationships);
    }

    [Fact]
    public void Execute_RejectsMalformedArgumentCountBeforeMutation()
    {
        var state = new GameState();
        var service = new NarrativeCommandService(state);

        Assert.Throws<ArgumentException>(() => service.Execute("change_trust", "resident", CharacterIds.Protagonist));
        Assert.Empty(state.Relationships);
    }

    [Fact]
    public void Evaluate_RejectsUnknownOrMalformedCondition()
    {
        var service = new NarrativeCommandService(new GameState());

        Assert.Throws<InvalidOperationException>(() => service.Evaluate("has_fact"));
        Assert.Throws<InvalidOperationException>(() => service.Evaluate("arbitrary_condition", "value"));
    }

    [Fact]
    public void Evaluate_MissingRelationshipAsZeroWithoutMutatingState()
    {
        var state = new GameState();
        var service = new NarrativeCommandService(state);

        Assert.False(service.Evaluate("trust_at_least", "unknown", CharacterIds.Protagonist, "1"));
        Assert.True(service.Evaluate("affinity_at_least", "unknown", CharacterIds.Protagonist, "0"));
        Assert.Empty(state.Relationships);
    }
}
