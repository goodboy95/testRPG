using Godot;

namespace TownRpg.Presentation;

public readonly record struct LateRoomCollisionSpec(string Name, Vector3 Position, Vector3 Size, bool Furniture = false);

public static class LateRoomLayout
{
    public static bool IsBlocked(Vector3 p, Rect2 bounds, IReadOnlyList<LateRoomCollisionSpec> specs)
    {
        if (!bounds.HasPoint(new Vector2(p.X, p.Z))) return true;
        return specs.Where(s => s.Position.Y + s.Size.Y / 2 > .05f && s.Position.Y - s.Size.Y / 2 < 1.6f)
            .Any(s => new Rect2(s.Position.X - s.Size.X / 2, s.Position.Z - s.Size.Z / 2, s.Size.X, s.Size.Z)
                .Grow(.32f).HasPoint(new Vector2(p.X, p.Z)));
    }

    public static Vector3[] Waypoints(Rect2 bounds, Func<Vector3, bool> blocked, IEnumerable<Vector3> anchors)
    {
        var points = new List<Vector3>(anchors);
        for (var x = bounds.Position.X + .4f; x < bounds.End.X; x += .8f)
            for (var z = bounds.Position.Y + .4f; z < bounds.End.Y; z += .8f)
                points.Add(new Vector3(x, 0, z));
        return points.Where(p => !blocked(p)).Distinct().ToArray();
    }

    public static Vector3 ResolveSpawn(Vector3 p, Func<Vector3, bool> blocked, IReadOnlyList<Vector3> points)
    {
        p.Y = 0;
        return blocked(p) ? points.MinBy(q => q.DistanceSquaredTo(p)) : p;
    }
}
