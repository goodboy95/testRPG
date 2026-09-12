using System.Numerics;
using TownRpg.Presentation;

namespace TownRpg.Core.Tests;

public sealed class CompanionMotionTests
{
    [Theory]
    [InlineData(30)] [InlineData(60)] [InlineData(120)]
    public void ApproachBrakesAndSettlesWithoutOscillation(int fps)
    {
        var motion = new CompanionMotionState();
        var distance = 3f;
        for (var i = 0; i < fps * 4; i++)
        {
            var step = motion.Step(distance, 3.4f, 1f / fps, false);
            Assert.InRange(step, 0, distance);
            distance -= step;
        }
        Assert.InRange(distance, 0, .105f);
        Assert.Equal(0, motion.Speed);
        Assert.Equal(0, motion.Step(.18f, 3.4f, 1f / fps, false));
    }

    [Fact]
    public void PoseLifetimeStartsAtDestinationAndRepeatedRequestsDoNotRestart()
    {
        var motion = new CompanionMotionState();
        motion.Request("operate", Vector3.One, Vector3.UnitZ, 1.35f, false);
        for (var i = 0; i < 200; i++) motion.TickPose(2, .025f, false);
        Assert.Equal(1.35f, motion.Remaining);
        motion.TickPose(.01f, .5f, false);
        motion.Request("operate", Vector3.One, Vector3.UnitZ, 1.35f, false);
        Assert.Equal(.85f, motion.Remaining, 3);
        motion.TickPose(.01f, 1, false);
        Assert.Null(motion.Pose);
    }

    [Fact]
    public void HeldPosePersistsUntilCancelledAndPauseStopsMovement()
    {
        var motion = new CompanionMotionState();
        motion.Request("hold", Vector3.Zero, Vector3.UnitZ, 0, true);
        motion.TickPose(0, 100, false);
        Assert.True(motion.Arrived);
        Assert.Equal("hold", motion.Pose);
        motion.Step(5, 3.4f, .1f, false);
        Assert.Equal(0, motion.Step(5, 3.4f, .1f, true));
        Assert.Equal(0, motion.Speed);
        motion.Reset();
        Assert.Null(motion.Pose);
        Assert.False(motion.Arrived);
    }

    [Fact]
    public void PauseDoesNotSpendPoseDurationAndReplacementClearsArrival()
    {
        var motion = new CompanionMotionState();
        motion.Request("operate", Vector3.Zero, Vector3.UnitZ, 1, false);
        motion.TickPose(0, .5f, true);
        Assert.Equal(1, motion.Remaining);
        motion.TickPose(0, .25f, false);
        motion.Request("observe", Vector3.One, Vector3.UnitX, 0, true);
        Assert.False(motion.Arrived);
        Assert.Equal(Vector3.One, motion.Destination);
    }
}
