using Godot;

namespace TownRpg.Presentation;

// Metres, Y up. Read by the offline v2 producer; coordinates in saves stay unchanged.
public static class ArchiveRoomLayout3D
{
    public const float Width = 14f;
    public const float Depth = 12f;
    public const float Height = 4.2f;
    public const float WallThickness = 0.2f;
    public const float DoorWidth = 1.6f;
    public const float DoorHeight = 2.6f;
    public const float MainAisleWidth = 2.8f;
    public const float SecondaryAisleWidth = 1.5f;
    public static readonly Vector3 ExitHinge = new(6.98f, 0f, 3.4f);
    public static readonly Vector3 Entry = new(4.5f, 0f, 4.2f);
    public static readonly Vector3 RecorderCompanion = new(4.2f, 0f, 2.2f);
    public static readonly Vector3 WestStacksPosition = new(-6.5f, 1.35f, -1.4f);
    public static readonly Vector3 WestStacksSize = new(0.8f, 2.7f, 7.2f);
    public static readonly Vector3 NorthStacksPosition = new(-3.1f, 1.35f, -5.5f);
    public static readonly Vector3 NorthStacksSize = new(5.2f, 2.7f, 0.8f);
    public static readonly Vector3 SouthCabinetPosition = new(-5.3f, 1.1f, 5.3f);
    public static readonly Vector3 SouthCabinetSize = new(2.4f, 2.2f, 0.8f);
    public static readonly Vector3 RegistryTablePosition = new(-2.5f, 0.4f, 0f);
    public static readonly Vector3 RegistryTableSize = new(2.2f, 0.8f, 1.2f);
    public static readonly Vector3 DrawerMatrixPosition = new(-2.5f, 0.65f, -3.2f);
    public static readonly Vector3 DrawerMatrixSize = new(2.2f, 1.3f, 0.8f);
    public static readonly Vector3 RepairBenchPosition = new(5.8f, 0.4f, -2.4f);
    public static readonly Vector3 RepairBenchSize = new(1.6f, 0.8f, 1.6f);
    public static readonly Vector3 RecorderBenchPosition = new(5.8f, 0.4f, 1f);
    public static readonly Vector3 RecorderBenchSize = new(1.6f, 0.8f, 1.8f);
    public static readonly Vector3 TimelineTablePosition = new(0f, 0.4f, 5.4f);
    public static readonly Vector3 TimelineTableSize = new(2.6f, 0.8f, 0.9f);
    public static readonly Vector3 IndexTablePosition = new(-3.1f, 0.4f, 3.3f);
    public static readonly Vector3 IndexTableSize = new(1.4f, 0.8f, 0.9f);
    public static readonly Vector3 RouteTablePosition = new(2.7f, 0.4f, -5.4f);
    public static readonly Vector3 RouteTableSize = new(2.4f, 0.8f, 0.9f);
    public static readonly Rect2 WalkableBounds = new Rect2(-7, -6, 14, 12).Grow(-.32f);
    public static readonly AreaCoordinateMapper3D Mapper = new(new Rect2(16, 54, 608, 272), WalkableBounds);
    public static IReadOnlyDictionary<string, Vector3> MarkerPositions { get; } =
        new Dictionary<string, Vector3>(StringComparer.Ordinal)
        {
            ["area:dream.corridor"] = new(6.1f, 0, 4.2f),
            ["observe:target.archive.registry_rule"] = new(-4.8f, 0, 4.3f),
            ["observe:target.archive.registry_choir"] = new(-5.4f, 0, 1.4f),
            ["observe:target.archive.registry_broadcast"] = new(-5.4f, 0, -1f),
            ["observe:target.archive.registry_art"] = new(-5.4f, 0, -3.8f),
            ["observe:target.archive.registry_track"] = new(-.7f, 0, -4.5f),
            ["puzzle:archive_retrieval"] = new(-2.5f, 0, 1.5f),
            ["pickup:dream.archive_index_strip"] = new(-3.1f, 0, 4.3f),
            ["observe:target.archive.fuse_route"] = new(2.7f, 0, -4.3f),
            ["assembly:archive_fuse"] = new(4.1f, 0, -2.4f),
            ["target.archive.recorder"] = new(4.1f, 0, 1f),
            ["puzzle:archive_work_timeline"] = new(0, 0, 4.3f),
            ["hint:xiaoyu"] = new(1.1f, 0, 2.5f)
        };

    public static readonly LateRoomCollisionSpec[] Collisions =
    [
        new("Floor", new(0, -.1f, 0), new(14, .2f, 12)),
        new("Ceiling", new(0, 4.3f, 0), new(14, .2f, 12)),
        new("WallNorth", new(0, 2.1f, -6.1f), new(14.4f, 4.2f, .2f)),
        new("WallSouth", new(0, 2.1f, 6.1f), new(14.4f, 4.2f, .2f)),
        new("WallWest", new(-7.1f, 2.1f, 0), new(.2f, 4.2f, 12)),
        new("WallEastNorth", new(7.1f, 2.1f, -1.3f), new(.2f, 4.2f, 9.4f)),
        new("WallEastSouth", new(7.1f, 2.1f, 5.5f), new(.2f, 4.2f, 1f)),
        new("DoorLintel", new(7.1f, 3.4f, 4.2f), new(.2f, 1.6f, 1.6f)),
        new("DoorBarrier", new(6.98f, 1.3f, 4.2f), new(.16f, 2.6f, 1.6f)),
        new("WestStacks", WestStacksPosition, WestStacksSize, true),
        new("NorthStacks", NorthStacksPosition, NorthStacksSize, true),
        new("SouthCabinet", SouthCabinetPosition, SouthCabinetSize, true),
        new("RegistryTable", RegistryTablePosition, RegistryTableSize, true),
        new("DrawerMatrix", DrawerMatrixPosition, DrawerMatrixSize, true),
        new("DrawerExtensionBarrier", new(-2.5f, .7f, -2.59f), new(1.3f, 1.4f, .42f)),
        new("RepairBench", RepairBenchPosition, RepairBenchSize, true),
        new("RecorderBench", RecorderBenchPosition, RecorderBenchSize, true),
        new("TimelineTable", TimelineTablePosition, TimelineTableSize, true),
        new("IndexTable", IndexTablePosition, IndexTableSize, true),
        new("RouteTable", RouteTablePosition, RouteTableSize, true)
    ];
    public static bool IsBlocked(Vector3 p) => LateRoomLayout.IsBlocked(p, WalkableBounds, Collisions);
    public static Vector3[] Waypoints() => LateRoomLayout.Waypoints(WalkableBounds, IsBlocked, MarkerPositions.Values.Append(Entry).Append(RecorderCompanion));
    public static Vector3 ResolveSpawn(Vector3 p) => LateRoomLayout.ResolveSpawn(p, IsBlocked, Waypoints());
}
