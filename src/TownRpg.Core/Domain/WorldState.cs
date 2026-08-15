namespace TownRpg.Core.Domain;

public sealed class WorldState
{
    public const int MorningStartMinute = 8 * 60;
    public const int DeepNightMinute = 23 * 60;

    public int Day { get; set; } = 1;
    public int MinuteOfDay { get; set; } = MorningStartMinute;
    public int StoryStage { get; set; }
    public StoryRoute Route { get; set; } = StoryRoute.Undecided;
    public ExposureStage Exposure { get; set; } = ExposureStage.Unnoticed;
    public int RitualNodesRemaining { get; set; } = 3;
    public HashSet<string> Facts { get; set; } = new(StringComparer.Ordinal);
    public HashSet<string> Evidence { get; set; } = new(StringComparer.Ordinal);

    public bool IsDeepNight => MinuteOfDay >= DeepNightMinute;

    public bool AddFact(string factId)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(factId);
        return Facts.Add(factId);
    }

    public bool HasFact(string factId) => Facts.Contains(factId);

    public bool AddEvidence(string evidenceId)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(evidenceId);
        return Evidence.Add(evidenceId);
    }

    public void AdvanceMinutes(int minutes)
    {
        if (minutes < 0)
        {
            throw new ArgumentOutOfRangeException(nameof(minutes), "Time cannot run backwards.");
        }

        MinuteOfDay = Math.Min(DeepNightMinute, MinuteOfDay + minutes);
    }

    public void StartNextDay()
    {
        Day++;
        MinuteOfDay = MorningStartMinute;
    }

    public void RaiseExposure(int steps = 1)
    {
        if (steps < 0)
        {
            throw new ArgumentOutOfRangeException(nameof(steps));
        }

        Exposure = (ExposureStage)Math.Min(
            (int)ExposureStage.Identified,
            (int)Exposure + steps);
    }
}
