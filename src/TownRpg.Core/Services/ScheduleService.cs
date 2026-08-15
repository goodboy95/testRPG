using TownRpg.Core.Domain;

namespace TownRpg.Core.Services;

public sealed class ScheduleService(IEnumerable<NpcScheduleEntry> entries)
{
    private readonly IReadOnlyList<NpcScheduleEntry> _entries = entries.ToArray();

    public NpcScheduleEntry? Current(string npcId, WorldState world) =>
        _entries
            .Where(entry => entry.NpcId == npcId)
            .Where(entry => entry.Matches(world.Day, world.MinuteOfDay, world.Facts))
            .OrderByDescending(entry => entry.RequiredFacts.Count)
            .ThenByDescending(entry => entry.Day.HasValue)
            .FirstOrDefault();

    public NpcScheduleEntry? NextVisible(string npcId, WorldState world, int searchDays = 7)
    {
        for (var dayOffset = 0; dayOffset <= searchDays; dayOffset++)
        {
            var day = world.Day + dayOffset;
            var minimumMinute = dayOffset == 0 ? world.MinuteOfDay : WorldState.MorningStartMinute;
            var match = _entries
                .Where(entry => entry.NpcId == npcId)
                .Where(entry => entry.Day is null || entry.Day == day)
                .Where(entry => entry.EndMinute > minimumMinute)
                .Where(entry => entry.RequiredFacts.All(world.Facts.Contains))
                .OrderBy(entry => Math.Max(entry.StartMinute, minimumMinute))
                .ThenByDescending(entry => entry.RequiredFacts.Count)
                .FirstOrDefault();
            if (match is not null)
            {
                return match;
            }
        }

        return null;
    }

    public IReadOnlyList<string> Validate()
    {
        var errors = new List<string>();
        foreach (var group in _entries.GroupBy(entry => (entry.NpcId, entry.Day)))
        {
            var ordered = group.OrderBy(entry => entry.StartMinute).ToArray();
            for (var index = 1; index < ordered.Length; index++)
            {
                var previous = ordered[index - 1];
                var current = ordered[index];
                if (current.StartMinute < previous.EndMinute &&
                    current.RequiredFacts.SetEquals(previous.RequiredFacts))
                {
                    errors.Add($"Overlapping schedule entries for {group.Key.NpcId}.");
                }
            }
        }

        return errors;
    }
}
