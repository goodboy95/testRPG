using Godot;

namespace TownRpg.Presentation;

public partial class NpcActor : Node2D
{
    public string NpcId { get; private set; } = string.Empty;
    public string DisplayName { get; private set; } = string.Empty;
    private Color _bodyColor = Colors.CornflowerBlue;
    private Label? _label;

    public void Configure(string npcId, string displayName, Color bodyColor)
    {
        NpcId = npcId;
        DisplayName = displayName;
        _bodyColor = bodyColor;
        _label ??= BuildLabel();
        _label.Text = displayName;
        QueueRedraw();
    }

    public override void _Draw()
    {
        DrawCircle(new Vector2(0, -10), 5, new Color("ebd2ba"));
        DrawRect(new Rect2(-7, -4, 14, 15), _bodyColor);
        DrawRect(new Rect2(-6, 11, 5, 4), new Color("283247"));
        DrawRect(new Rect2(1, 11, 5, 4), new Color("283247"));
    }

    private Label BuildLabel()
    {
        var label = new Label
        {
            Position = new Vector2(-32, -34),
            Size = new Vector2(64, 16),
            HorizontalAlignment = HorizontalAlignment.Center,
            MouseFilter = Control.MouseFilterEnum.Ignore
        };
        UiStyles.Font(label, 10);
        label.AddThemeFontOverride("font", UiStyles.CreateFont());
        label.AddThemeColorOverride("font_color", Colors.White);
        label.AddThemeColorOverride("font_shadow_color", new Color(0, 0, 0, 0.9f));
        label.AddThemeConstantOverride("shadow_offset_x", 1);
        label.AddThemeConstantOverride("shadow_offset_y", 1);
        AddChild(label);
        return label;
    }
}
