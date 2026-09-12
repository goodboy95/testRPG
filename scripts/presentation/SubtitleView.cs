using Godot;
using TownRpg.Core.Domain;

namespace TownRpg.Presentation;

/// <summary>Shared by live dialogue and the settings preview; no game state or input ownership.</summary>
public partial class SubtitleView : Control
{
    private Label _speaker = null!;
    private RichTextLabel _text = null!;
    private ColorRect _backdrop = null!;
    private ShaderMaterial _softShadow = null!;
    public int TextSize { get; private set; } = 9;
    public float LineWidth { get; private set; } = 288;
    public RichTextLabel TextControl => _text;

    public override void _Ready()
    {
        MouseFilter = MouseFilterEnum.Ignore;
        _softShadow = new ShaderMaterial { Shader = new Shader { Code = """
            shader_type canvas_item;
            void fragment() {
                vec2 p = abs(UV * 2.0 - 1.0);
                float a = (1.0 - smoothstep(0.62, 1.0, p.x)) * (1.0 - smoothstep(0.12, 1.0, p.y));
                COLOR = vec4(0.0, 0.0, 0.0, a * 0.62);
            }
            """ } };
        _backdrop = new ColorRect { MouseFilter = MouseFilterEnum.Ignore }; AddChild(_backdrop);
        _speaker = new Label { Name = "SubtitleSpeaker", HorizontalAlignment = HorizontalAlignment.Center,
            AutowrapMode = TextServer.AutowrapMode.WordSmart, MouseFilter = MouseFilterEnum.Ignore };
        _speaker.AddThemeFontOverride("font", UiStyles.CinematicHeadingFont);
        _speaker.AddThemeColorOverride("font_color", new Color("c4b5a4"));
        _speaker.AddThemeColorOverride("font_shadow_color", new Color(0, 0, 0, .55f));
        _speaker.AddThemeConstantOverride("outline_size", 0);
        _speaker.AddThemeConstantOverride("shadow_outline_size", 0);
        _speaker.AddThemeConstantOverride("shadow_offset_y", 1); AddChild(_speaker);
        _text = new RichTextLabel { Name = "SubtitleText", BbcodeEnabled = false, ScrollActive = false,
            HorizontalAlignment = HorizontalAlignment.Center, AutowrapMode = TextServer.AutowrapMode.Off,
            MouseFilter = MouseFilterEnum.Ignore };
        _text.AddThemeFontOverride("normal_font", UiStyles.CinematicFont);
        _text.AddThemeColorOverride("default_color", new Color("f1f0ec"));
        _text.AddThemeColorOverride("font_shadow_color", new Color(0, 0, 0, .6f));
        _text.AddThemeConstantOverride("outline_size", 0); _text.AddThemeConstantOverride("shadow_outline_size", 0);
        _text.AddThemeConstantOverride("shadow_offset_x", 0); _text.AddThemeConstantOverride("shadow_offset_y", 1);
        AddChild(_text);
    }

    public void Configure(float scale)
    {
        TextSize = Mathf.RoundToInt(UiStyles.RoleSize(UiTextRole.Subtitle) * scale);
        LineWidth = Math.Min(560, TextSize * 32);
    }

    public IReadOnlyList<string> Paginate(string text) => SubtitlePagination.Pages(text, LineWidth,
        line => UiStyles.CinematicFont.GetStringSize(line, fontSize: TextSize).X);

    public void ShowPage(string speaker, string text, float scale, SubtitleBackground background)
    {
        Configure(scale);
        var speakerSize = Mathf.RoundToInt(UiStyles.RoleSize(UiTextRole.Auxiliary) * scale);
        _speaker.AddThemeFontSizeOverride("font_size", speakerSize);
        _speaker.Text = speaker;
        var speakerHeight = string.IsNullOrEmpty(speaker) ? 0 : UiStyles.CinematicHeadingFont.GetMultilineStringSize(
            speaker, HorizontalAlignment.Center, LineWidth, speakerSize).Y;
        // Source Han already includes generous ascent/descent; extra spacing would nearly double the em.
        var spacing = 0;
        var height = UiStyles.CinematicFont.GetHeight(TextSize) * (1 + text.Count(c => c == '\n')) + spacing * text.Count(c => c == '\n');
        Size = new Vector2(LineWidth, speakerHeight + 3 + height);
        _speaker.Position = Vector2.Zero; _speaker.Size = new Vector2(LineWidth, speakerHeight);
        _text.Position = new Vector2(0, speakerHeight + 3); _text.Size = new Vector2(LineWidth, height + 1);
        _text.AddThemeFontSizeOverride("normal_font_size", TextSize);
        _text.AddThemeConstantOverride("line_separation", spacing);
        _text.Text = text;
        var measured = text.Split('\n').Append(speaker).Select(line => UiStyles.CinematicFont.GetStringSize(line, fontSize: TextSize).X).DefaultIfEmpty(0).Max();
        var shadeWidth = Math.Min(620, Math.Max(100, measured + 52));
        _backdrop.Position = new Vector2((LineWidth - shadeWidth) / 2, -12);
        _backdrop.Size = new Vector2(shadeWidth, Size.Y + 24);
        _backdrop.Visible = background != SubtitleBackground.None;
        _backdrop.Material = background == SubtitleBackground.SoftShadow ? _softShadow : null;
        _backdrop.Color = background == SubtitleBackground.HighContrast ? Colors.Black : Colors.White;
    }
}
