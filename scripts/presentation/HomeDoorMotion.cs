using System.Numerics;

namespace TownRpg.Presentation;

/// <summary>Transient door animation; persistence captures only the last stable endpoint.</summary>
public sealed class HomeDoorMotion(HomeLayout3D.Door definition)
{
    public const float Duration = .45f;
    public float Progress { get; private set; }
    public bool IsMoving { get; private set; }
    public bool IsOpen => Progress >= 1 && !IsMoving;
    private bool _opening;

    public bool Request(Vector2 player)
    {
        if (IsMoving || definition.SweepBounds().Contains(player)) return false;
        _opening = !IsOpen;
        IsMoving = true;
        return true;
    }

    public void Step(float delta)
    {
        if (!IsMoving) return;
        Progress = Math.Clamp(Progress + Math.Max(delta, 0) / Duration * (_opening ? 1 : -1), 0, 1);
        if (Progress is 0 or 1) IsMoving = false;
    }

    public void Reset() { Progress = 0; IsMoving = false; _opening = false; }
    public void Restore(bool open) { Progress = open ? 1 : 0; IsMoving = false; _opening = open; }
    public bool StableOpen => IsMoving ? !_opening : IsOpen;
}
