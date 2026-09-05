namespace TownRpg.Core.Domain;

public sealed class NpcScheduleEntry
{
    public required string NpcId { get; init; }
    public int? Day { get; init; }
    public int StartMinute { get; init; }
    public int EndMinute { get; init; }
    public required string LocationId { get; init; }
    public required string ActivityKey { get; init; }
    public HashSet<string> RequiredFacts { get; init; } = new(StringComparer.Ordinal);

    public bool Matches(int day, int minute, IReadOnlySet<string> facts) =>
        (Day is null || Day == day) &&
        minute >= StartMinute &&
        minute < EndMinute &&
        RequiredFacts.All(facts.Contains);
}

public sealed class EventDefinition
{
    public required string Id { get; init; }
    public required string LocationId { get; init; }
    public int StartMinute { get; init; }
    public int EndMinute { get; init; } = WorldState.DeepNightMinute;
    public EventPriority Priority { get; init; }
    public HashSet<string> RequiredFacts { get; init; } = new(StringComparer.Ordinal);
    public HashSet<string> ForbiddenFacts { get; init; } = new(StringComparer.Ordinal);

    public bool IsAvailable(WorldState world, string locationId) =>
        LocationId == locationId &&
        world.MinuteOfDay >= StartMinute &&
        world.MinuteOfDay < EndMinute &&
        RequiredFacts.All(world.Facts.Contains) &&
        ForbiddenFacts.All(fact => !world.Facts.Contains(fact));
}

public sealed class InventoryState
{
    public int Money { get; set; } = 300;
    public Dictionary<string, int> Items { get; set; } = new(StringComparer.Ordinal);

    public void Add(string itemId, int count = 1)
    {
        if (count <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(count));
        }

        Items[itemId] = Items.GetValueOrDefault(itemId) + count;
    }
}

public sealed class ProgressionState
{
    public int Level { get; set; } = 1;
    public int Experience { get; set; }

    public void GrantExperience(int amount)
    {
        if (amount < 0)
        {
            throw new ArgumentOutOfRangeException(nameof(amount));
        }

        Experience += amount;
        while (Level < 10 && Experience >= ExperienceForNextLevel(Level))
        {
            Experience -= ExperienceForNextLevel(Level);
            Level++;
        }
    }

    public static int ExperienceForNextLevel(int level) => 40 + (level * 20);
}

public sealed class PlayerSettings
{
    public const int DefaultWindowWidth = 1280;
    public const int DefaultWindowHeight = 720;

    public float TextSpeed { get; set; } = 1f;
    public float FontScale { get; set; } = 1f;
    public float CameraLookSensitivity { get; set; } = 1f;
    public bool ReduceFlashing { get; set; } = true;
    public bool VibrationEnabled { get; set; } = true;
    public bool HighContrastPrompts { get; set; }
    public bool StoryMode { get; set; }
    public bool BorderlessFullscreen { get; set; }
    public int WindowWidth { get; set; } = DefaultWindowWidth;
    public int WindowHeight { get; set; } = DefaultWindowHeight;
}

public sealed class PlayerState
{
    public string Name { get; set; } = "小岚";
    public string Title { get; set; } = "同学";
    public string LocationId { get; set; } = "old_house";
    public float PositionX { get; set; } = 320f;
    public float PositionY { get; set; } = 260f;
}
