using Godot;
using TownRpg.Core.Domain;

namespace TownRpg.Presentation;

public partial class DreamClassroomVisual3D : Node3D
{
    private sealed record CollisionSpec(string Name, Vector3 Position, Vector3 Size, bool Furniture);

    private static readonly CollisionSpec[] CollisionSpecs =
    [
        new("Floor", new Vector3(0, -0.09f, 0), new Vector3(9.6f, 0.18f, 7.2f), false),
        new("WallNorth", new Vector3(0, 1.6f, -3.6f), new Vector3(9.9f, 3.2f, 0.18f), false),
        new("WallSouth", new Vector3(0, 1.6f, 3.6f), new Vector3(9.9f, 3.2f, 0.18f), false),
        new("WallWest", new Vector3(-4.8f, 1.6f, 0), new Vector3(0.18f, 3.2f, 7.2f), false),
        new("WallEastBack", new Vector3(4.8f, 1.6f, -0.98f), new Vector3(0.18f, 3.2f, 5.24f), false),
        new("WallEastFront", new Vector3(4.8f, 1.6f, 3.18f), new Vector3(0.18f, 3.2f, 0.84f), false),
        new("WallEastDoorTop", new Vector3(4.8f, 2.67f, 2.2f), new Vector3(0.18f, 1.06f, 1.12f), false),
        new("DoorBarrier", new Vector3(4.68f, 1.10f, 2.2f), new Vector3(0.24f, 2.20f, 1.12f), false),
        new("MapCabinet", new Vector3(-3.83f, 1.05f, -2.8f), new Vector3(1.2f, 2.1f, 0.72f), true),
        new("TeacherDesk", new Vector3(0.4f, 0.48f, -2.12f), new Vector3(1.95f, 0.96f, 1.04f), true),
        new("StudentDesk01", new Vector3(-3.35f, 0.52f, -0.10f), new Vector3(1.02f, 1.04f, 1.38f), true),
        new("StudentDesk02", new Vector3(-1.68f, 0.52f, -0.10f), new Vector3(1.02f, 1.04f, 1.38f), true),
        new("StudentDesk03", new Vector3(0.0f, 0.52f, -0.10f), new Vector3(1.02f, 1.04f, 1.38f), true),
        new("StudentDesk04", new Vector3(1.68f, 0.52f, -0.10f), new Vector3(1.02f, 1.04f, 1.38f), true),
        new("StudentDesk05", new Vector3(3.35f, 0.52f, -0.10f), new Vector3(1.02f, 1.04f, 1.38f), true),
        new("StudentDesk06", new Vector3(-3.35f, 0.52f, 1.63f), new Vector3(1.02f, 1.04f, 1.38f), true),
        new("StudentDesk07", new Vector3(-1.68f, 0.52f, 1.63f), new Vector3(1.02f, 1.04f, 1.38f), true),
        new("StudentDesk08", new Vector3(0.0f, 0.52f, 1.63f), new Vector3(1.02f, 1.04f, 1.38f), true),
        new("StudentDesk09", new Vector3(1.68f, 0.52f, 1.63f), new Vector3(1.02f, 1.04f, 1.38f), true),
        new("StudentDesk10", new Vector3(3.35f, 0.52f, 1.63f), new Vector3(1.02f, 1.04f, 1.38f), true)
    ];

    private static readonly Vector3[] SafeWaypoints =
    [
        new(0f, 0, 2.75f), new(-4.25f, 0, 2.75f), new(-2.55f, 0, 2.75f), new(-0.85f, 0, 2.75f),
        new(0.85f, 0, 2.75f), new(2.55f, 0, 2.75f), new(4.25f, 0, 2.35f),
        new(-4.25f, 0, 0.70f), new(-2.55f, 0, 0.70f), new(-0.85f, 0, 0.70f),
        new(0.85f, 0, 0.70f), new(2.55f, 0, 0.70f), new(4.25f, 0, 0.70f),
        new(-2.45f, 0, -1.75f), new(2.45f, 0, -1.75f), new(4.25f, 0, -1.75f)
    ];

    private const float PlayerRadius = 0.30f;
    private Node3D _model = null!;
    private Node3D _chalk = null!;
    private StatefulObjectVisual3D _blackboard = null!;
    private StatefulObjectVisual3D _teacherDesk = null!;
    private StatefulObjectVisual3D _mapCabinet = null!;
    private Node3D? _debugHost;

    public int CollisionBodyCount => CollisionSpecs.Length;
    public int FurnitureCollisionCount => CollisionSpecs.Count(spec => spec.Furniture);
    public Rect2 WalkableBounds => new(-4.52f, -3.30f, 9.04f, 6.60f);
    public bool ChalkVisible => _chalk.Visible;
    public string BlackboardVariant => _blackboard.CurrentVariant;
    public string TeacherDeskVariant => _teacherDesk.CurrentVariant;
    public string MapCabinetVariant => _mapCabinet.CurrentVariant;
    public Node3D DoorHinge { get; private set; } = null!;

    public override void _Ready()
    {
        _model = GetNode<Node3D>("../ClassroomModel");
        ApplyRuntimeTextureFiltering(_model);
        _chalk = RequiredNode("Chalk");
        _blackboard = new StatefulObjectVisual3D(new Dictionary<string, Node3D>(StringComparer.Ordinal)
        {
            ["blank"] = RequiredNode("Blackboard_Blank"),
            ["revealed"] = RequiredNode("Blackboard_Revealed")
        });
        _teacherDesk = new StatefulObjectVisual3D(new Dictionary<string, Node3D>(StringComparer.Ordinal)
        {
            ["closed"] = RequiredNode("TeacherDesk_Closed"),
            ["open"] = RequiredNode("TeacherDesk_Open")
        });
        _mapCabinet = new StatefulObjectVisual3D(new Dictionary<string, Node3D>(StringComparer.Ordinal)
        {
            ["closed"] = RequiredNode("MapCabinet_Closed"),
            ["open"] = RequiredNode("MapCabinet_Open")
        });
        DoorHinge = RequiredNode("Door_East_Hinge");
        BuildCollisionBodies();
    }

    public void ApplyState(DreamEscapeState state)
    {
        _chalk.Visible = !state.SolvedPuzzles.Contains("dream.pickup.chalk");
        _blackboard.SetVariant(state.SolvedPuzzles.Contains(DreamEscapeIds.PuzzleBlackboard) ? "revealed" : "blank");
        _teacherDesk.SetVariant(state.SolvedPuzzles.Contains(DreamEscapeIds.PuzzleTeacherDesk) ? "open" : "closed");
        _mapCabinet.SetVariant(state.SolvedPuzzles.Contains(DreamEscapeIds.PuzzleMapCabinet) ? "open" : "closed");
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
            .FirstOrDefault(new Vector3(0f, 0f, 2.75f));
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

        foreach (var child in node.GetChildren())
            ApplyRuntimeTextureFiltering(child);
    }

    private Node3D RequiredNode(string name) =>
        _model.FindChild(name, true, false) as Node3D
        ?? throw new InvalidOperationException($"Classroom GLB is missing state node '{name}'.");
}
