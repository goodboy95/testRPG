using System.Runtime.CompilerServices;
using Godot;
using TownRpg.Application;

namespace TownRpg.Presentation;

public enum UiTextRole { Body, Subtitle, Auxiliary, Heading }

/// <summary>Shared game UI tokens, in 640×360 logical pixels. Paper is a local reading surface.</summary>
public static class UiStyles
{
    public const int BodySize = 14;
    public const int CinematicBodySize = 8;
    public static int RoleSize(UiTextRole role) => role switch
    {
        UiTextRole.Subtitle => 9, UiTextRole.Auxiliary => 7, UiTextRole.Heading => 12, _ => CinematicBodySize
    };
    private static Font? _cinematicFont;
    private static Font? _cinematicHeadingFont;
    public static Font CinematicFont => _cinematicFont ??= ResourceLoader.Load<Font>("res://assets/fonts/source-han-sans/SourceHanSansSC-Regular.otf");
    public static Font CinematicHeadingFont => _cinematicHeadingFont ??= ResourceLoader.Load<Font>("res://assets/fonts/source-han-sans/SourceHanSansSC-Medium.otf");
    public static readonly Color Background = new("171c1e");
    public static readonly Color Surface = new("242b2d");
    public static readonly Color Paper = new("e9e3d6");
    public static readonly Color PaperInk = new("272a28");
    public static readonly Color PaperAccent = new("754d32");
    public static readonly Color Ink = new("f2ede3");
    public static readonly Color Accent = new("c99770");
    public static readonly Color Muted = new("acb2ad");
    public static readonly Color Rule = new("424a49");
    private sealed class Motion { public Tween? Tween; }
    private static readonly ConditionalWeakTable<Control, Motion> Motions = new();

    public static Theme CreateTheme(float scale = 1, bool paper = false, bool cinematic = false)
    {
        var text = paper ? PaperInk : Ink;
        var accent = paper ? PaperAccent : Accent;
        var theme = new Theme { DefaultFont = cinematic ? CinematicFont : CreateFont(), DefaultFontSize = Mathf.RoundToInt((cinematic ? CinematicBodySize : BodySize) * scale) };
        theme.SetConstant("cinematic", "TownRpg", cinematic ? 1 : 0);
        foreach (var type in new[] { "Button", "OptionButton", "CheckButton", "CheckBox" })
        {
            theme.SetStylebox("normal", type, Panel(Colors.Transparent));
            theme.SetStylebox("hover", type, Panel(paper ? new Color("d9d0c1") : Surface));
            theme.SetStylebox("pressed", type, Selected(paper));
            theme.SetStylebox("hover_pressed", type, Selected(paper));
            theme.SetStylebox("disabled", type, Panel(Colors.Transparent));
            var focus = Panel(Colors.Transparent);
            if (cinematic) focus.BorderWidthBottom = 1;
            else focus.SetBorderWidthAll(1);
            focus.BorderColor = text;
            theme.SetStylebox("focus", type, focus);
            foreach (var property in new[] { "font_color", "font_hover_color", "font_focus_color", "font_pressed_color", "font_hover_pressed_color" })
                theme.SetColor(property, type, text);
            theme.SetColor("font_disabled_color", type, new Color(text, .42f));
            if (cinematic)
                foreach (var name in theme.GetStyleboxList(type))
                {
                    var box = theme.GetStylebox(name, type);
                    box.ContentMarginTop = 3; box.ContentMarginBottom = 3;
                    box.ContentMarginLeft = 8; box.ContentMarginRight = 8;
                    if (box is StyleBoxFlat flat && flat.BorderWidthBottom > 1) flat.BorderWidthBottom = 1;
                }
        }
        theme.SetColor("font_color", "Label", text);
        theme.SetColor("default_color", "RichTextLabel", text);
        theme.SetConstant("line_spacing", "Label", cinematic ? 3 : 4);
        theme.SetStylebox("panel", "PanelContainer", Panel(paper ? Paper : Background));
        theme.SetStylebox("panel", "PopupMenu", Panel(paper ? Paper : Background));
        theme.SetStylebox("panel", "AcceptDialog", Panel(paper ? Paper : Background));
        var window = Panel(paper ? Paper : Background); window.ContentMarginTop = 26;
        window.ExpandMarginTop = 26;
        window.SetBorderWidthAll(1); window.BorderColor = paper ? new Color("ccc3b3") : Rule;
        theme.SetStylebox("embedded_border", "Window", window);
        theme.SetStylebox("embedded_unfocused_border", "Window", window);
        theme.SetColor("title_color", "Window", text); theme.SetColor("title_outline_modulate", "Window", Colors.Transparent);
        theme.SetStylebox("hover", "PopupMenu", Selected(paper));
        theme.SetColor("font_color", "PopupMenu", text);
        theme.SetColor("font_hover_color", "PopupMenu", text);
        theme.SetStylebox("normal", "LineEdit", Panel(paper ? new Color("d9d0c1") : Surface));
        var inputFocus = Panel(Colors.Transparent); inputFocus.BorderWidthBottom = 2; inputFocus.BorderColor = accent;
        theme.SetStylebox("focus", "LineEdit", inputFocus);
        theme.SetColor("font_color", "LineEdit", text);
        theme.SetColor("caret_color", "LineEdit", accent);
        var rail = new StyleBoxFlat { BgColor = paper ? new Color("ccc3b3") : Rule, ContentMarginLeft = cinematic ? 1 : 2, ContentMarginRight = cinematic ? 1 : 2 };
        var grip = new StyleBoxFlat { BgColor = accent, CornerRadiusTopLeft = 2, CornerRadiusTopRight = 2,
            CornerRadiusBottomLeft = 2, CornerRadiusBottomRight = 2 };
        foreach (var type in new[] { "VScrollBar", "HScrollBar" })
        {
            theme.SetStylebox("scroll", type, rail);
            foreach (var name in new[] { "grabber", "grabber_highlight", "grabber_pressed" }) theme.SetStylebox(name, type, grip);
        }
        theme.SetStylebox("separator", "HSeparator", new StyleBoxLine { Color = paper ? new Color("ccc3b3") : Rule, Thickness = 1 });
        return theme;
    }
    public static SystemFont CreateFont() => new() { FontNames = ["Microsoft YaHei UI", "Microsoft YaHei", "Noto Sans SC"], AllowSystemFallback = true };
    public static SystemFont CreateHeadingFont() => new() { FontNames = ["Noto Serif SC", "SimSun", "Microsoft YaHei"], AllowSystemFallback = true };
    public static StyleBoxFlat Panel(Color? color = null, int radius = 0) => new()
    {
        BgColor = color ?? Background,
        CornerRadiusTopLeft = Math.Min(radius, 2), CornerRadiusTopRight = Math.Min(radius, 2),
        CornerRadiusBottomLeft = Math.Min(radius, 2), CornerRadiusBottomRight = Math.Min(radius, 2),
        ContentMarginLeft = 10, ContentMarginRight = 10, ContentMarginTop = 5, ContentMarginBottom = 5
    };
    public static StyleBoxFlat Selected(bool paper = false)
    {
        var style = Panel(paper ? new Color("d9d0c1") : Surface);
        style.BorderWidthBottom = 2; style.BorderColor = paper ? PaperAccent : Accent;
        return style;
    }
    public static Button Button(string text)
    {
        var button = new Button { Text = text, FocusMode = Control.FocusModeEnum.All,
            CustomMinimumSize = new Vector2(0, 28), MouseDefaultCursorShape = Control.CursorShape.PointingHand };
        button.TreeEntered += () =>
        {
            if (IsCinematic(button) && button.CustomMinimumSize.Y == 28)
                button.CustomMinimumSize = new Vector2(button.CustomMinimumSize.X, 20);
        };
        return button;
    }
    public static void Primary(Button button)
    {
        button.AddThemeStyleboxOverride("normal", Panel(Accent));
        button.AddThemeStyleboxOverride("hover", Panel(new Color("dfb48f")));
        button.AddThemeStyleboxOverride("pressed", Panel(new Color("b38765")));
        foreach (var name in new[] { "font_color", "font_hover_color", "font_pressed_color", "font_focus_color" }) button.AddThemeColorOverride(name, Background);
    }
    public static void Heading(Control control, int size = 20)
    {
        Font(control, size); control.SetMeta("ui_heading", true);
        void Apply() => control.AddThemeFontOverride("font", IsCinematic(control) ? CinematicHeadingFont : CreateHeadingFont());
        if (control.IsInsideTree()) Apply(); else control.TreeEntered += Apply;
    }
    public static TextureRect GradientShade(bool horizontal = false)
    {
        var gradient = new Gradient { Colors = [new Color(Background, .96f), new Color(Background, horizontal ? .05f : 0f)] };
        var shade = new TextureRect { Texture = new GradientTexture2D { Gradient = gradient, Width = 64, Height = 64,
            FillFrom = horizontal ? Vector2.Zero : new Vector2(0, 1), FillTo = horizontal ? new Vector2(1, 0) : Vector2.Zero },
            ExpandMode = TextureRect.ExpandModeEnum.IgnoreSize, MouseFilter = Control.MouseFilterEnum.Ignore };
        shade.SetAnchorsAndOffsetsPreset(Control.LayoutPreset.FullRect); return shade;
    }
    public static Label Wrapped(string text) => new() { Text = text, AutowrapMode = TextServer.AutowrapMode.WordSmart,
        SizeFlagsHorizontal = Control.SizeFlags.ExpandFill };
    public static void Font(Control control, int size)
    {
        size = Math.Max(11, size);
        control.SetMeta("ui_font_size", size);
        void Apply() => control.AddThemeFontSizeOverride("font_size", Mathf.RoundToInt(AdaptSize(control, size) * Scale(control)));
        if (control.IsInsideTree()) Apply(); else control.TreeEntered += Apply;
    }
    public static bool IsCinematic(Control control) => control.GetThemeConstant("cinematic", "TownRpg") == 1;
    public static float Scale(Control control) => control.IsInsideTree() ? control.GetThemeDefaultFontSize() / (float)(IsCinematic(control) ? CinematicBodySize : BodySize) : 1;
    // Existing shared readers carry legacy sizes; translate only within the opt-in sample theme.
    private static int AdaptSize(Control control, int size) => !IsCinematic(control) ? size : size switch
    {
        <= 12 => RoleSize(UiTextRole.Auxiliary), <= 15 => RoleSize(UiTextRole.Body),
        <= 17 => 10, _ => RoleSize(UiTextRole.Heading)
    };
    public static void Role(Control control, UiTextRole role)
    {
        control.SetMeta("ui_text_role", (int)role);
        void Apply() => control.AddThemeFontSizeOverride("font_size", Mathf.RoundToInt(RoleSize(role) * Scale(control)));
        if (control.IsInsideTree()) Apply(); else control.TreeEntered += Apply;
    }
    public static void UseCinematic(Control root, float scale = 1)
    {
        root.SetMeta("ui_cinematic", true);
        ApplyScale(root, scale);
    }
    public static void ApplyScale(Control root, float scale)
    {
        root.Theme = CreateTheme(scale, root.HasMeta("ui_paper"), root.HasMeta("ui_cinematic"));
        ApplyFonts(root, scale);
    }
    private static void ApplyFonts(Node node, float scale)
    {
        if (node is Control c)
        {
            if (c.HasMeta("ui_cinematic")) c.Theme = CreateTheme(scale, c.HasMeta("ui_paper"), true);
            else if (c.HasMeta("ui_paper")) c.Theme = CreateTheme(scale, true, c.GetParent() is Control parent && IsCinematic(parent));
            if (c.HasMeta("ui_font_size")) c.AddThemeFontSizeOverride("font_size", Mathf.RoundToInt(AdaptSize(c, (int)c.GetMeta("ui_font_size")) * scale));
            if (c.HasMeta("ui_text_role")) c.AddThemeFontSizeOverride("font_size", Mathf.RoundToInt(RoleSize((UiTextRole)(int)c.GetMeta("ui_text_role")) * scale));
            if (c.HasMeta("ui_heading")) c.AddThemeFontOverride("font", IsCinematic(c) ? CinematicHeadingFont : CreateHeadingFont());
        }
        foreach (var child in node.GetChildren()) ApplyFonts(child, scale);
    }
    public static void PaperSurface(Control control)
    {
        control.SetMeta("ui_paper", true);
        void Apply() => control.Theme = CreateTheme(Scale(control.GetParent<Control>()), true, IsCinematic(control.GetParent<Control>()));
        if (control.IsInsideTree()) Apply(); else control.TreeEntered += Apply;
    }
    public static void Clear(Node parent)
    {
        foreach (var child in parent.GetChildren()) { parent.RemoveChild(child); child.QueueFree(); }
    }
    public static void ReleaseFocus(Control root)
    {
        var focus = root.GetViewport().GuiGetFocusOwner();
        if (focus != null && (focus == root || root.IsAncestorOf(focus))) focus.ReleaseFocus();
    }
    public static bool ReducedMotion(Control root) => root.GetNodeOrNull<GameSession>("/root/GameSession")?.State.Settings.ReduceFlashing ?? true;
    public static void FadeIn(Control root)
    {
        var motion = Motions.GetOrCreateValue(root); motion.Tween?.Kill();
        root.Modulate = Colors.White;
        if (ReducedMotion(root)) return;
        root.Modulate = new Color(1, 1, 1, 0);
        motion.Tween = root.CreateTween(); motion.Tween.TweenProperty(root, "modulate:a", 1f, .16);
    }
    public static void SelectionFeedback(Control root)
    {
        var motion = Motions.GetOrCreateValue(root); motion.Tween?.Kill(); root.Modulate = Colors.White;
        if (ReducedMotion(root)) return;
        root.Modulate = new Color(.82f, .84f, .82f, 1);
        motion.Tween = root.CreateTween(); motion.Tween.TweenProperty(root, "modulate", Colors.White, .14);
    }
    public static ScrollContainer Scroll(Node parent)
    {
        var scroll = new ScrollContainer { SizeFlagsHorizontal = Control.SizeFlags.ExpandFill,
            SizeFlagsVertical = Control.SizeFlags.ExpandFill, HorizontalScrollMode = ScrollContainer.ScrollMode.Disabled, FollowFocus = true,
            FocusMode = Control.FocusModeEnum.All };
        scroll.GuiInput += e =>
        {
            if (!scroll.HasFocus()) return;
            var direction = e.IsActionPressed("ui_down", true) ? 1 : e.IsActionPressed("ui_up", true) ? -1 : 0;
            if (direction == 0) return;
            scroll.ScrollVertical += direction * Mathf.RoundToInt(28 * Scale(scroll)); scroll.AcceptEvent();
        };
        parent.AddChild(scroll); return scroll;
    }
    public static int PageDirection(InputEvent e, Control root)
    {
        if (root.GetViewport().GuiGetFocusOwner() is LineEdit or TextEdit) return 0;
        return e switch
        {
            InputEventKey { Pressed: true, Echo: false, Keycode: Key.Q } => -1,
            InputEventKey { Pressed: true, Echo: false, Keycode: Key.E } => 1,
            InputEventJoypadButton { Pressed: true, ButtonIndex: JoyButton.LeftShoulder } => -1,
            InputEventJoypadButton { Pressed: true, ButtonIndex: JoyButton.RightShoulder } => 1,
            _ => 0
        };
    }
    public static void Confirm(ConfirmationDialog dialog, float scale = 1)
    {
        if (dialog.Visible) return;
        dialog.Theme = CreateTheme(scale);
        dialog.GetLabel().AutowrapMode = TextServer.AutowrapMode.WordSmart;
        dialog.GetLabel().CustomMinimumSize = new Vector2(340, 0);
        dialog.PopupCentered(new Vector2I(380, Mathf.Min(330, Mathf.RoundToInt(160 * scale))));
        dialog.GetCancelButton().GrabFocus();
    }
}
