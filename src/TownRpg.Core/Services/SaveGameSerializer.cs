using System.Text.Json;
using System.Text.Json.Serialization;
using TownRpg.Core.Domain;

namespace TownRpg.Core.Services;

public sealed class SaveEnvelope
{
    public const int CurrentFormatVersion = 1;
    public int FormatVersion { get; init; } = CurrentFormatVersion;
    public required GameState Game { get; init; }
}

public static class SaveGameSerializer
{
    private static readonly HashSet<string> ValidPlayerLocations = new(StringComparer.Ordinal)
    {
        "old_house",
        "station_quarter"
    };

    private static readonly JsonSerializerOptions Options = new()
    {
        WriteIndented = true,
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        Converters = { new JsonStringEnumConverter(JsonNamingPolicy.CamelCase) }
    };

    public static string Serialize(GameState state) => JsonSerializer.Serialize(
        new SaveEnvelope { Game = state },
        Options);

    public static GameState Deserialize(string json)
    {
        var envelope = JsonSerializer.Deserialize<SaveEnvelope>(json, Options)
            ?? throw new InvalidDataException("Save data is empty or malformed.");
        if (envelope.FormatVersion != SaveEnvelope.CurrentFormatVersion)
        {
            throw new NotSupportedException(
                $"Save format {envelope.FormatVersion} is not supported; expected {SaveEnvelope.CurrentFormatVersion}.");
        }

        var game = envelope.Game ?? throw new InvalidDataException("Save data does not contain a game state.");
        Validate(game);
        return game;
    }

    private static void Validate(GameState game)
    {
        if (game.Player is null || game.World is null || game.Investigation is null ||
            game.Inventory is null || game.Progression is null || game.Settings is null ||
            game.Relationships is null || game.Promises is null || game.CompletedEvents is null ||
            game.AppliedOutcomes is null || game.ReadDialogueLines is null)
        {
            throw new InvalidDataException("Save data is missing a required state section.");
        }

        if (string.IsNullOrWhiteSpace(game.Player.Name) ||
            string.IsNullOrWhiteSpace(game.Player.Title) ||
            !ValidPlayerLocations.Contains(game.Player.LocationId))
        {
            throw new InvalidDataException("Save data contains an invalid player identity or location.");
        }

        if (game.World.Day < 1 ||
            game.World.MinuteOfDay is < 0 or > WorldState.DeepNightMinute ||
            !Enum.IsDefined(game.World.Route) ||
            !Enum.IsDefined(game.World.Exposure) ||
            game.World.Facts is null ||
            game.World.Evidence is null)
        {
            throw new InvalidDataException("Save data contains an invalid world state.");
        }

        var relationshipKeys = new HashSet<(string SourceId, string TargetId)>();
        foreach (var relationship in game.Relationships)
        {
            if (relationship is null ||
                string.IsNullOrWhiteSpace(relationship.SourceId) ||
                string.IsNullOrWhiteSpace(relationship.TargetId) ||
                relationship.Affinity is < 0 or > 100 ||
                relationship.Trust is < 0 or > 100 ||
                !Enum.IsDefined(relationship.Romance) ||
                relationship.Memories is null ||
                relationship.GiftedAffinityStages is null ||
                !relationshipKeys.Add((relationship.SourceId, relationship.TargetId)))
            {
                throw new InvalidDataException("Save data contains an invalid or duplicate relationship.");
            }
        }

        var promiseIds = new HashSet<string>(StringComparer.Ordinal);
        foreach (var promise in game.Promises)
        {
            if (promise is null ||
                string.IsNullOrWhiteSpace(promise.Id) ||
                string.IsNullOrWhiteSpace(promise.PromisorId) ||
                string.IsNullOrWhiteSpace(promise.PromiseeId) ||
                string.IsNullOrWhiteSpace(promise.Description) ||
                promise.CreatedDay < 1 ||
                !Enum.IsDefined(promise.Status) ||
                !promiseIds.Add(promise.Id))
            {
                throw new InvalidDataException("Save data contains an invalid or duplicate promise.");
            }
        }

        if (game.Investigation.Leads is null ||
            game.Investigation.KnownScheduleEntries is null ||
            game.Investigation.Timeline is null ||
            game.Investigation.Timeline.Any(entry => entry is null || entry.Day < 1 || string.IsNullOrWhiteSpace(entry.TextKey)) ||
            game.Inventory.Items is null ||
            game.Inventory.Money < 0 ||
            game.Inventory.Items.Any(item => string.IsNullOrWhiteSpace(item.Key) || item.Value < 0) ||
            game.Progression.Level is < 1 or > 10 ||
            game.Progression.Experience < 0 ||
            game.Settings.TextSpeed is < 0.5f or > 3f ||
            game.Settings.FontScale is < 0.75f or > 2f)
        {
            throw new InvalidDataException("Save data contains an invalid journal, inventory, progression, or setting value.");
        }
    }
}
