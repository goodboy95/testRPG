using System.Numerics;
using TownRpg.Presentation;
using Xunit;

namespace TownRpg.Core.Tests;

public sealed class CharacterLocomotionTests
{
    private static void Tick(CharacterLocomotion motion, Vector2 input, bool slow = false, float dt = 1f / 60f, float maximumSpeed = 3.1f)
    {
        var velocity = motion.Step(input, slow, maximumSpeed, dt);
        motion.ObserveMotion(velocity, dt);
    }

    [Theory]
    [InlineData(30)] [InlineData(60)] [InlineData(120)]
    public void AccelerationAndBrakingReachTargetsAtDifferentRates(int fps)
    {
        var m = new CharacterLocomotion();
        m.Reset(0);
        for (var i = 0; i < fps / 5; i++) Tick(m, Vector2.UnitY, dt: 1f / fps);
        Assert.InRange(m.ActualSpeed, 3.099f, 3.101f);
        for (var i = 0; i < fps / 5; i++) Tick(m, Vector2.Zero, dt: 1f / fps);
        Assert.Equal(0f, m.ActualSpeed);
    }

    [Fact]
    public void SlowModifierAndAnalogMagnitudeControlSpeed()
    {
        var m = new CharacterLocomotion(); m.Reset(0);
        for (var i = 0; i < 30; i++) Tick(m, Vector2.UnitY, true);
        Assert.Equal(1.4f, m.ActualSpeed, 3);
        for (var i = 0; i < 30; i++) Tick(m, Vector2.UnitY * .3f);
        Assert.Equal(.93f, m.ActualSpeed, 3);
        for (var i = 0; i < 30; i++) Tick(m, Vector2.One);
        Assert.InRange(m.ActualSpeed, 3.099f, 3.101f);
    }

    [Theory]
    [InlineData(30)] [InlineData(60)] [InlineData(120)]
    public void VillageSpeedAcceleratesAndBrakesWithoutChangingTiming(int fps)
    {
        var m = new CharacterLocomotion(); m.Reset(0);
        var dt = 1f / fps;
        for (var i = 0; i < (int)MathF.Ceiling(.15f * fps); i++)
            Tick(m, Vector2.UnitY, dt: dt, maximumSpeed: 4.2f);
        Assert.InRange(m.ActualSpeed, 4.199f, 4.201f);
        var brakingDistance = 0f;
        for (var i = 0; i < (int)MathF.Ceiling(.10f * fps); i++)
        {
            Tick(m, Vector2.Zero, dt: dt, maximumSpeed: 4.2f);
            brakingDistance += m.ActualSpeed * dt;
        }
        Assert.InRange(m.ActualSpeed, 0, .00001f);
        Assert.InRange(brakingDistance, .1f, .22f);
    }

    [Theory]
    [InlineData(30)] [InlineData(60)] [InlineData(120)]
    public void VillageSpeedPreservesSlowAnalogAndDiagonalControl(int fps)
    {
        var m = new CharacterLocomotion(); m.Reset(0);
        void Hold(Vector2 input, bool slow = false)
        {
            for (var i = 0; i < fps; i++) Tick(m, input, slow, 1f / fps, 4.2f);
        }
        Hold(Vector2.UnitY, true); Assert.Equal(1.4f, m.ActualSpeed, 3);
        Hold(Vector2.UnitY * .3f); Assert.Equal(1.26f, m.ActualSpeed, 3);
        Hold(Vector2.One); Assert.InRange(m.ActualSpeed, 4.199f, 4.201f);
        m.ObserveMotion(Vector2.Zero, 1f / fps);
        Assert.False(m.Moving);
        m.Reset(0); Assert.Equal(Vector2.Zero, m.Velocity);
    }

    [Theory]
    [InlineData(1, 0, "turn_left_90")]
    [InlineData(-1, 0, "turn_right_90")]
    [InlineData(0, -1, "turn_left_180")]
    public void PivotReachesTargetWithoutTranslation(float x, float y, string animation)
    {
        var m = new CharacterLocomotion(); m.Reset(0);
        var input = new Vector2(x, y);
        Tick(m, input);
        Assert.True(m.IsTurning);
        Assert.Equal(animation, m.TurnAnimation);
        for (var i = 0; i < 60 && m.IsTurning; i++)
        {
            Assert.Equal(Vector2.Zero, m.Velocity);
            Tick(m, input);
        }
        Assert.False(m.IsTurning);
        Assert.InRange(MathF.Abs(CharacterLocomotion.AngleDifference(m.Yaw, MathF.Atan2(x, y))), 0, .001f);
    }

    [Fact]
    public void PivotIsInterruptibleAndResetDiscardsAllMotion()
    {
        var m = new CharacterLocomotion(); m.Reset(0);
        Tick(m, -Vector2.UnitY);
        Tick(m, Vector2.Zero);
        Assert.False(m.IsTurning);
        Tick(m, -Vector2.UnitY);
        Tick(m, Vector2.UnitY);
        Assert.False(m.IsTurning);
        Assert.True(m.Velocity.Y > 0);
        m.Reset(1.2f);
        Assert.Equal(1.2f, m.Yaw);
        Assert.Equal(Vector2.Zero, m.Velocity);
        Assert.False(m.Moving);
        Assert.Equal(0f, m.TurnProgress);
    }

    [Fact]
    public void ReversalBrakesBeforePivotAndBlockedMotionStopsGait()
    {
        var m = new CharacterLocomotion(); m.Reset(0);
        for (var i = 0; i < 30; i++) Tick(m, Vector2.UnitY);
        Tick(m, -Vector2.UnitY);
        Assert.InRange(m.Velocity.Y, 0f, 3.099f);
        for (var i = 0; i < 10 && !m.IsTurning; i++) Tick(m, -Vector2.UnitY);
        Assert.True(m.IsTurning);
        m.Reset(0);
        for (var i = 0; i < 20; i++)
        {
            m.Step(Vector2.UnitY, false, 3.1f, 1f / 60f);
            m.ObserveMotion(Vector2.Zero, 1f / 60f);
        }
        Assert.False(m.Moving);
        Assert.Equal(Vector2.Zero, m.Velocity);
    }

    [Fact]
    public void StationaryCameraDoesNotChangeFacingAndNoiseHasHysteresis()
    {
        var m = new CharacterLocomotion(); m.Reset(3.1f);
        Tick(m, Vector2.Zero);
        Assert.Equal(3.1f, m.Yaw);
        m.ObserveMotion(Vector2.UnitY * .04f, .016f);
        Assert.False(m.Moving);
        m.ObserveMotion(Vector2.UnitY * .08f, .016f);
        Assert.True(m.Moving);
        m.ObserveMotion(Vector2.UnitY * .04f, .016f);
        Assert.True(m.Moving);
        m.ObserveMotion(Vector2.Zero, .016f);
        Assert.False(m.Moving);
    }
}
