using System.Text.Json;
using TownRpg.Core.Domain;
using TownRpg.Core.Services;

namespace TownRpg.Core.Tests;

public sealed class SaveGameSerializerTests
{
    [Fact]
    public void SubtitleSettings_RoundTripIndependentlyOfMenuText()
    {
        var state = GameState.CreateVerticalSlice();
        state.Settings.FontScale = 1.25f;
        state.Settings.SubtitleFontScale = 2f;
        state.Settings.SubtitleBackground = SubtitleBackground.HighContrast;
        var restored = SaveGameSerializer.Deserialize(SaveGameSerializer.Serialize(state));
        Assert.Equal(1.25f, restored.Settings.FontScale);
        Assert.Equal(2f, restored.Settings.EffectiveSubtitleFontScale);
        Assert.Equal(SubtitleBackground.HighContrast, restored.Settings.SubtitleBackground);
    }

    [Fact]
    public void OldSubtitleSettings_InheritSavedFontScaleWithoutResettingOtherPreferences()
    {
        var state = GameState.CreateVerticalSlice();
        state.Settings.FontScale = 1.5f;
        state.Settings.TextSpeed = .75f;
        var json = System.Text.Json.Nodes.JsonNode.Parse(SaveGameSerializer.Serialize(state))!;
        var settings = json["game"]!["settings"]!.AsObject();
        settings.Remove("subtitleFontScale"); settings.Remove("subtitleBackground");
        var restored = SaveGameSerializer.Deserialize(json.ToJsonString());
        Assert.Null(restored.Settings.SubtitleFontScale);
        Assert.Equal(1.5f, restored.Settings.EffectiveSubtitleFontScale);
        Assert.Equal(SubtitleBackground.SoftShadow, restored.Settings.SubtitleBackground);
        Assert.Equal(.75f, restored.Settings.TextSpeed);
        restored.Settings.SubtitleFontScale = 1f;
        Assert.Equal(1.5f, restored.Settings.FontScale);
    }

    [Theory]
    [InlineData(.74f, 0)]
    [InlineData(2.01f, 0)]
    [InlineData(1f, 99)]
    public void SubtitleSettings_RejectInvalidScaleOrBackground(float scale, int background)
    {
        var state = GameState.CreateVerticalSlice();
        state.Settings.SubtitleFontScale = scale;
        state.Settings.SubtitleBackground = (SubtitleBackground)background;
        Assert.Throws<InvalidDataException>(() => SaveGameSerializer.Deserialize(SaveGameSerializer.Serialize(state)));
    }

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
        state.Settings.WindowWidth = 1920;
        state.Settings.WindowHeight = 1080;
        state.Settings.CameraLookSensitivity = 1.35f;
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
        Assert.Equal(1920, restored.Settings.WindowWidth);
        Assert.Equal(1080, restored.Settings.WindowHeight);
        Assert.Equal(1.35f, restored.Settings.CameraLookSensitivity);
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

    [Fact]
    public void Deserialize_OldV1SettingsUseNewDisplayDefaults()
    {
        var json = SaveGameSerializer.Serialize(GameState.CreateVerticalSlice());
        using var document = JsonDocument.Parse(json);
        var game = document.RootElement.GetProperty("game");
        var settings = game.GetProperty("settings").EnumerateObject()
            .Where(property => property.Name is not "windowWidth" and not "windowHeight" and
                               not "cameraLookSensitivity")
            .ToDictionary(property => property.Name, property => property.Value.Clone());
        var gameProperties = game.EnumerateObject()
            .Where(property => property.Name != "settings")
            .ToDictionary(property => property.Name, property => property.Value.Clone());
        gameProperties["settings"] = JsonSerializer.SerializeToElement(settings);
        var legacyJson = JsonSerializer.Serialize(new { formatVersion = 1, game = gameProperties });

        var restored = SaveGameSerializer.Deserialize(legacyJson);

        Assert.Equal(PlayerSettings.DefaultWindowWidth, restored.Settings.WindowWidth);
        Assert.Equal(PlayerSettings.DefaultWindowHeight, restored.Settings.WindowHeight);
        Assert.Equal(1f, restored.Settings.CameraLookSensitivity);
    }

    [Theory]
    [InlineData(1366, 768, 1f)]
    [InlineData(1280, 720, 0.49f)]
    [InlineData(1280, 720, 1.51f)]
    public void Deserialize_RejectsInvalidDisplaySettings(int width, int height, float sensitivity)
    {
        var state = GameState.CreateVerticalSlice();
        state.Settings.WindowWidth = width;
        state.Settings.WindowHeight = height;
        state.Settings.CameraLookSensitivity = sensitivity;

        Assert.Throws<InvalidDataException>(() =>
            SaveGameSerializer.Deserialize(SaveGameSerializer.Serialize(state)));
    }
}
