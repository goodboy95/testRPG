using Godot;

namespace TownRpg.Presentation;

// Godot metres, Y up. The v6 Blender producer reads these same layout constants.
public static class ClassroomLayout3D
{
    public static readonly float[] DeskColumns = [-3.55f, -2.25f, 2.25f, 3.55f];
    public static readonly float[] DeskRows = [-1.00f, 0.65f, 2.25f];
    public const float DeskWidth = 0.80f;
    public const float DeskTopHeight = 0.75f;
    public const float ChairSeatHeight = 0.44f;
    public const float GroupDepthOffset = 0.20f;
    public static readonly Vector3 CollisionSize = new(0.82f, 0.88f, 1.02f);
    public static IEnumerable<Vector3> DeskPositions =>
        DeskRows.SelectMany(z => DeskColumns.Select(x => new Vector3(x, 0f, z)));
}
