using Godot;
using TownRpg.Core.Domain;

namespace TownRpg.Presentation;

public static class DisplaySettingsController
{
    public static readonly Vector2I[] WindowedPresets =
    [
        new(1280, 720),
        new(1600, 900),
        new(1920, 1080)
    ];

    public static void Apply(PlayerSettings settings)
    {
        ArgumentNullException.ThrowIfNull(settings);
        if (settings.BorderlessFullscreen)
        {
            DisplayServer.WindowSetMode(DisplayServer.WindowMode.Fullscreen);
            return;
        }

        DisplayServer.WindowSetMode(DisplayServer.WindowMode.Windowed);
        DisplayServer.WindowSetFlag(DisplayServer.WindowFlags.Borderless, false);
        var screen = DisplayServer.WindowGetCurrentScreen();
        var usable = DisplayServer.ScreenGetUsableRect(screen);
        var size = ResolveWindowedSize(settings.WindowWidth, settings.WindowHeight, usable.Size);
        DisplayServer.WindowSetSize(size);
        DisplayServer.WindowSetPosition(usable.Position + (usable.Size - size) / 2);
    }

    public static void ToggleFullscreen(PlayerSettings settings)
    {
        ArgumentNullException.ThrowIfNull(settings);
        settings.BorderlessFullscreen = !settings.BorderlessFullscreen;
        Apply(settings);
    }

    public static int PresetIndex(int width, int height)
    {
        for (var index = 0; index < WindowedPresets.Length; index++)
        {
            if (WindowedPresets[index] == new Vector2I(width, height)) return index;
        }
        return 0;
    }

    public static Vector2I ResolveWindowedSize(int width, int height, Vector2I usableSize)
    {
        var requested = WindowedPresets[PresetIndex(width, height)];
        var supported = WindowedPresets
            .Where(size => size.X <= usableSize.X && size.Y <= usableSize.Y)
            .Where(size => size.X <= requested.X && size.Y <= requested.Y)
            .ToArray();
        if (supported.Length > 0) return supported[^1];

        var fittedWidth = Math.Min(usableSize.X, Mathf.FloorToInt(usableSize.Y * 16f / 9f));
        var fittedHeight = Mathf.FloorToInt(fittedWidth * 9f / 16f);
        return new Vector2I(Math.Max(fittedWidth, 640), Math.Max(fittedHeight, 360));
    }
}
