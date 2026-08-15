using Godot;

namespace TownRpg.Presentation;

public static class UiStyles
{
    public static Theme CreateTheme()
    {
        var theme = new Theme
        {
            DefaultFont = CreateFont(),
            DefaultFontSize = 14
        };
        return theme;
    }

    public static SystemFont CreateFont() => new()
    {
        FontNames = ["Microsoft YaHei UI", "Microsoft YaHei", "Noto Sans CJK SC", "Arial"],
        AllowSystemFallback = true
    };

    public static StyleBoxFlat Panel(Color? color = null, int radius = 8)
    {
        var style = new StyleBoxFlat
        {
            BgColor = color ?? new Color("202939e6"),
            BorderColor = new Color("b9d1c5"),
            BorderWidthLeft = 1,
            BorderWidthTop = 1,
            BorderWidthRight = 1,
            BorderWidthBottom = 1,
            CornerRadiusTopLeft = radius,
            CornerRadiusTopRight = radius,
            CornerRadiusBottomLeft = radius,
            CornerRadiusBottomRight = radius,
            ContentMarginLeft = 14,
            ContentMarginRight = 14,
            ContentMarginTop = 10,
            ContentMarginBottom = 10
        };
        return style;
    }

    public static Button Button(string text)
    {
        var button = new Button
        {
            Text = text,
            FocusMode = Control.FocusModeEnum.All,
            CustomMinimumSize = new Vector2(0, 30)
        };
        return button;
    }
}
