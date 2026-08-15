using TownRpg.Core.Domain;

namespace TownRpg.Core.Services;

public static class EventScheduler
{
    public static IReadOnlyList<EventDefinition> AvailableEvents(
        IEnumerable<EventDefinition> definitions,
        WorldState world,
        string locationId,
        IReadOnlySet<string> completedEventIds)
    {
        return definitions
            .Where(definition => !completedEventIds.Contains(definition.Id))
            .Where(definition => definition.IsAvailable(world, locationId))
            .OrderByDescending(definition => definition.Priority)
            .ThenBy(definition => definition.Id, StringComparer.Ordinal)
            .ToArray();
    }
}
