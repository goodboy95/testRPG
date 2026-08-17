using Godot;

namespace TownRpg.Presentation;

public static class DreamCharacterFrames
{
    public const int FrameWidth = 32;
    public const int FrameHeight = 48;
    public static readonly string[] DirectionNames = ["down", "left", "right", "up"];

    public static SpriteFrames Create(Texture2D sheet, double framesPerSecond = 6d)
    {
        ArgumentNullException.ThrowIfNull(sheet);
        if (sheet.GetWidth() != FrameWidth * 4 || sheet.GetHeight() != FrameHeight * 4)
        {
            throw new ArgumentException(
                $"Dream character sheet must be 128x192, got {sheet.GetWidth()}x{sheet.GetHeight()}.",
                nameof(sheet));
        }

        var result = new SpriteFrames();
        result.RemoveAnimation("default");
        for (var row = 0; row < DirectionNames.Length; row++)
        {
            var animation = new StringName(DirectionNames[row]);
            result.AddAnimation(animation);
            result.SetAnimationLoop(animation, true);
            result.SetAnimationSpeed(animation, framesPerSecond);
            for (var column = 0; column < 4; column++)
            {
                result.AddFrame(animation, new AtlasTexture
                {
                    Atlas = sheet,
                    Region = new Rect2(column * FrameWidth, row * FrameHeight, FrameWidth, FrameHeight)
                });
            }
        }
        return result;
    }
}
