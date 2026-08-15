using TownRpg.Core.Domain;
using TownRpg.Core.Services;

namespace TownRpg.Core.Tests;

public sealed class EventSchedulerTests
{
    [Fact]
    public void MainInvestigation_RemainsAvailableAfterSleepingSeveralDays()
    {
        var world = new WorldState();
        world.AddFact("railway_notice_read");
        world.StartNextDay();
        world.StartNextDay();
        world.StartNextDay();
        var definition = new EventDefinition
        {
            Id = "first_anomaly",
            LocationId = "abandoned_railway",
            StartMinute = WorldState.MorningStartMinute,
            RequiredFacts = new HashSet<string>(StringComparer.Ordinal) { "railway_notice_read" },
            Priority = EventPriority.DangerWarning
        };

        var available = EventScheduler.AvailableEvents(
            [definition],
            world,
            "abandoned_railway",
            new HashSet<string>(StringComparer.Ordinal));

        Assert.Equal(4, world.Day);
        Assert.Same(definition, Assert.Single(available));
    }

    [Fact]
    public void DangerWarning_SortsAheadOfOrdinaryEvents()
    {
        var world = new WorldState();
        var ordinary = new EventDefinition
        {
            Id = "ordinary",
            LocationId = "station",
            StartMinute = 0,
            Priority = EventPriority.Ambient
        };
        var warning = new EventDefinition
        {
            Id = "warning",
            LocationId = "station",
            StartMinute = 0,
            Priority = EventPriority.DangerWarning
        };

        var available = EventScheduler.AvailableEvents(
            [ordinary, warning],
            world,
            "station",
            new HashSet<string>(StringComparer.Ordinal));

        Assert.Equal([warning, ordinary], available);
    }
}
