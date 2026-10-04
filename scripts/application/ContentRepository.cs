using Godot;
using TownRpg.Core.Content;
using TownRpg.Core.Domain;
using TownRpg.Core.Services;
using GodotFileAccess = Godot.FileAccess;

namespace TownRpg.Application;

public sealed class ContentRepository
{
    public CharacterCatalog Characters { get; private set; } = new();
    public ScheduleCatalog Schedules { get; private set; } = new();
    public EventCatalog Events { get; private set; } = new();
    public ItemCatalog Items { get; private set; } = new();
    public OpeningResident[] OpeningResidents { get; private set; } = [];
    public OpeningGoods[] OpeningGoods { get; private set; } = [];

    public void Load()
    {
        Characters = LoadJson<CharacterCatalog>("res://data/characters.json");
        Schedules = LoadJson<ScheduleCatalog>("res://data/schedules.json");
        Events = LoadJson<EventCatalog>("res://data/events.json");
        Items = LoadJson<ItemCatalog>("res://data/items.json");
        OpeningResidents = VillageOpeningContent.ParseResidents(GodotFileAccess.GetFileAsString("res://data/village-opening-npcs.json"));
        OpeningGoods = VillageOpeningContent.ParseGoods(GodotFileAccess.GetFileAsString("res://data/village-opening-goods.json"));
        Validate();
    }

    private static T LoadJson<T>(string path) where T : class
    {
        using var file = GodotFileAccess.Open(path, GodotFileAccess.ModeFlags.Read);
        if (file is null)
        {
            throw new FileNotFoundException($"Required content file '{path}' was not found.");
        }

        return ContentJson.Deserialize<T>(file.GetAsText());
    }

    private void Validate()
    {
        var errors = new List<string>();
        if (Characters.SchemaVersion != 1 || Schedules.SchemaVersion != 1 ||
            Events.SchemaVersion != 1 || Items.SchemaVersion != 1)
        {
            errors.Add("All content catalogs must use schemaVersion 1.");
        }

        var characterIds = Characters.Characters.Select(character => character.Id).ToHashSet(StringComparer.Ordinal);
        if (!characterIds.Contains(CharacterIds.Protagonist))
        {
            errors.Add("characters.json is missing the protagonist definition.");
        }

        foreach (var duplicate in Characters.Characters.GroupBy(character => character.Id).Where(group => group.Count() > 1))
        {
            errors.Add($"Duplicate character id '{duplicate.Key}'.");
        }

        foreach (var schedule in Schedules.Entries.Where(entry => !characterIds.Contains(entry.NpcId)))
        {
            errors.Add($"Schedule references unknown NPC '{schedule.NpcId}'.");
        }

        errors.AddRange(new ScheduleService(Schedules.Entries).Validate());
        foreach (var gameEvent in Events.Events)
        {
            if (!GodotFileAccess.FileExists(gameEvent.DialoguePath))
            {
                errors.Add($"Event '{gameEvent.Id}' references missing dialogue '{gameEvent.DialoguePath}'.");
            }
        }
        foreach (var resident in OpeningResidents)
            if (!characterIds.Contains(resident.Id) || !ResourceLoader.Exists(resident.Model)) errors.Add("Invalid opening resident reference: "+resident.Id);
        var openingEvents = Events.Events.Where(e=>e.Id.StartsWith("village.opening.")).ToArray();
        if (!openingEvents.Select(e=>e.Id).ToHashSet().SetEquals(VillageOpeningContent.StoryKeys.Select(VillageOpeningIds.Event))) errors.Add("Opening event catalog is incomplete or contains unknown events.");

        foreach (var duplicate in Items.Items.GroupBy(item => item.Id).Where(group => group.Count() > 1))
        {
            errors.Add($"Duplicate item id '{duplicate.Key}'.");
        }
        var itemIds = Items.Items.Select(item => item.Id).ToHashSet(StringComparer.Ordinal);
        foreach (var missing in DreamEscapeIds.Items.Except(itemIds).Order(StringComparer.Ordinal))
        {
            errors.Add($"items.json is missing dream item '{missing}'.");
        }
        foreach (var item in Items.Items)
        {
            if (!GodotFileAccess.FileExists(item.IconPath) ||
                !ResourceLoader.Exists(item.IconPath, "Texture2D"))
            {
                errors.Add($"Item '{item.Id}' references missing or unimported icon '{item.IconPath}'.");
            }
        }

        if (errors.Count > 0)
        {
            throw new InvalidDataException("Content validation failed:\n" + string.Join("\n", errors));
        }
    }
}
