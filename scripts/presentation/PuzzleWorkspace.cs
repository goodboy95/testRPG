using Godot;

namespace TownRpg.Presentation;

/// <summary>One mechanism workspace with a fixed heading and action bar.</summary>
public partial class PuzzleWorkspace : Control
{
    public VBoxContainer Work { get; private set; } = null!;
    public HBoxContainer Footer { get; private set; } = null!;
    public Label Heading { get; private set; } = null!;
    public override void _Ready()
    {
        SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect);
        var shade = new ColorRect { Color = new Color("101514c9") }; shade.SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect); AddChild(shade);
        var frame = new PanelContainer { Name = "WorkspaceFrame", Position = new Vector2(18, 14), Size = new Vector2(604, 332) }; AddChild(frame);
        var column = new VBoxContainer(); column.AddThemeConstantOverride("separation", 8); frame.AddChild(column);
        Heading = UiStyles.Wrapped(""); UiStyles.Heading(Heading, 20); column.AddChild(Heading);
        column.AddChild(new HSeparator());
        Work = new VBoxContainer { SizeFlagsHorizontal = SizeFlags.ExpandFill, SizeFlagsVertical = SizeFlags.ExpandFill };
        Work.AddThemeConstantOverride("separation", 6); column.AddChild(Work);
        Footer = new HBoxContainer(); Footer.AddThemeConstantOverride("separation", 4); column.AddChild(Footer);
    }
}
