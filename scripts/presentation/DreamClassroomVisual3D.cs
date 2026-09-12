using Godot;
using TownRpg.Core.Domain;

namespace TownRpg.Presentation;

public partial class DreamClassroomVisual3D : Node3D
{
    private sealed record CollisionSpec(string Name, Vector3 Position, Vector3 Size, bool Furniture);

    private static readonly CollisionSpec[] CollisionSpecs = BuildCollisionSpecs();

    private static readonly Vector3[] SafeWaypoints =
    [
        new(0f, 0, -2.55f), new(-3.40f, 0, -2.55f), new(3.40f, 0, -2.55f),
        new(-4.70f, 0, -1.45f), new(0f, 0, -1.45f), new(4.70f, 0, -1.45f),
        new(-4.70f, 0, 0.55f), new(0f, 0, 0.55f), new(4.70f, 0, 0.55f),
        new(-4.70f, 0, 2.30f), new(0f, 0, 2.30f), new(4.70f, 0, 2.30f),
        new(-4.25f, 0, 3.75f), new(-1.0f, 0, 3.75f), new(1.0f, 0, 3.75f), new(4.25f, 0, 3.75f),
        new(5.10f, 0, 2.85f)
    ];

    private const float PlayerRadius = 0.30f;
    private Node3D _model = null!;
    private Node3D _windowCacheClosed = null!;
    private Node3D _windowCacheOpen = null!;
    private Node3D _clockHandPickup = null!;
    private StatefulObjectVisual3D _blackboard = null!;
    private StatefulObjectVisual3D _clock = null!;
    private StatefulObjectVisual3D _roster = null!;
    private StatefulObjectVisual3D _teacherDesk = null!;
    private StatefulObjectVisual3D _mapCabinet = null!;
    private StatefulObjectVisual3D _transomCache = null!;
    private Node3D? _debugHost;
    private readonly Queue<Vector3> _companionPath = new();
    private Vector3 _companionDestination;
    private bool[,]? _waypointConnections;

    public int CollisionBodyCount => CollisionSpecs.Length;
    public int FurnitureCollisionCount => CollisionSpecs.Count(spec => spec.Furniture);
    public Rect2 WalkableBounds => new(-5.7f, -4.2f, 11.4f, 8.4f);
    public bool WindowCacheOpen => _windowCacheOpen.Visible;
    public bool ClockHandPickupVisible => _clockHandPickup.Visible;
    public string BlackboardVariant => _blackboard.CurrentVariant;
    public string ClockVariant => _clock.CurrentVariant;
    public string RosterVariant => _roster.CurrentVariant;
    public string TeacherDeskVariant => _teacherDesk.CurrentVariant;
    public string MapCabinetVariant => _mapCabinet.CurrentVariant;
    public string TransomCacheVariant => _transomCache.CurrentVariant;
    public Node3D DoorHinge { get; private set; } = null!;

    public override void _Ready()
    {
        _model = GetNode<Node3D>("../ClassroomModel");
        ApplyRuntimeTextureFiltering(_model);
        _windowCacheClosed = RequiredNode("WindowCache_Closed");
        _windowCacheOpen = RequiredNode("WindowCache_Open");
        _clockHandPickup = RequiredNode("ClockHand_Pickup");
        _blackboard = Variants(("blank", "Blackboard_Blank"), ("revealed", "Blackboard_Revealed"));
        _clock = Variants(
            ("missing", "Clock_Missing"), ("hand_only", "Clock_HandOnly"),
            ("key_only", "Clock_KeyOnly"), ("ready", "Clock_Ready"), ("restored", "Clock_Restored"));
        _roster = Variants(
            ("missing", "Roster_Missing"), ("inserted", "Roster_Inserted"), ("aligned", "Roster_Aligned"));
        _teacherDesk = Variants(("closed", "TeacherDesk_Closed"), ("open", "TeacherDesk_Open"));
        _mapCabinet = Variants(("closed", "MapCabinet_Closed"), ("open", "MapCabinet_Open"));
        _transomCache = Variants(("closed", "TransomCache_Closed"), ("open", "TransomCache_Open"));
        DoorHinge = RequiredNode("Door_East_Hinge");
        HideDirectionalScratchMeshes(_model);
        BuildSeparatedClueProps();
        RequiredNode("Blackboard_Revealed").AddChild(new Label3D
        {
            Name = "RevealedOrderText",
            Text = DreamPrologueText.Get("DREAM_V6_WORLD_BLACKBOARD_ORDER"),
            Position = new Vector3(0f, 2.12f, -4.10f),
            FontSize = 40,
            PixelSize = 0.0034f,
            Modulate = new Color("cecdb3"),
            OutlineSize = 0
        });
        BuildCollisionBodies();
    }

    public void ApplyState(DreamEscapeState state)
    {
        var windowSolved = state.SolvedPuzzles.Contains(DreamEscapeIds.PuzzleWindowLatches);
        _windowCacheClosed.Visible = !windowSolved;
        _windowCacheOpen.Visible = windowSolved;
        _clockHandPickup.Visible = !state.SolvedPuzzles.Contains(DreamEscapeIds.PickupClockHand);
        _blackboard.SetVariant(state.SolvedPuzzles.Contains(DreamEscapeIds.PuzzleBlackboard) ? "revealed" : "blank");

        var clockSolved = state.SolvedPuzzles.Contains(DreamEscapeIds.PuzzleClockRestore);
        var handInstalled = state.InstalledParts.Contains(DreamEscapeIds.SlotClassroomClockHand) || clockSolved;
        var keyInstalled = state.InstalledParts.Contains(DreamEscapeIds.SlotClassroomClockKey) || clockSolved;
        _clock.SetVariant(clockSolved ? "restored" : handInstalled && keyInstalled ? "ready" :
            handInstalled ? "hand_only" : keyInstalled ? "key_only" : "missing");

        var rosterSolved = state.SolvedPuzzles.Contains(DreamEscapeIds.PuzzleRosterSlider);
        var rosterInstalled = state.InstalledParts.Contains(DreamEscapeIds.SlotClassroomRosterSlider) || rosterSolved;
        _roster.SetVariant(rosterSolved ? "aligned" : rosterInstalled ? "inserted" : "missing");
        _teacherDesk.SetVariant(state.SolvedPuzzles.Contains(DreamEscapeIds.PuzzleTeacherDesk) ? "open" : "closed");
        _mapCabinet.SetVariant(state.SolvedPuzzles.Contains(DreamEscapeIds.PuzzleMapCabinet) ? "open" : "closed");
        _transomCache.SetVariant("closed");
    }

    public void ApplySettings(PlayerSettings settings) => _ = settings;

    public bool IsBlocked(Vector3 playerPosition)
    {
        if (!WalkableBounds.HasPoint(new Vector2(playerPosition.X, playerPosition.Z))) return true;
        return CollisionSpecs.Where(spec => spec.Furniture || spec.Name == "DoorBarrier").Any(spec =>
        {
            var rect = new Rect2(
                spec.Position.X - spec.Size.X / 2f,
                spec.Position.Z - spec.Size.Z / 2f,
                spec.Size.X,
                spec.Size.Z).Grow(PlayerRadius);
            return rect.HasPoint(new Vector2(playerPosition.X, playerPosition.Z));
        });
    }

    public Vector3 ResolveSpawn(Vector3 requestedPosition)
    {
        requestedPosition = new Vector3(requestedPosition.X, 0f, requestedPosition.Z);
        if (!IsBlocked(requestedPosition)) return requestedPosition;
        return SafeWaypoints
            .Where(point => !IsBlocked(point))
            .OrderBy(point => point.DistanceSquaredTo(requestedPosition))
            .FirstOrDefault(new Vector3(0f, 0f, -2.55f));
    }

    // Only this room opts into this route resolver; other regions retain their own behavior.
    public Vector3 AdvanceCompanion(Vector3 current, Vector3 desired, float distance)
    {
        current = ResolveSpawn(current);
        desired = ResolveSpawn(desired);
        if (ClearSegment(current, desired))
        {
            _companionPath.Clear();
            return current.MoveToward(desired, distance);
        }
        if (_companionPath.Count == 0 || desired.DistanceTo(_companionDestination) > 0.75f ||
            !ClearSegment(current, _companionPath.Peek()))
        {
            _companionPath.Clear();
            _companionDestination = desired;
            foreach (var point in FindCompanionRoute(current, desired)) _companionPath.Enqueue(point);
        }
        while (_companionPath.TryPeek(out var reached) && current.DistanceTo(reached) < 0.04f)
            _companionPath.Dequeue();
        return _companionPath.TryPeek(out var next) ? current.MoveToward(next, distance) : current;
    }

    private bool ClearSegment(Vector3 from, Vector3 to)
    {
        var samples = Math.Max(1, (int)Math.Ceiling(from.DistanceTo(to) / 0.10f));
        for (var index = 0; index <= samples; index++)
            if (IsBlocked(from.Lerp(to, (float)index / samples))) return false;
        return true;
    }

    private IReadOnlyList<Vector3> FindCompanionRoute(Vector3 from, Vector3 to)
    {
        var count = SafeWaypoints.Length;
        if (_waypointConnections is null)
        {
            _waypointConnections = new bool[count, count];
            for (var i = 0; i < count; i++)
            for (var j = i + 1; j < count; j++)
                _waypointConnections[i, j] = _waypointConnections[j, i] = ClearSegment(SafeWaypoints[i], SafeWaypoints[j]);
        }
        var points = SafeWaypoints.Concat(new[] { from, to }).ToArray();
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
                var connected = node < count && next < count
                    ? _waypointConnections[node, next] : ClearSegment(points[node], points[next]);
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

    public void SetCollisionDebug(bool visible)
    {
        if (!visible)
        {
            _debugHost?.QueueFree();
            _debugHost = null;
            return;
        }
        if (_debugHost is not null) return;
        _debugHost = new Node3D { Name = "CollisionDebug" };
        AddChild(_debugHost);
        foreach (var spec in CollisionSpecs)
        {
            var material = new StandardMaterial3D
            {
                AlbedoColor = spec.Furniture ? new Color("ffad3b55") : new Color("ff4c4c3c"),
                ShadingMode = BaseMaterial3D.ShadingModeEnum.Unshaded,
                Transparency = BaseMaterial3D.TransparencyEnum.Alpha,
                NoDepthTest = true
            };
            _debugHost.AddChild(new MeshInstance3D
            {
                Position = spec.Position,
                Mesh = new BoxMesh { Size = spec.Size },
                MaterialOverride = material,
                CastShadow = GeometryInstance3D.ShadowCastingSetting.Off
            });
        }
    }

    private static CollisionSpec[] BuildCollisionSpecs()
    {
        var specs = new List<CollisionSpec>
        {
            new("Floor", new Vector3(0, -0.09f, 0), new Vector3(12f, 0.18f, 9f), false),
            new("Ceiling", new Vector3(0, 4.05f, 0), new Vector3(12f, 0.18f, 9f), false),
            new("WallNorth", new Vector3(0, 2f, -4.5f), new Vector3(12.3f, 4f, 0.20f), false),
            new("WallSouth", new Vector3(0, 2f, 4.5f), new Vector3(12.3f, 4f, 0.20f), false),
            new("WallWest", new Vector3(-6f, 2f, 0), new Vector3(0.20f, 4f, 9f), false),
            new("WallEastBack", new Vector3(6f, 2f, -0.88f), new Vector3(0.20f, 4f, 7.25f), false),
            new("WallEastFront", new Vector3(6f, 2f, 4.23f), new Vector3(0.20f, 4f, 0.55f), false),
            new("WallEastDoorTop", new Vector3(6f, 3.25f, 3.35f), new Vector3(0.20f, 1.50f, 1.20f), false),
            new("DoorBarrier", new Vector3(5.86f, 1.18f, 3.35f), new Vector3(0.24f, 2.36f, 1.20f), false),
            new("TeacherDesk", new Vector3(0.55f, 0.50f, -3.25f), new Vector3(2.05f, 1.0f, 1.05f), true),
            new("MapCabinet", new Vector3(-5.05f, 1.15f, -3.78f), new Vector3(1.34f, 2.30f, 0.74f), true),
            new("ScheduleStand", new Vector3(-3.40f, 0.70f, -2.05f), new Vector3(0.78f, 1.40f, 0.44f), true)
        };
        var index = 1;
        foreach (var position in ClassroomLayout3D.DeskPositions)
            specs.Add(new CollisionSpec($"StudentDesk{index++:00}",
                position + new Vector3(0f, ClassroomLayout3D.CollisionSize.Y / 2f, ClassroomLayout3D.GroupDepthOffset),
                ClassroomLayout3D.CollisionSize, true));
        return specs.ToArray();
    }

    private void BuildCollisionBodies()
    {
        var host = new Node3D { Name = "CollisionBodies" };
        AddChild(host);
        foreach (var spec in CollisionSpecs)
        {
            var body = new StaticBody3D { Name = spec.Name, Position = spec.Position };
            body.AddChild(new CollisionShape3D { Shape = new BoxShape3D { Size = spec.Size } });
            host.AddChild(body);
        }
    }

    private static void ApplyRuntimeTextureFiltering(Node node)
    {
        if (node is MeshInstance3D mesh && mesh.Mesh is not null)
        {
            // Diffuse window glass represents the exterior and must not seal the daylight opening.
            if (mesh.Name == "Window_ThreeBay_Glass")
                mesh.CastShadow = GeometryInstance3D.ShadowCastingSetting.Off;
            for (var surface = 0; surface < mesh.Mesh.GetSurfaceCount(); surface++)
            {
                if (mesh.GetActiveMaterial(surface) is BaseMaterial3D material)
                    material.TextureFilter = BaseMaterial3D.TextureFilterEnum.LinearWithMipmapsAnisotropic;
            }
        }
        foreach (var child in node.GetChildren()) ApplyRuntimeTextureFiltering(child);
    }

    private static void HideDirectionalScratchMeshes(Node node)
    {
        if (node is Node3D node3D && node.Name.ToString().Contains("Scratch", StringComparison.OrdinalIgnoreCase))
            node3D.Visible = false;
        foreach (var child in node.GetChildren()) HideDirectionalScratchMeshes(child);
    }

    private void BuildSeparatedClueProps()
    {
        var host = new Node3D { Name = "SeparatedClueProps" };
        AddChild(host);
        var paper = new StandardMaterial3D
        {
            AlbedoColor = new Color("d2c394"),
            Roughness = 0.92f
        };
        AddPaper(host, "WindowMaintenanceCard", new Vector3(-3.55f, 0.753f, 2.25f), paper, DreamPrologueText.Get("DREAM_V6_WORLD_WINDOW"));
        AddPaper(host, "MorningSchedule", new Vector3(-3.40f, 1.12f, -2.018f), paper, DreamPrologueText.Get("DREAM_V6_WORLD_SCHEDULE"), upright: true);
        AddPaper(host, "DutyNote", new Vector3(3.55f, 0.753f, 2.25f), paper, DreamPrologueText.Get("DREAM_V6_WORLD_DUTY"));
    }

    private static void AddPaper(Node parent, string name, Vector3 position, Material material, string text, bool upright = false)
    {
        var paper = new MeshInstance3D
        {
            Name = name,
            Position = position,
            RotationDegrees = upright ? Vector3.Zero : new Vector3(-90f, 0f, 0f),
            Mesh = new QuadMesh { Size = new Vector2(0.60f, 0.38f) },
            MaterialOverride = material
        };
        parent.AddChild(paper);
        parent.AddChild(new Label3D
        {
            Name = name + "Label",
            Text = text,
            Position = position + (upright ? new Vector3(0f, 0f, 0.003f) : new Vector3(0f, 0.003f, 0f)),
            RotationDegrees = upright ? Vector3.Zero : new Vector3(-90f, 0f, 0f),
            FontSize = 28,
            PixelSize = 0.0018f,
            Modulate = new Color("493b2d"),
            OutlineSize = 2
        });
    }

    private StatefulObjectVisual3D Variants(params (string Variant, string NodeName)[] entries) =>
        new(entries.ToDictionary(entry => entry.Variant, entry => RequiredNode(entry.NodeName), StringComparer.Ordinal));

    private Node3D RequiredNode(string name) =>
        _model.FindChild(name, true, false) as Node3D
        ?? throw new InvalidOperationException($"Classroom GLB is missing state node '{name}'.");
}
