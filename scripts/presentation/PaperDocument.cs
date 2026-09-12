using Godot;

namespace TownRpg.Presentation;

/// <summary>A document is readable text; texture is supplementary and never the only clue.</summary>
public partial class PaperDocument : PanelContainer
{
    public bool UsePaper { get; set; } = true;
    private VBoxContainer _body = null!;
    private Label? _source;
    public ScrollContainer Scroll { get; private set; } = null!;
    public override void _Ready()
    {
        if (UsePaper) UiStyles.PaperSurface(this);
        else AddThemeStyleboxOverride("panel", UiStyles.Panel(Colors.Transparent));
        var padding = new MarginContainer();
        foreach (var side in new[] { "left", "right", "top", "bottom" }) padding.AddThemeConstantOverride("margin_" + side, 6);
        AddChild(padding);
        Scroll = UiStyles.Scroll(padding);
        _body = new VBoxContainer { SizeFlagsHorizontal = SizeFlags.ExpandFill };
        _body.AddThemeConstantOverride("separation", 6); Scroll.AddChild(_body);
    }
    public void ShowDocument(string title, string text, string? source = null, string? imagePath = null)
    {
        UiStyles.Clear(_body);
        _source = null;
        var heading = UiStyles.Wrapped(title); UiStyles.Heading(heading, 16); _body.AddChild(heading);
        if (!string.IsNullOrWhiteSpace(source))
        {
            var label = UiStyles.Wrapped(source); UiStyles.Font(label, 11);
            label.AddThemeColorOverride("font_color", UsePaper ? UiStyles.PaperAccent : UiStyles.Muted); _body.AddChild(label); _source = label;
        }
        _body.AddChild(new HSeparator());
        if (imagePath is not null)
            _body.AddChild(new TextureRect { Texture = ResourceLoader.Load<Texture2D>(imagePath),
                CustomMinimumSize = new Vector2(0, 130), ExpandMode = TextureRect.ExpandModeEnum.IgnoreSize,
                StretchMode = TextureRect.StretchModeEnum.KeepAspectCentered });
        _body.AddChild(UiStyles.Wrapped(text)); Scroll.ScrollVertical = 0;
    }
    public override void _Process(double delta)
    {
        if (_source is not null) _source.Visible = Size.Y >= 190;
    }
}
