using Godot;

namespace TownRpg.Presentation;

public static class CameraRelativeMovement
{
    public static Vector3 ToWorld(Vector2 screenInput, Camera3D camera)
    {
        ArgumentNullException.ThrowIfNull(camera);
        var right = Flatten(camera.GlobalTransform.Basis.X);
        var forward = Flatten(-camera.GlobalTransform.Basis.Z);
        var result = right * screenInput.X + forward * -screenInput.Y;
        var inputLength = Mathf.Min(screenInput.Length(), 1f);
        return result.LengthSquared() <= 0.0001f
            ? Vector3.Zero
            : result.Normalized() * inputLength;
    }

    public static Vector2 ToScreen(Vector3 worldMotion, Camera3D camera)
    {
        ArgumentNullException.ThrowIfNull(camera);
        if (worldMotion.LengthSquared() <= 0.0001f) return Vector2.Zero;
        var right = Flatten(camera.GlobalTransform.Basis.X);
        var forward = Flatten(-camera.GlobalTransform.Basis.Z);
        return new Vector2(worldMotion.Dot(right), -worldMotion.Dot(forward));
    }

    private static Vector3 Flatten(Vector3 value)
    {
        value.Y = 0f;
        return value.LengthSquared() <= 0.0001f ? Vector3.Forward : value.Normalized();
    }
}
