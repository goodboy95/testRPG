using System.Numerics;

namespace TownRpg.Presentation;

// Transient presentation math; intentionally independent of Godot and save data.
public sealed class CharacterLocomotion
{
    public const float WalkSpeed = 1.4f;
    public const float JogSpeed = 3.1f;
    public Vector2 Velocity { get; private set; }
    public float Yaw { get; private set; } = MathF.PI;
    public bool IsTurning { get; private set; }
    public float TurnProgress { get; private set; }
    public float TurnAngle { get; private set; }
    public float TurnDuration => MathF.Abs(TurnAngle) > MathF.PI * .75f ? .4f : .3f;
    public string TurnAnimation => $"turn_{(TurnAngle >= 0 ? "left" : "right")}_{(MathF.Abs(TurnAngle) > MathF.PI * .75f ? 180 : 90)}";
    public float ActualSpeed { get; private set; }
    public bool Moving { get; private set; }
    private float _turnStart;
    private float _turnTarget;

    public static float AngleDifference(float from, float to)
    {
        var angle = MathF.Atan2(MathF.Sin(to - from), MathF.Cos(to - from));
        return MathF.Abs(MathF.Abs(angle) - MathF.PI) < .00001f ? MathF.PI : angle;
    }
    public static float SmoothStep(float t) => t * t * (3f - 2f * t);

    public Vector2 Step(Vector2 input, bool slow, float maximumSpeed, float delta, bool allowTurn = true)
    {
        if (!float.IsFinite(delta) || delta <= 0f) return Velocity;
        delta = MathF.Min(delta, .1f);
        var magnitude = MathF.Min(input.Length(), 1f);
        var hasInput = magnitude > .001f;
        var direction = hasInput ? Vector2.Normalize(input) : Vector2.Zero;
        var targetYaw = hasInput ? MathF.Atan2(direction.X, direction.Y) : Yaw;
        var difference = AngleDifference(Yaw, targetYaw);

        // A new direction or released input cancels a pivot on the same physics tick.
        if (IsTurning && (!hasInput || MathF.Abs(AngleDifference(_turnTarget, targetYaw)) > .35f))
            IsTurning = false;
        if (!IsTurning && allowTurn && hasInput && Velocity.Length() < .35f &&
            MathF.Abs(difference) >= MathF.PI * .5f - .001f)
        {
            IsTurning = true;
            _turnStart = Yaw;
            _turnTarget = targetYaw;
            TurnAngle = difference;
            TurnProgress = 0f;
        }
        if (IsTurning)
        {
            TurnProgress = MathF.Min(1f, TurnProgress + delta / TurnDuration);
            Yaw = _turnStart + TurnAngle * SmoothStep(TurnProgress);
            Velocity = Vector2.Zero;
            if (TurnProgress >= 1f) IsTurning = false;
            return Velocity;
        }

        var reversing = allowTurn && hasInput && MathF.Abs(difference) > MathF.PI * .75f;
        var target = reversing ? Vector2.Zero : direction * magnitude *
            (slow ? MathF.Min(WalkSpeed, maximumSpeed) : maximumSpeed);
        var acceleration = maximumSpeed / (target.LengthSquared() < Velocity.LengthSquared() ? .10f : .15f);
        var change = target - Velocity;
        Velocity = change.Length() <= acceleration * delta ? target :
            Velocity + Vector2.Normalize(change) * acceleration * delta;
        return Velocity;
    }

    public void ObserveMotion(Vector2 actualVelocity, float delta)
    {
        ActualSpeed = actualVelocity.Length();
        Moving = ActualSpeed > (Moving ? .025f : .065f);
        // Feed collision resolution back into the next tick; never build speed against a wall.
        Velocity = actualVelocity;
        if (!IsTurning && Moving)
        {
            var target = MathF.Atan2(actualVelocity.X, actualVelocity.Y);
            Yaw += Math.Clamp(AngleDifference(Yaw, target), -12f * delta, 12f * delta);
        }
    }

    public void Reset(float yaw)
    {
        Yaw = yaw;
        Velocity = Vector2.Zero;
        ActualSpeed = 0f;
        Moving = false;
        IsTurning = false;
        TurnProgress = 0f;
        TurnAngle = 0f;
    }
}
