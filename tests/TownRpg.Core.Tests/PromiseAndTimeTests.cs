using TownRpg.Core.Domain;
using TownRpg.Core.Services;

namespace TownRpg.Core.Tests;

public sealed class PromiseServiceTests
{
    [Fact]
    public void Create_IsIdempotentAndRecordsCreationDay()
    {
        var state = new GameState();
        state.World.Day = 2;
        var service = new PromiseService(state);

        var first = service.Create("keep_secret", "protagonist", "resident", "保守秘密");
        var duplicate = service.Create("keep_secret", "someone_else", "another", "不同内容");

        Assert.Same(first, duplicate);
        Assert.Equal(2, first.CreatedDay);
        Assert.Equal("protagonist", first.PromisorId);
        Assert.Single(state.Promises);
    }

    [Theory]
    [InlineData(PromiseStatus.Fulfilled)]
    [InlineData(PromiseStatus.Explained)]
    [InlineData(PromiseStatus.Broken)]
    public void Resolve_AllowsEachTerminalStateFromPending(PromiseStatus terminal)
    {
        var state = new GameState();
        var service = new PromiseService(state);
        service.Create("promise", "a", "b", "description");

        var resolved = service.Resolve("promise", terminal);

        Assert.Equal(terminal, resolved.Status);
    }

    [Fact]
    public void Resolve_RejectsPendingAndAnySecondResolution()
    {
        var state = new GameState();
        var service = new PromiseService(state);
        service.Create("promise", "a", "b", "description");

        Assert.Throws<InvalidOperationException>(() => service.Resolve("promise", PromiseStatus.Pending));
        service.Resolve("promise", PromiseStatus.Fulfilled);
        Assert.Throws<InvalidOperationException>(() => service.Resolve("promise", PromiseStatus.Explained));
    }

    [Fact]
    public void PendingPromise_DoesNotExpireWhenDaysPass()
    {
        var state = new GameState();
        var service = new PromiseService(state);
        var promise = service.Create("promise", "a", "b", "description");

        state.World.StartNextDay();
        state.World.StartNextDay();
        state.World.StartNextDay();

        Assert.Equal(4, state.World.Day);
        Assert.Equal(PromiseStatus.Pending, promise.Status);
    }
}

public sealed class WorldStateTimeTests
{
    [Fact]
    public void TimeService_AdvancesOnlyFromMovementAndHonorsModalGate()
    {
        var state = new GameState();
        var time = new TimeService(state);

        time.AdvanceMovementSeconds(4, modalOpen: true);
        Assert.Equal(WorldState.MorningStartMinute, state.World.MinuteOfDay);

        time.AdvanceMovementSeconds(4, modalOpen: false);
        Assert.Equal(WorldState.MorningStartMinute + 3, state.World.MinuteOfDay);
    }

    [Fact]
    public void TimeService_DoesNotAdvancePastDeepNightEvenWithMovement()
    {
        var state = new GameState();
        state.World.MinuteOfDay = WorldState.DeepNightMinute;
        var time = new TimeService(state);

        time.AdvanceMovementSeconds(120, modalOpen: false);

        Assert.Equal(WorldState.DeepNightMinute, state.World.MinuteOfDay);
        Assert.Equal(1, state.World.Day);
    }

    [Fact]
    public void NewWorld_StartsAtEightOnDayOne()
    {
        var world = new WorldState();

        Assert.Equal(1, world.Day);
        Assert.Equal(8 * 60, world.MinuteOfDay);
        Assert.False(world.IsDeepNight);
    }

    [Fact]
    public void AdvanceMinutes_StopsAtDeepNight()
    {
        var world = new WorldState();

        world.AdvanceMinutes(10_000);

        Assert.Equal(WorldState.DeepNightMinute, world.MinuteOfDay);
        Assert.True(world.IsDeepNight);
    }

    [Fact]
    public void AdvanceMinutes_RejectsMovingTimeBackwards()
    {
        var world = new WorldState();

        Assert.Throws<ArgumentOutOfRangeException>(() => world.AdvanceMinutes(-1));
    }

    [Fact]
    public void StartNextDay_IsTheOnlyOperationThatAdvancesDayAndResetsClock()
    {
        var world = new WorldState();
        world.AdvanceMinutes(300);

        world.StartNextDay();

        Assert.Equal(2, world.Day);
        Assert.Equal(WorldState.MorningStartMinute, world.MinuteOfDay);
    }
}
