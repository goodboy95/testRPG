using TownRpg.Core.Domain;

namespace TownRpg.Core.Content;

public sealed class CharacterCatalog
{
    public int SchemaVersion { get; init; }
    public List<CharacterDefinition> Characters { get; init; } = [];
}

public sealed class CharacterDefinition
{
    public required string Id { get; init; }
    public required string NameKey { get; init; }
    public required string RoleKey { get; init; }
    public required string ColorHex { get; init; }
    public bool RomanceCandidate { get; init; }
}

public sealed class ScheduleCatalog
{
    public int SchemaVersion { get; init; }
    public List<NpcScheduleEntry> Entries { get; init; } = [];
}

public sealed class EventCatalog
{
    public int SchemaVersion { get; init; }
    public List<ContentEventDefinition> Events { get; init; } = [];
}

public sealed class ContentEventDefinition
{
    public required string Id { get; init; }
    public int? Day { get; init; }
    public required string LocationId { get; init; }
    public int StartMinute { get; init; }
    public int EndMinute { get; init; }
    public EventPriority Priority { get; init; }
    public HashSet<string> RequiredFacts { get; init; } = new(StringComparer.Ordinal);
    public HashSet<string> ForbiddenFacts { get; init; } = new(StringComparer.Ordinal);
    public required string DialoguePath { get; init; }
    public required string StartTitle { get; init; }
    public required string DisplayNameKey { get; init; }
}
