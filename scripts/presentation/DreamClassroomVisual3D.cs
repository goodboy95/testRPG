using Godot;
using TownRpg.Core.Domain;

namespace TownRpg.Presentation;

public partial class DreamClassroomVisual3D : Node3D
{
    private sealed record CollisionSpec(string Name, Vector3 Position, Vector3 Size, bool Furniture);

    private static readonly float[] DeskColumns = [-4.25f, -2.55f, -0.85f, 0.85f, 2.55f, 4.25f];
    private static readonly float[] DeskRows = [-0.40f, 1.45f, 3.05f];
    private static readonly CollisionSpec[] CollisionSpecs = BuildCollisionSpecs();

    private static readonly Vector3[] SafeWaypoints =
    [
        new(0f, 0, -2.55f), new(-3.40f, 0, -2.55f), new(3.40f, 0, -2.55f),
        new(-5.15f, 0, -1.45f), new(-1.70f, 0, -1.45f), new(1.70f, 0, -1.45f), new(5.15f, 0, -1.45f),
        new(-5.15f, 0, 0.55f), new(-1.70f, 0, 0.55f), new(1.70f, 0, 0.55f), new(5.15f, 0, 0.55f),
        new(-5.15f, 0, 2.20f), new(-1.70f, 0, 2.20f), new(1.70f, 0, 2.20f), new(5.15f, 0, 2.20f),
        new(-4.25f, 0, 4.05f), new(-1.70f, 0, 4.05f), new(1.70f, 0, 4.05f), new(4.25f, 0, 4.05f),
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
        _transomCache.SetVariant(state.SolvedPuzzles.Contains(DreamEscapeIds.PuzzleTransomCache) ? "open" : "closed");
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
            new("MapCabinet", new Vector3(-5.05f, 1.15f, -3.78f), new Vector3(1.20f, 2.30f, 0.72f), true)
        };
        var index = 1;
        foreach (var z in DeskRows)
        foreach (var x in DeskColumns)
            specs.Add(new CollisionSpec($"StudentDesk{index++:00}", new Vector3(x, 0.52f, z),
                new Vector3(0.82f, 1.04f, 1.18f), true));
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
        AddPaper(host, "WindowMaintenanceCard", new Vector3(-4.25f, 1.06f, 3.05f), paper, "窗扣维护\n上　下　中");
        AddPaper(host, "MorningSchedule", new Vector3(-1.70f, 0.86f, -1.45f), paper, "晨间作息\n7:00");
        AddPaper(host, "DutyNote", new Vector3(4.25f, 1.06f, 3.05f), paper, "值日便签\n第 2 项");
    }

    private static void AddPaper(Node parent, string name, Vector3 position, Material material, string text)
    {
        var paper = new MeshInstance3D
        {
            Name = name,
            Position = position,
            RotationDegrees = new Vector3(-90f, 0f, 0f),
            Mesh = new QuadMesh { Size = new Vector2(0.78f, 0.48f) },
            MaterialOverride = material
        };
        parent.AddChild(paper);
        parent.AddChild(new Label3D
        {
            Name = name + "Label",
            Text = text,
            Position = position + new Vector3(0f, 0.012f, 0f),
            RotationDegrees = new Vector3(-90f, 0f, 0f),
            FontSize = 28,
            PixelSize = 0.0025f,
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
