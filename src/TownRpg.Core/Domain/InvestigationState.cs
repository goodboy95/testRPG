namespace TownRpg.Core.Domain;

public sealed class InvestigationState
{
    public HashSet<string> Leads { get; set; } = new(StringComparer.Ordinal);
    public HashSet<string> KnownScheduleEntries { get; set; } = new(StringComparer.Ordinal);
    public List<TimelineEntry> Timeline { get; set; } = [];

    public void AddLead(string leadId)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(leadId);
        Leads.Add(leadId);
    }

    public void AddTimeline(int day, string textKey)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(textKey);
        if (Timeline.Any(entry => entry.Day == day && entry.TextKey == textKey))
        {
            return;
        }

        Timeline.Add(new TimelineEntry { Day = day, TextKey = textKey });
    }
}

public sealed class TimelineEntry
{
    public int Day { get; init; }
    public required string TextKey { get; init; }
}
