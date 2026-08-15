using System.Text.Json;
using TownRpg.Core.Domain;
using TownRpg.Core.Services;

namespace TownRpg.Core.Tests;

public sealed class SaveGameSerializerTests
{
    [Fact]
    public void RoundTrip_PreservesVerticalSliceState()
    {
        var state = GameState.CreateVerticalSlice();
        state.World.Day = 2;
        state.World.MinuteOfDay = 777;
        state.World.Route = StoryRoute.Cautious;
        state.World.Exposure = ExposureStage.Watched;
        state.World.AddFact("railway_notice_read");
        state.World.AddEvidence("altered_work_order");
        state.Investigation.AddLead("visit_abandoned_line");
        state.Investigation.AddTimeline(2, "timeline.first_anomaly");
        state.Inventory.Add("old_flashlight", 1);
        state.Progression.GrantExperience(25);
        state.CompletedEvents.Add("event.first_anomaly");
        state.ReadDialogueLines.Add("line.001");
        state.Settings.StoryMode = true;
        new PromiseService(state).Create("keep_secret", CharacterIds.Protagonist, CharacterIds.AbilityHolder, "保守秘密");

        var json = SaveGameSerializer.Serialize(state);
        var restored = SaveGameSerializer.Deserialize(json);

        Assert.Equal(2, restored.World.Day);
        Assert.Equal(777, restored.World.MinuteOfDay);
        Assert.Equal(StoryRoute.Cautious, restored.World.Route);
        Assert.Equal(ExposureStage.Watched, restored.World.Exposure);
        Assert.Contains("railway_notice_read", restored.World.Facts);
        Assert.Contains("altered_work_order", restored.World.Evidence);
        Assert.Contains("visit_abandoned_line", restored.Investigation.Leads);
        Assert.Contains(restored.Investigation.Timeline, entry =>
            entry.Day == 2 && entry.TextKey == "timeline.first_anomaly");
        Assert.Equal(1, restored.Inventory.Items["old_flashlight"]);
        Assert.Equal(25, restored.Progression.Experience);
        Assert.Contains("event.first_anomaly", restored.CompletedEvents);
        Assert.Contains("line.001", restored.ReadDialogueLines);
        Assert.True(restored.Settings.StoryMode);
        Assert.Contains(restored.Promises, promise => promise.Id == "keep_secret" && promise.Status == PromiseStatus.Pending);
        Assert.Contains(restored.Relationships, relationship =>
            relationship.SourceId == CharacterIds.AbilityHolder &&
            relationship.TargetId == CharacterIds.Protagonist &&
            relationship.Affinity == 20);

        Assert.False(restored.World.AddFact("railway_notice_read"));
    }

    [Fact]
    public void Deserialize_RejectsUnsupportedFormatVersion()
    {
        const string json = """
            {
              "formatVersion": 999,
              "game": {}
            }
            """;

        var exception = Assert.Throws<NotSupportedException>(() => SaveGameSerializer.Deserialize(json));

        Assert.Contains("999", exception.Message, StringComparison.Ordinal);
        Assert.Contains(SaveEnvelope.CurrentFormatVersion.ToString(), exception.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void Deserialize_RejectsMalformedJson()
    {
        Assert.Throws<JsonException>(() => SaveGameSerializer.Deserialize("{not-json}"));
    }

    [Fact]
    public void Deserialize_RejectsMissingRequiredStateSection()
    {
        var json = SaveGameSerializer.Serialize(GameState.CreateVerticalSlice());
        using var document = JsonDocument.Parse(json);
        var mutated = JsonSerializer.Serialize(new
        {
            formatVersion = 1,
            game = new
            {
                player = document.RootElement.GetProperty("game").GetProperty("player"),
                world = (object?)null
            }
        });

        Assert.Throws<InvalidDataException>(() => SaveGameSerializer.Deserialize(mutated));
    }

    [Fact]
    public void Deserialize_RejectsOutOfRangeRelationshipValue()
    {
        var state = GameState.CreateVerticalSlice();
        state.Relationships[0].Trust = 101;
        var json = SaveGameSerializer.Serialize(state);

        Assert.Throws<InvalidDataException>(() => SaveGameSerializer.Deserialize(json));
    }

    [Fact]
    public void Deserialize_RejectsUnknownPlayerLocation()
    {
        var state = GameState.CreateVerticalSlice();
        state.Player.LocationId = "missing_scene";
        var json = SaveGameSerializer.Serialize(state);

        var exception = Assert.Throws<InvalidDataException>(() => SaveGameSerializer.Deserialize(json));

        Assert.Contains("location", exception.Message, StringComparison.OrdinalIgnoreCase);
    }
}
