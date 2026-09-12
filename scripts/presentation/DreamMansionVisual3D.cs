using Godot;
using TownRpg.Core.Domain;

namespace TownRpg.Presentation;

public partial class DreamMansionVisual3D : Node3D
{
    private static readonly MansionCollisionSpec[] CollisionSpecs = MansionLayout3D.Collisions;
    private readonly GroundWaypointNavigator3D _companionNavigator = new(
        MansionLayout3D.SafeWaypoints, MansionLayout3D.IsBlocked, MansionLayout3D.ResolveSpawn);

    private static readonly Dictionary<string, float[]> DoorOpenAngles = new(StringComparer.Ordinal)
    {
        [$"area:{DreamEscapeIds.AreaClassroom}"] = [-72f],
        [$"area:{DreamEscapeIds.AreaMusic}"] = [72f],
        [$"area:{DreamEscapeIds.AreaArchive}"] = [-72f],
        [$"area:{DreamEscapeIds.AreaAuditorium}"] = [-62f, 62f]
    };

    private readonly Dictionary<string, Node3D[]> _doorHinges = new(StringComparer.Ordinal);
    private readonly Dictionary<Node3D, float> _closedDoorYaw = [];
    private Node3D _model = null!;
    private Node3D? _debugHost;

    public int CollisionBodyCount => CollisionSpecs.Length;
    public int FurnitureCollisionCount => CollisionSpecs.Count(spec => spec.Furniture);
    public int DoorBarrierCount => CollisionSpecs.Count(spec => spec.Door);
    public int DoorHingeCount => _closedDoorYaw.Count;
    public Rect2 WalkableBounds => new Rect2(-MansionLayout3D.FoyerWidth / 2,
        -MansionLayout3D.FoyerDepth, MansionLayout3D.FoyerWidth,
        MansionLayout3D.FoyerDepth + MansionLayout3D.HallDepth).Grow(-MansionLayout3D.ClearanceRadius);
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
        BuildPracticalLights();
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

    public bool IsBlocked(Vector3 playerPosition) => MansionLayout3D.IsBlocked(playerPosition);

    public Vector3 ResolveSpawn(Vector3 requestedPosition) => MansionLayout3D.ResolveSpawn(requestedPosition);

    public Vector3 AdvanceCompanion(Vector3 current, Vector3 desired, float distance) =>
        _companionNavigator.Advance(current, desired, distance);

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

    private void BuildPracticalLights()
    {
        var host = new Node3D { Name = "PracticalLights" };
        AddChild(host);
        Vector3[] positions =
        [
            new(-2.45f, 2.5f, -7.52f), new(2.45f, 2.5f, -7.52f),
            new(-4.5f, 2.8f, -1.3f), new(4.5f, 3.5f, -3.3f),
            new(-3f, 2.8f, 5.5f), new(3f, 2.8f, 5.4f),
            new(-3f, 2.8f, 10.5f), new(3f, 2.8f, 10.4f)
        ];
        foreach (var position in positions)
            host.AddChild(new OmniLight3D
            {
                Position = position, LightColor = new Color(1f, 0.78f, 0.49f),
                LightEnergy = 0.65f, OmniRange = 4.3f, ShadowEnabled = false
            });
        foreach (var x in new[] { -1.695f, 1.695f })
            host.AddChild(new SpotLight3D
            {
                Position = new Vector3(x, 2.35f, -8.22f), RotationDegrees = new Vector3(-15, 180, 0),
                LightColor = new Color(1f, 0.86f, 0.66f), LightEnergy = 0.3f,
                SpotRange = 10f, SpotAngle = 49f, ShadowEnabled = false
            });
        foreach (var (position, range, energy) in new[]
        {
            (new Vector3(0, 4.57f, -3.8f), 9f, 1.1f),
            (new Vector3(0, 3.12f, 6f), 7f, 0.85f)
        })
            host.AddChild(new SpotLight3D
            {
                Position = position, RotationDegrees = new Vector3(-90, 0, 0),
                LightColor = new Color(1f, 0.84f, 0.64f), LightEnergy = energy,
                SpotRange = range, SpotAngle = 68f, ShadowEnabled = true
            });
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
            if (mesh.Name == "V3_FrontSidelightGlass")
                mesh.CastShadow = GeometryInstance3D.ShadowCastingSetting.Off;
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
