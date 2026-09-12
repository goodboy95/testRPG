using System.Numerics;

namespace TownRpg.Presentation;

// Presentation only: destination and animation lifetime never enter a save.
public sealed class CompanionMotionState
{
    public string? Pose { get; private set; }
    public Vector3 Destination { get; private set; }
    public Vector3 Facing { get; private set; }
    public float Remaining { get; private set; }
    public bool Held { get; private set; }
    public bool Arrived { get; private set; }
    public float Speed { get; private set; }
    private bool _following;

    public void Request(string pose, Vector3 destination, Vector3 facing, float seconds, bool held)
    {
        if (Pose == pose && Destination == destination && Facing == facing && Held == held) return;
        Pose = pose; Destination = destination; Facing = facing;
        Remaining = MathF.Max(0, seconds); Held = held; Arrived = false;
    }

    public void CancelPose() { Pose = null; Held = Arrived = false; Remaining = 0; }
    public void Reset() { CancelPose(); Stop(); }
    public void Stop() { Speed = 0; _following = false; }

    public float Step(float distance, float maximumSpeed, float delta, bool paused)
    {
        if (!float.IsFinite(delta) || delta <= 0) return 0;
        delta = MathF.Min(delta, .1f);
        if (paused) { Stop(); return 0; }
        var precise = Pose is not null;
        var stopDistance = precise ? .004f : .10f;
        var startDistance = precise ? .008f : .26f;
        _following = distance > (_following ? stopDistance : startDistance);
        const float acceleration = 7f;
        const float braking = 9f;
        var target = _following ? MathF.Min(maximumSpeed, MathF.Sqrt(2 * braking * MathF.Max(0, distance - stopDistance))) : 0;
        var change = (target < Speed ? braking : acceleration) * delta;
        Speed += Math.Clamp(target - Speed, -change, change);
        if (!_following) Speed = 0;
        return MathF.Min(distance, Speed * delta);
    }

    public void TickPose(float distance, float delta, bool paused)
    {
        if (Pose is null || paused || delta <= 0) return;
        Arrived = distance <= .01f;
        if (!Arrived || Held) return;
        Remaining = MathF.Max(0, Remaining - delta);
        if (Remaining == 0) CancelPose();
    }
}
