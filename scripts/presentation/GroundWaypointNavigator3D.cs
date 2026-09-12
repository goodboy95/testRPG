using Godot;

namespace TownRpg.Presentation;

// A per-instance route cache for flat rooms with static furniture. It stores no game state.
public sealed class GroundWaypointNavigator3D(Vector3[] waypoints, Func<Vector3, bool> isBlocked,
    Func<Vector3, Vector3> resolvePosition)
{
    private readonly Queue<Vector3> _path = new();
    private Vector3 _destination;
    private bool[,]? _connections;

    public Vector3 Advance(Vector3 current, Vector3 desired, float distance)
    {
        current = resolvePosition(current);
        desired = resolvePosition(desired);
        if (ClearSegment(current, desired))
        {
            _path.Clear();
            return SafeStep(current, desired, distance);
        }
        if (_path.Count == 0 || desired.DistanceTo(_destination) > 0.75f ||
            !ClearSegment(current, _path.Peek()))
        {
            _path.Clear();
            _destination = desired;
            foreach (var point in FindRoute(current, desired)) _path.Enqueue(point);
        }
        // Finish the corner before advancing: skipping a nearby waypoint can cut through furniture.
        while (_path.TryPeek(out var reached) && current.DistanceTo(reached) < 0.0001f) _path.Dequeue();
        return _path.TryPeek(out var next) ? SafeStep(current, next, distance) : current;
    }

    private Vector3 SafeStep(Vector3 current, Vector3 target, float distance)
    {
        var candidate = current.MoveToward(target, distance);
        if (ClearSegment(current, candidate)) return candidate;
        _path.Clear();
        return current;
    }

    private bool ClearSegment(Vector3 from, Vector3 to)
    {
        var samples = Math.Max(1, (int)Math.Ceiling(from.DistanceTo(to) / 0.02f));
        for (var i = 0; i <= samples; i++)
            if (isBlocked(from.Lerp(to, (float)i / samples))) return false;
        return true;
    }

    private IReadOnlyList<Vector3> FindRoute(Vector3 from, Vector3 to)
    {
        var count = waypoints.Length;
        if (_connections is null)
        {
            _connections = new bool[count, count];
            for (var i = 0; i < count; i++)
            for (var j = i + 1; j < count; j++)
                _connections[i, j] = _connections[j, i] = ClearSegment(waypoints[i], waypoints[j]);
        }
        var points = waypoints.Concat(new[] { from, to }).ToArray();
        var costs = Enumerable.Repeat(float.PositiveInfinity, points.Length).ToArray();
        var previous = Enumerable.Repeat(-1, points.Length).ToArray();
        var queue = new PriorityQueue<int, float>();
        costs[count] = 0f;
        queue.Enqueue(count, 0f);
        while (queue.TryDequeue(out var node, out var cost))
        {
            if (cost > costs[node]) continue;
            if (node == count + 1) break;
            for (var next = 0; next < points.Length; next++)
            {
                if (next == node) continue;
                var connected = node < count && next < count ? _connections[node, next] : ClearSegment(points[node], points[next]);
                var nextCost = cost + points[node].DistanceTo(points[next]);
                if (!connected || nextCost >= costs[next]) continue;
                costs[next] = nextCost;
                previous[next] = node;
                queue.Enqueue(next, nextCost);
            }
        }
        if (previous[count + 1] < 0) return Array.Empty<Vector3>();
        var route = new List<Vector3>();
        for (var node = count + 1; node != count; node = previous[node]) route.Add(points[node]);
        route.Reverse();
        return route;
    }
}
