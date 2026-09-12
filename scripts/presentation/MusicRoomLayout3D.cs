using Godot;

namespace TownRpg.Presentation;

public readonly record struct MusicCollisionSpec(
    string Name, Vector3 Position, Vector3 Size, bool Furniture = false, string Phase = "always");

// Metres, Y up. The offline Blender producer reads these dimensions and anchors.
public static class MusicRoomLayout3D
{
    public const float Width = 12f;
    public const float Depth = 10f;
    public const float Height = 4f;
    public const float WallThickness = 0.2f;
    public const float ClearanceRadius = 0.32f;
    public const float DoorWidth = 1.4f;
    public const float DoorHeight = 2.55f;
    public const float EquipmentWest = 2.8f;
    public const float EquipmentSouth = -1.8f;
    public const float MainAisleWidth = 3f;

    public static readonly Vector3 ExitDoor = new(-6f, 0f, 3.55f);
    public static readonly Vector3 ExitHinge = new(-5.98f, 0f, 2.85f);
    public static readonly Vector3 Entry = new(-3.35f, 0f, 3.6f);
    public static readonly Vector3 EquipmentDoor = new(4.35f, 0f, -1.8f);
    public static readonly Vector3 EquipmentHinge = new(5.05f, 0f, -1.8f);
    public static readonly Vector3 BeforeJoin = new(4.2f, 0f, -3.15f);
    public static readonly Vector3 SideObservation = new(3.45f, 0f, -3.85f);
    public static readonly Vector3 EquipmentExit = new(4.35f, 0f, -0.65f);
    public static readonly Vector3 PianoPosition = new(0f, 0.65f, 4.48f);
    public static readonly Vector3 PianoSize = new(1.62f, 1.3f, 0.82f);
    public static readonly Vector3 BenchPosition = new(0f, 0.26f, 3.42f);
    public static readonly Vector3 BenchSize = new(0.9f, 0.52f, 0.46f);
    public static readonly Vector3 PartsTrayPosition = new(-1.65f, 0.4f, 4.4f);
    public static readonly Vector3 PartsTraySize = new(0.58f, 0.8f, 0.6f);
    public static readonly Vector3 RollDeskPosition = new(2.8f, 0.4f, 4.25f);
    public static readonly Vector3 RollDeskSize = new(1.85f, 0.8f, 0.95f);
    public static readonly Vector3 ScoreDeskPosition = new(-4.35f, 0.4f, -4.3f);
    public static readonly Vector3 ScoreDeskSize = new(1.65f, 0.8f, 0.95f);
    public static readonly Vector3 ToolsDeskPosition = new(-1.5f, 0.4f, -4.3f);
    public static readonly Vector3 ToolsDeskSize = new(2.6f, 0.8f, 0.85f);
    public static readonly Vector3 CalibrationDeskPosition = new(1.45f, 0.4f, -3.5f);
    public static readonly Vector3 CalibrationDeskSize = new(1.85f, 0.8f, 0.85f);
    public static readonly Vector3 PhotoCabinetPosition = new(-5.57f, 0.95f, -0.45f);
    public static readonly Vector3 PhotoCabinetSize = new(0.7f, 1.9f, 1.15f);
    public static readonly Vector3 AcousticCabinetPosition = new(5.57f, 1.15f, 1.25f);
    public static readonly Vector3 AcousticCabinetSize = new(0.72f, 2.3f, 2.6f);
    public static readonly Vector3 EquipmentShelfPosition = new(5.62f, 1.1f, -3.6f);
    public static readonly Vector3 EquipmentShelfSize = new(0.6f, 2.2f, 2.3f);
    public static readonly Vector3 ChairSize = new(0.58f, 0.85f, 0.58f);
    public static readonly Vector3 Chair01 = new(-3.7f, 0.425f, -1.1f);
    public static readonly Vector3 Chair02 = new(-2.4f, 0.425f, -1.1f);
    public static readonly Vector3 Chair03 = new(-3.7f, 0.425f, 0.4f);
    public static readonly Vector3 Chair04 = new(-2.4f, 0.425f, 0.4f);
    public static readonly Vector3 Chair05 = new(-3.7f, 0.425f, 1.9f);
    public static readonly Vector3 Chair06 = new(-2.4f, 0.425f, 1.9f);
    public static readonly Vector3[] Chairs = [Chair01, Chair02, Chair03, Chair04, Chair05, Chair06];

    public static readonly Rect2 StoredBounds = new(16f, 54f, 608f, 272f);
    public static readonly Rect2 WalkableBounds = new Rect2(-Width / 2, -Depth / 2, Width, Depth).Grow(-ClearanceRadius);
    public static readonly AreaCoordinateMapper3D Mapper = new(StoredBounds, WalkableBounds);

    public static IReadOnlyDictionary<string, Vector3> MarkerPositions { get; } =
        new Dictionary<string, Vector3>(StringComparer.Ordinal)
        {
            ["area:dream.corridor"] = new(-5.3f, 0f, 3.55f),
            ["target.music.score"] = new(-4.35f, 0f, -3.3f),
            ["puzzle:music_cage"] = new(4.35f, 0f, -0.8f),
            ["pickup:dream.rubber_mallet"] = new(-2.4f, 0f, -3.45f),
            ["pickup:dream.listening_cone"] = new(-0.65f, 0f, -3.45f),
            ["pickup:dream.metronome_weight"] = new(-1.8f, 0f, 3.45f),
            ["pickup:dream.torn_piano_roll"] = new(2.15f, 0f, 3.2f),
            ["pickup:dream.wax_patch"] = new(4.45f, 0f, -3.45f),
            ["target.music.metronome"] = new(-0.98f, 0f, 3.6f),
            ["puzzle:music_channel_calibration"] = new(1.4f, 0f, -2.25f),
            ["puzzle:music_piano_roll"] = new(3.4f, 0f, 3.2f),
            ["puzzle:music_acoustic_wall"] = new(4.6f, 0f, 1.25f),
            ["puzzle:music_backstage_photo"] = new(-4.85f, 0f, -0.45f),
            ["observe:target.music.side_wave"] = SideObservation,
            ["hint:xiaoyu"] = new(-0.9f, 0f, 0.6f),
            ["observe:target.music.tempo_notice"] = new(-4.85f, 0f, 2f),
            ["observe:target.music.fork_reference"] = new(4.25f, 0f, -4.1f),
            ["observe:target.music.roll_template"] = new(-4.85f, 0f, -2.15f)
        };

    public static readonly MusicCollisionSpec[] Collisions = BuildCollisions();

    public static bool IsActive(MusicCollisionSpec spec, bool open) =>
        spec.Phase == "always" || spec.Phase == (open ? "open" : "closed");

    public static bool IsBlocked(Vector3 position, bool open)
    {
        if (!WalkableBounds.HasPoint(new Vector2(position.X, position.Z))) return true;
        foreach (var spec in Collisions)
        {
            if (!IsActive(spec, open) || spec.Position.Y + spec.Size.Y / 2 <= 0.05f ||
                spec.Position.Y - spec.Size.Y / 2 >= 1.6f) continue;
            var footprint = new Rect2(spec.Position.X - spec.Size.X / 2,
                spec.Position.Z - spec.Size.Z / 2, spec.Size.X, spec.Size.Z).Grow(ClearanceRadius);
            if (footprint.HasPoint(new Vector2(position.X, position.Z))) return true;
        }
        // Closed equipment room is not a valid player restore destination.
        return !open && position.X > EquipmentWest && position.Z < EquipmentSouth;
    }

    public static Vector3[] Waypoints(bool open)
    {
        float[] xs = [-5f, -4.6f, -3.7f, -2.4f, -1.2f, 0f, 1.4f, 2.1f, 3.45f, 4.35f, 4.6f];
        float[] zs = [-4.1f, -3.45f, -2.25f, -0.6f, 0.8f, 2.7f, 3.6f];
        return xs.SelectMany(x => zs.Select(z => new Vector3(x, 0, z)))
            .Concat(MarkerPositions.Values).Concat([Entry, EquipmentExit, BeforeJoin, SideObservation])
            .Where(p => !IsBlocked(p, open)).Distinct().ToArray();
    }

    public static Vector3 ResolveSpawn(Vector3 requested, bool open)
    {
        requested.Y = 0f;
        requested.X = Mathf.Clamp(requested.X, WalkableBounds.Position.X + .001f, WalkableBounds.End.X - .001f);
        requested.Z = Mathf.Clamp(requested.Z, WalkableBounds.Position.Y + .001f, WalkableBounds.End.Y - .001f);
        return IsBlocked(requested, open)
            ? Waypoints(open).MinBy(point => point.DistanceSquaredTo(requested))
            : requested;
    }

    private static MusicCollisionSpec[] BuildCollisions()
    {
        var result = new List<MusicCollisionSpec>
        {
            new("Floor", new(0, -0.1f, 0), new(Width, .2f, Depth)),
            new("Ceiling", new(0, 4.1f, 0), new(Width, .2f, Depth)),
            new("WallNorth", new(0, 2, -5.1f), new(12.4f, 4, .2f)),
            new("WallSouth", new(0, 2, 5.1f), new(12.4f, 4, .2f)),
            new("WallEast", new(6.1f, 2, 0), new(.2f, 4, 10)),
            new("WallWestNorth", new(-6.1f, 2, -1.075f), new(.2f, 4, 7.85f)),
            new("WallWestSouth", new(-6.1f, 2, 4.625f), new(.2f, 4, .75f)),
            new("ExitLintel", new(-6.1f, 3.275f, 3.55f), new(.2f, 1.45f, DoorWidth)),
            new("ExitBarrier", new(-5.97f, 1.275f, 3.55f), new(.16f, DoorHeight, DoorWidth)),
            new("EquipmentWestWall", new(2.8f, 2, -3.4f), new(.18f, 4, 3.2f)),
            new("EquipmentSouthLeft", new(3.225f, 2, -1.8f), new(.85f, 4, .18f)),
            new("EquipmentSouthRight", new(5.525f, 2, -1.8f), new(.95f, 4, .18f)),
            new("EquipmentLintel", new(4.35f, 3.275f, -1.8f), new(DoorWidth, 1.45f, .18f)),
            new("EquipmentDoorClosed", new(4.35f, 1.275f, -1.8f), new(DoorWidth, DoorHeight, .24f), Phase: "closed"),
            new("EquipmentDoorOpen", new(5.05f, 1.275f, -2.5f), new(.24f, DoorHeight, DoorWidth), Phase: "open"),
            new("Piano", PianoPosition, PianoSize, true),
            new("PianoBench", BenchPosition, BenchSize, true),
            new("PartsTray", PartsTrayPosition, PartsTraySize, true),
            new("RollDesk", RollDeskPosition, RollDeskSize, true),
            new("ScoreDesk", ScoreDeskPosition, ScoreDeskSize, true),
            new("ToolsDesk", ToolsDeskPosition, ToolsDeskSize, true),
            new("CalibrationDesk", CalibrationDeskPosition, CalibrationDeskSize, true),
            new("PhotoCabinet", PhotoCabinetPosition, PhotoCabinetSize, true),
            new("AcousticCabinet", AcousticCabinetPosition, AcousticCabinetSize, true),
            new("EquipmentShelf", EquipmentShelfPosition, EquipmentShelfSize, true)
        };
        for (var i = 0; i < Chairs.Length; i++)
            result.Add(new($"Chair{i + 1:00}", Chairs[i], ChairSize, true));
        return result.ToArray();
    }
}
