using Godot;
using TownRpg.Core.Domain;

namespace TownRpg.Presentation;

public partial class DreamMansionVisual3D : Node3D
{
    private sealed record CollisionSpec(string Name, Vector3 Position, Vector3 Size, bool Furniture, bool Door);

    private static readonly CollisionSpec[] CollisionSpecs =
    [
        new("Floor", new Vector3(0f, -0.10f, 5.6f), new Vector3(7.6f, 0.20f, 24.8f), false, false),
        new("Ceiling", new Vector3(0f, 4.30f, 5.6f), new Vector3(7.6f, 0.20f, 24.8f), false, false),
        new("WallWest", new Vector3(-3.50f, 2.10f, 5.6f), new Vector3(0.20f, 4.20f, 24.8f), false, false),
        new("WallEast", new Vector3(3.50f, 2.10f, 5.6f), new Vector3(0.20f, 4.20f, 24.8f), false, false),
        new("WallSouth", new Vector3(0f, 2.10f, -6.70f), new Vector3(7.0f, 4.20f, 0.20f), false, false),
        new("WallNorth", new Vector3(0f, 2.10f, 17.90f), new Vector3(7.0f, 4.20f, 0.20f), false, false),
        new("DoorBarrierFront", new Vector3(0f, 1.10f, -6.54f), new Vector3(2.50f, 2.20f, 0.24f), false, true),
        new("DoorBarrierRoom1", new Vector3(-3.34f, 1.10f, 3.2f), new Vector3(0.24f, 2.20f, 1.35f), false, true),
        new("DoorBarrierRoom2", new Vector3(3.34f, 1.10f, 3.2f), new Vector3(0.24f, 2.20f, 1.35f), false, true),
        new("DoorBarrierRoom3", new Vector3(-3.34f, 1.10f, 13.2f), new Vector3(0.24f, 2.20f, 1.35f), false, true),
        new("DoorBarrierRoom4", new Vector3(0f, 1.10f, 17.74f), new Vector3(2.65f, 2.20f, 0.24f), false, true),
        new("Fireplace", new Vector3(-3.31f, 1.03f, -3.45f), new Vector3(0.86f, 2.06f, 2.06f), true, false),
        new("ConsoleTable", new Vector3(3.06f, 0.75f, -1.72f), new Vector3(0.66f, 1.50f, 1.80f), true, false),
        new("FoyerBench", new Vector3(3.00f, 0.75f, 6.80f), new Vector3(0.70f, 1.50f, 1.72f), true, false)
    ];

    private static readonly Vector3[] SafeWaypoints =
    [
        DreamAreaSpawnCatalog.MansionRoomOneEntryWorld,
        DreamAreaSpawnCatalog.MansionRoomTwoEntryWorld,
        DreamAreaSpawnCatalog.MansionRoomThreeEntryWorld,
        DreamAreaSpawnCatalog.MansionRoomFourEntryWorld,
        new(0f, 0f, -5.7f), new(-1.8f, 0f, -4.2f), new(0f, 0f, -2.5f),
        new(0f, 0f, 0.8f), new(0f, 0f, 4.8f), new(0f, 0f, 8.8f),
        new(0f, 0f, 12.8f), new(0f, 0f, 15.8f)
    ];

    private static readonly Dictionary<string, float[]> DoorOpenAngles = new(StringComparer.Ordinal)
    {
        [$"area:{DreamEscapeIds.AreaClassroom}"] = [-72f],
        [$"area:{DreamEscapeIds.AreaMusic}"] = [72f],
        [$"area:{DreamEscapeIds.AreaArchive}"] = [-72f],
        [$"area:{DreamEscapeIds.AreaAuditorium}"] = [-62f, 62f]
    };

    private const float PlayerRadius = 0.30f;
    private readonly Dictionary<string, Node3D[]> _doorHinges = new(StringComparer.Ordinal);
    private readonly Dictionary<Node3D, float> _closedDoorYaw = [];
    private Node3D _model = null!;
    private Node3D? _debugHost;

    public int CollisionBodyCount => CollisionSpecs.Length;
    public int FurnitureCollisionCount => CollisionSpecs.Count(spec => spec.Furniture);
    public int DoorBarrierCount => CollisionSpecs.Count(spec => spec.Door);
    public int DoorHingeCount => _closedDoorYaw.Count;
    public Rect2 WalkableBounds => new(-3.08f, -6.32f, 6.16f, 23.80f);
    public Node3D ModelRoot => _model;

    public override void _Ready()
    {
        _model = GetNode<Node3D>("../ManorModel");
        ApplyRuntimeTextureFiltering(_model);
        RegisterDoor($"area:{DreamEscapeIds.AreaClassroom}", "Door_Room1_Hinge");
        RegisterDoor($"area:{DreamEscapeIds.AreaMusic}", "Door_Room2_Hinge");
        RegisterDoor($"area:{DreamEscapeIds.AreaArchive}", "Door_Room3_Hinge");
        RegisterDoor(
            $"area:{DreamEscapeIds.AreaAuditorium}",
            "Door_Room4_Hinge_L",
            "Door_Room4_Hinge_R");
        RegisterDoor("locked:mansion_front_door", "Door_Front_Hinge_L", "Door_Front_Hinge_R");
        BuildCollisionBodies();
    }

    public void ApplyState(DreamEscapeState state) => _ = state;

    public void ApplySettings(PlayerSettings settings) => _ = settings;

    public void SetDoorOpenProgress(string targetId, float progress)
    {
        if (!_doorHinges.TryGetValue(targetId, out var hinges) ||
            !DoorOpenAngles.TryGetValue(targetId, out var openAngles)) return;
        progress = Mathf.Clamp(progress, 0f, 1f);
        for (var index = 0; index < hinges.Length; index++)
        {
            var hinge = hinges[index];
            var closed = _closedDoorYaw[hinge];
            hinge.Rotation = new Vector3(
                hinge.Rotation.X,
                Mathf.LerpAngle(closed, closed + Mathf.DegToRad(openAngles[index]), progress),
                hinge.Rotation.Z);
        }
    }

    public void ResetDoors()
    {
        foreach (var (hinge, closedYaw) in _closedDoorYaw)
        {
            hinge.Rotation = new Vector3(hinge.Rotation.X, closedYaw, hinge.Rotation.Z);
        }
    }

    public bool IsBlocked(Vector3 playerPosition)
    {
        if (!WalkableBounds.HasPoint(new Vector2(playerPosition.X, playerPosition.Z))) return true;
        return CollisionSpecs.Where(spec => spec.Furniture || spec.Door).Any(spec =>
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
        var clamped = new Vector3(
            Mathf.Clamp(requestedPosition.X, WalkableBounds.Position.X, WalkableBounds.End.X),
            0f,
            Mathf.Clamp(requestedPosition.Z, WalkableBounds.Position.Y, WalkableBounds.End.Y));
        if (!IsBlocked(clamped)) return clamped;
        return SafeWaypoints
            .Where(point => !IsBlocked(point))
            .OrderBy(point => point.DistanceSquaredTo(clamped))
            .FirstOrDefault(new Vector3(0f, 0f, -3.7f));
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
            var color = spec.Door
                ? new Color("ff4c4c5f")
                : spec.Furniture ? new Color("ffad3b55") : new Color("48a8ff3c");
            var material = new StandardMaterial3D
            {
                AlbedoColor = color,
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

    private void RegisterDoor(string targetId, params string[] hingeNames)
    {
        var hinges = hingeNames.Select(RequiredNode).ToArray();
        _doorHinges[targetId] = hinges;
        foreach (var hinge in hinges) _closedDoorYaw[hinge] = hinge.Rotation.Y;
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

    private Node3D RequiredNode(string name) =>
        _model.FindChild(name, true, false) as Node3D
        ?? throw new InvalidOperationException($"Dream manor GLB is missing required node '{name}'.");
}
