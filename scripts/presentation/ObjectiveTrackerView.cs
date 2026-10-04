using Godot;

namespace TownRpg.Presentation;

/// <summary>One destination projected into the current camera, independent of interaction eligibility.</summary>
public partial class ObjectiveTrackerView : Control
{
    private Label _summary = null!;
    private Label _pin = null!;
    private float _scale = 1;
    public string Summary => _summary.Text;
    public Vector2 PinPosition => _pin.Position;
    public bool Offscreen { get; private set; }
    public override void _Ready()
    {
        SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect); MouseFilter = MouseFilterEnum.Ignore;
        UiStyles.UseCinematic(this);
        _summary = UiStyles.Wrapped(""); _summary.Position = new(14, 76); _summary.Size = new(300, 52);
        _summary.MouseFilter = MouseFilterEnum.Ignore; UiStyles.Role(_summary, UiTextRole.Body); AddChild(_summary);
        _pin = new Label { HorizontalAlignment = HorizontalAlignment.Center, MouseFilter = MouseFilterEnum.Ignore };
        UiStyles.Role(_pin, UiTextRole.Auxiliary); AddChild(_pin);
        foreach (var label in new[] { _summary, _pin })
        { label.AddThemeColorOverride("font_shadow_color", Colors.Black); label.AddThemeConstantOverride("shadow_outline_size", 2); }
    }
    public void ShowTarget(Camera3D camera, Vector3 player, Vector3 target, string title, string action, string place, float scale)
    {
        Visible = true;
        if (!Mathf.IsEqualApprox(_scale, scale)) { UiStyles.ApplyScale(this, scale); _scale = scale; }
        _summary.Text = title + "\n" + action;
        var projected = camera.UnprojectPosition(target + Vector3.Up * 1.6f);
        var viewport = GetViewportRect().Size;
        // Canvas coordinates are logical even when the window renders 3D at native resolution.
        var rect = new Rect2(new Vector2(24, 30), viewport - new Vector2(48, 64));
        var behind = camera.IsPositionBehind(target);
        var direction = projected - viewport / 2;
        if (behind) direction = -direction;
        if (direction.LengthSquared() < .001f) direction = Vector2.Down;
        Offscreen = behind || !rect.HasPoint(projected);
        var glyph = "◇";
        if (Offscreen)
        {
            var half = rect.Size / 2;
            var factor = Math.Min(half.X / Math.Max(Math.Abs(direction.X), .001f), half.Y / Math.Max(Math.Abs(direction.Y), .001f));
            projected = rect.GetCenter() + direction * factor;
            var angle = Mathf.PosMod(Mathf.RoundToInt(direction.Angle() / (Mathf.Pi / 4)), 8);
            glyph = new[] { "→", "↘", "↓", "↙", "←", "↖", "↑", "↗" }[angle];
        }
        var distance = Mathf.RoundToInt(new Vector2(target.X - player.X, target.Z - player.Z).Length());
        _pin.Text = $"{glyph} {place} · {distance} 米";
        _pin.ResetSize(); var size = _pin.GetCombinedMinimumSize();
        _pin.Position = new(Mathf.Clamp(projected.X - size.X / 2, 12, Math.Max(12, viewport.X - size.X - 12)),
            Mathf.Clamp(projected.Y - size.Y / 2, 24, Math.Max(24, viewport.Y - size.Y - 20)));
    }
}
