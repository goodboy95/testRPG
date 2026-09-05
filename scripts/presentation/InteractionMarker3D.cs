using Godot;

namespace TownRpg.Presentation;

public partial class InteractionMarker3D : Marker3D
{
    [Export] public string TargetId { get; set; } = string.Empty;
    [Export] public string Prompt { get; set; } = string.Empty;
    [Export] public float InteractionRange { get; set; } = 1.25f;

    private MeshInstance3D _outer = null!;
    private MeshInstance3D _inner = null!;

    public override void _Ready()
    {
        _outer = CreateDisc("FocusOuter", 0.25f, Colors.Black, 0.012f);
        _inner = CreateDisc("FocusInner", 0.18f, new Color("efb95c"), 0.02f);
        AddChild(_outer);
        AddChild(_inner);
        SetFocused(false, false);
    }

    public void SetFocused(bool focused, bool highContrast)
    {
        if (_outer is null || _inner is null) return;
        _outer.Visible = focused && highContrast;
        _inner.Visible = focused;
        if (_inner.MaterialOverride is StandardMaterial3D material)
            material.AlbedoColor = highContrast ? Colors.White : new Color("efb95c");
    }

    private static MeshInstance3D CreateDisc(string name, float radius, Color color, float y)
    {
        var material = new StandardMaterial3D
        {
            AlbedoColor = color,
            ShadingMode = BaseMaterial3D.ShadingModeEnum.Unshaded,
            Transparency = BaseMaterial3D.TransparencyEnum.Alpha
        };
        return new MeshInstance3D
        {
            Name = name,
            Position = new Vector3(0f, y, 0f),
            Mesh = new CylinderMesh
            {
                TopRadius = radius,
                BottomRadius = radius,
                Height = 0.012f,
                RadialSegments = 32
            },
            MaterialOverride = material,
            CastShadow = GeometryInstance3D.ShadowCastingSetting.Off
        };
    }
}
