using Godot;
using TownRpg.Core.Domain;

namespace TownRpg.Presentation;

public abstract partial class DreamLateRoomVisual3D : Node3D
{
    protected sealed record CollisionSpec(string Name, Vector3 Position, Vector3 Size, bool Furniture);

    private const float PlayerRadius = 0.30f;
    private readonly Dictionary<string, StandardMaterial3D> _materials = new(StringComparer.Ordinal);
    private Node3D? _debugHost;

    protected Node3D Geometry { get; private set; } = null!;
    protected abstract IReadOnlyList<CollisionSpec> CollisionSpecs { get; }
    protected abstract IReadOnlyList<Vector3> SafeWaypoints { get; }
    public abstract Rect2 WalkableBounds { get; }
    public abstract Node3D DoorHinge { get; }

    public int CollisionBodyCount => CollisionSpecs.Count;
    public int FurnitureCollisionCount => CollisionSpecs.Count(spec => spec.Furniture);

    public override void _Ready()
    {
        BuildMaterials();
        Geometry = new Node3D { Name = "RuntimeStateGeometry" };
        AddChild(Geometry);
        BuildRoom();
        BuildCollisionBodies();
    }

    public abstract void ApplyState(DreamEscapeState state);

    public virtual void ApplyTransientState(DreamAreaTransientState state)
    {
        _ = state;
    }

    public virtual void ApplySettings(PlayerSettings settings)
    {
        if (_materials.TryGetValue("amber_glow", out var amber))
            amber.EmissionEnergyMultiplier = settings.HighContrastPrompts ? 4.5f : 2.8f;
        if (_materials.TryGetValue("green_glow", out var green))
            green.EmissionEnergyMultiplier = settings.HighContrastPrompts ? 4.2f : 2.5f;
    }

    public virtual bool IsBlocked(Vector3 position)
    {
        if (!WalkableBounds.HasPoint(new Vector2(position.X, position.Z))) return true;
        return CollisionSpecs.Where(spec => spec.Furniture || spec.Name.Contains("Barrier", StringComparison.Ordinal))
            .Any(spec =>
            {
                var rect = new Rect2(spec.Position.X - spec.Size.X / 2f,
                    spec.Position.Z - spec.Size.Z / 2f, spec.Size.X, spec.Size.Z).Grow(PlayerRadius);
                return rect.HasPoint(new Vector2(position.X, position.Z));
            });
    }

    public virtual Vector3 ResolveSpawn(Vector3 requested)
    {
        requested.Y = 0f;
        if (!IsBlocked(requested)) return requested;
        return SafeWaypoints.Where(point => !IsBlocked(point))
            .OrderBy(point => point.DistanceSquaredTo(requested))
            .FirstOrDefault(SafeWaypoints[0]);
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
            AddBox(_debugHost, spec.Name, spec.Position, spec.Size,
                spec.Furniture ? "debug_furniture" : "debug_structure");
    }

    protected abstract void BuildRoom();

    protected StandardMaterial3D Material(string name, Color color, float roughness, float metallic = 0f,
        bool transparent = false, Color? emission = null)
    {
        var material = new StandardMaterial3D
        {
            AlbedoColor = color,
            Roughness = roughness,
            Metallic = metallic,
            TextureFilter = BaseMaterial3D.TextureFilterEnum.LinearWithMipmapsAnisotropic
        };
        if (transparent)
        {
            material.Transparency = BaseMaterial3D.TransparencyEnum.Alpha;
            material.CullMode = BaseMaterial3D.CullModeEnum.Disabled;
        }
        if (emission is { } glow)
        {
            material.EmissionEnabled = true;
            material.Emission = glow;
            material.EmissionEnergyMultiplier = 2.8f;
        }
        _materials[name] = material;
        return material;
    }

    protected MeshInstance3D AddBox(Node parent, string name, Vector3 position, Vector3 size,
        string material, Vector3? rotationDegrees = null)
    {
        var mesh = new MeshInstance3D
        {
            Name = name,
            Position = position,
            RotationDegrees = rotationDegrees ?? Vector3.Zero,
            Mesh = new BoxMesh { Size = size },
            MaterialOverride = _materials[material]
        };
        parent.AddChild(mesh);
        return mesh;
    }

    protected MeshInstance3D AddCylinder(Node parent, string name, Vector3 position, float radius, float height,
        string material, Vector3? rotationDegrees = null)
    {
        var mesh = new MeshInstance3D
        {
            Name = name,
            Position = position,
            RotationDegrees = rotationDegrees ?? Vector3.Zero,
            Mesh = new CylinderMesh
            {
                TopRadius = radius,
                BottomRadius = radius,
                Height = height,
                RadialSegments = 24
            },
            MaterialOverride = _materials[material]
        };
        parent.AddChild(mesh);
        return mesh;
    }

    protected Label3D AddLabel(string name, string text, Vector3 position, Vector3 rotationDegrees,
        int fontSize = 32, Color? color = null)
    {
        var label = new Label3D
        {
            Name = name,
            Text = text,
            Position = position,
            RotationDegrees = rotationDegrees,
            FontSize = fontSize,
            PixelSize = 0.0025f,
            Modulate = color ?? new Color("e8d9b4"),
            OutlineSize = 5,
            OutlineModulate = new Color("16232b")
        };
        Geometry.AddChild(label);
        return label;
    }

    private void BuildMaterials()
    {
        Material("plaster", new Color("a8afa9"), 0.92f);
        Material("ceiling", new Color("7c8788"), 0.96f);
        Material("floor", new Color("665b50"), 0.78f);
        Material("oak", new Color("5f4435"), 0.70f);
        Material("oak_light", new Color("8a6650"), 0.64f);
        Material("teal", new Color("526f70"), 0.74f);
        Material("curtain", new Color("532f37"), 0.88f);
        Material("metal", new Color("4e595b"), 0.38f, 0.78f);
        Material("brass", new Color("a87934"), 0.32f, 0.82f);
        Material("paper", new Color("c9b98f"), 0.90f);
        Material("glass", new Color(0.35f, 0.50f, 0.52f, 0.25f), 0.18f, 0.10f, true);
        Material("amber_glow", new Color("ffba55"), 0.28f, 0.35f, false, new Color("ff9d32"));
        Material("green_glow", new Color("8dd6bb"), 0.30f, 0.25f, false, new Color("52c7a5"));
        Material("red_glow", new Color("d65c54"), 0.30f, 0.20f, false, new Color("d43b32"));
        Material("debug_furniture", new Color("ffad3b55"), 0f, 0f, true);
        Material("debug_structure", new Color("ff4c4c3c"), 0f, 0f, true);
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
}
