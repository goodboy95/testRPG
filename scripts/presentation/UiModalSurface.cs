using Godot;

namespace TownRpg.Presentation;

/// A bounded frame: only its body scrolls; title and actions stay reachable.
public partial class UiModalSurface : PanelContainer
{
    public Vector2 BaseSize { get; set; } = new(392, 204);
    public bool ScaleHeight { get; set; } = true;
    public VBoxContainer Body { get; private set; } = null!;
    public HBoxContainer Footer { get; private set; } = null!;
    public Label Heading { get; private set; } = null!;
    public ScrollContainer Scroll { get; private set; } = null!;
    public override void _Ready()
    {
        AddThemeStyleboxOverride("panel", UiStyles.Panel());
        var column = new VBoxContainer();
        column.AddThemeConstantOverride("separation", 8);
        AddChild(column);
        Heading = UiStyles.Wrapped(""); UiStyles.Heading(Heading, 20);
        column.AddChild(Heading);
        Scroll = UiStyles.Scroll(column);
        Body = new VBoxContainer { SizeFlagsHorizontal = SizeFlags.ExpandFill };
        Body.AddThemeConstantOverride("separation", 6);
        Scroll.AddChild(Body);
        Footer = new HBoxContainer();
        Footer.AddThemeConstantOverride("separation", 6);
        column.AddChild(Footer);
        Layout();
    }
    public override void _Process(double delta) => Layout();
    private void Layout()
    {
        var height = Mathf.Min(332, BaseSize.Y * (ScaleHeight ? UiStyles.Scale(this) : 1));
        var width = BaseSize.X + (UiStyles.ActiveTypography is { } p ? (p.Body - 9) * 16 : 0);
        Size = new Vector2(width, height);
        Position = (new Vector2(640, 360) - Size) / 2;
    }
}
