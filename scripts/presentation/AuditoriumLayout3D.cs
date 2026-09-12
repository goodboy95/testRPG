using Godot;

namespace TownRpg.Presentation;

// Metres, Y up. Shared by Blender, collision, interactions, restore and companion routes.
public static class AuditoriumLayout3D
{
    public const float Width = 22f;
    public const float Depth = 16f;
    public const float Height = 7.5f;
    public const float WallThickness = 0.2f;
    public const float DoorWidth = 1.8f;
    public const float DoorHeight = 2.8f;
    public const float MainAisleWidth = 3.38f;
    public const float SecondaryAisleWidth = 1.5f;
    public const float WeightMinimumHeight = 1.3f;
    public const float WeightStep = 0.45f;
    public static readonly Vector3 CurtainTop = new(0f, 6.7f, -4.4f);
    public static readonly Vector3 Weight01 = new(9.3f, 1.3f, -6.75f);
    public static readonly Vector3 Weight02 = new(9.3f, 1.3f, -5.92f);
    public static readonly Vector3 Weight03 = new(9.3f, 1.3f, -5.09f);
    public static readonly Vector3 Weight04 = new(9.3f, 1.3f, -4.26f);
    public static readonly Vector3 Disc01 = new(-9.24f, 1.45f, -5.18f);
    public static readonly Vector3 Disc02 = new(-9.24f, 1.45f, -4.8f);
    public static readonly Vector3 Disc03 = new(-9.24f, 1.45f, -4.42f);
    public static readonly Vector3 MasterReel = new(9.2f, 1.23f, 5.8f);
    public static readonly Vector3 ExitHinge = new(-0.9f, 0f, 7.98f);
    public static readonly Vector3 Entry = new(0f, 0f, 5.2f);
    public static readonly Vector3 SideObservation = new(-7.95f, 0f, -7.1f);
    public static readonly Vector3 LeftLeverCompanion = new(-8.5f, 0f, -0.8f);
    public static readonly Vector3 StagePosition = new(0f, 0.55f, -6f);
    public static readonly Vector3 StageSize = new(14f, 1.1f, 4f);
    public static readonly Vector3 ProjectorPosition = new(-9.7f, 1f, -4.8f);
    public static readonly Vector3 ProjectorSize = new(2f, 2f, 1.4f);
    public static readonly Vector3 FlyRailPosition = new(9.7f, 3.2f, -5.5f);
    public static readonly Vector3 FlyRailSize = new(1.4f, 6.4f, 3.8f);
    public static readonly Vector3 LensTablePosition = new(-9.7f, 0.4f, 0.5f);
    public static readonly Vector3 LensTableSize = new(1.2f, 0.8f, 0.8f);
    public static readonly Vector3 WeightTablePosition = new(9.7f, 0.4f, 0.5f);
    public static readonly Vector3 WeightTableSize = new(1.2f, 0.8f, 0.8f);
    public static readonly Vector3 TapeReferenceTablePosition = new(-9.7f, 0.4f, 3.1f);
    public static readonly Vector3 TapeReferenceTableSize = new(1.6f, 0.8f, 1.2f);
    public static readonly Vector3 SpliceBenchPosition = new(9.7f, 0.4f, 3.1f);
    public static readonly Vector3 SpliceBenchSize = new(1.6f, 0.8f, 1.2f);
    public static readonly Vector3 MasterPlayerPosition = new(9.7f, 0.8f, 5.8f);
    public static readonly Vector3 MasterPlayerSize = new(1.6f, 1.6f, 1f);
    public static readonly Vector3 LightConsolePosition = new(-6f, 0.5f, 6.95f);
    public static readonly Vector3 LightConsoleSize = new(1.8f, 1f, 1f);
    public static readonly Vector3 SoundConsolePosition = new(-3.3f, 0.5f, 6.95f);
    public static readonly Vector3 SoundConsoleSize = new(1.8f, 1f, 1f);
    public static readonly Vector3 BroadcastConsolePosition = new(5.8f, 0.5f, 6.95f);
    public static readonly Vector3 BroadcastConsoleSize = new(1.8f, 1f, 1f);
    public static readonly Vector3 LeftLeverPosition = new(-8.6f, 0.65f, -1.8f);
    public static readonly Vector3 LeftLeverSize = new(0.55f, 1.3f, 0.6f);
    public static readonly Vector3 RightSwitchPosition = new(8.6f, 0.65f, -1.8f);
    public static readonly Vector3 RightSwitchSize = new(0.55f, 1.3f, 0.6f);
    public static readonly Vector3 SideStandPosition = new(-7.13f, 0.85f, -7.1f);
    public static readonly Vector3 SideStandSize = new(0.15f, 1.7f, 0.85f);
    public static readonly Vector3 SeatSize = new(0.62f, 0.95f, 0.72f);
    public static readonly Vector3 Seat01 = new(-2f, 0.475f, -1f);
    public static readonly Vector3 Seat02 = new(-2.88f, 0.475f, -1f);
    public static readonly Vector3 Seat03 = new(-3.76f, 0.475f, -1f);
    public static readonly Vector3 Seat04 = new(-4.64f, 0.475f, -1f);
    public static readonly Vector3 Seat05 = new(-2f, 0.475f, 0.75f);
    public static readonly Vector3 Seat06 = new(-2.88f, 0.475f, 0.75f);
    public static readonly Vector3 Seat07 = new(-3.76f, 0.475f, 0.75f);
    public static readonly Vector3 Seat08 = new(-4.64f, 0.475f, 0.75f);
    public static readonly Vector3 Seat09 = new(-2f, 0.475f, 2.5f);
    public static readonly Vector3 Seat10 = new(-2.88f, 0.475f, 2.5f);
    public static readonly Vector3 Seat11 = new(-3.76f, 0.475f, 2.5f);
    public static readonly Vector3 Seat12 = new(-4.64f, 0.475f, 2.5f);
    public static readonly Vector3 Seat13 = new(-2f, 0.475f, 4.25f);
    public static readonly Vector3 Seat14 = new(-2.88f, 0.475f, 4.25f);
    public static readonly Vector3 Seat15 = new(-3.76f, 0.475f, 4.25f);
    public static readonly Vector3 Seat16 = new(-4.64f, 0.475f, 4.25f);
    public static readonly Vector3 Seat17 = new(2f, 0.475f, -1f);
    public static readonly Vector3 Seat18 = new(2.88f, 0.475f, -1f);
    public static readonly Vector3 Seat19 = new(3.76f, 0.475f, -1f);
    public static readonly Vector3 Seat20 = new(4.64f, 0.475f, -1f);
    public static readonly Vector3 Seat21 = new(2f, 0.475f, 0.75f);
    public static readonly Vector3 Seat22 = new(2.88f, 0.475f, 0.75f);
    public static readonly Vector3 Seat23 = new(3.76f, 0.475f, 0.75f);
    public static readonly Vector3 Seat24 = new(4.64f, 0.475f, 0.75f);
    public static readonly Vector3 Seat25 = new(2f, 0.475f, 2.5f);
    public static readonly Vector3 Seat26 = new(2.88f, 0.475f, 2.5f);
    public static readonly Vector3 Seat27 = new(3.76f, 0.475f, 2.5f);
    public static readonly Vector3 Seat28 = new(4.64f, 0.475f, 2.5f);
    public static readonly Vector3 Seat29 = new(2f, 0.475f, 4.25f);
    public static readonly Vector3 Seat30 = new(2.88f, 0.475f, 4.25f);
    public static readonly Vector3 Seat31 = new(3.76f, 0.475f, 4.25f);
    public static readonly Vector3 Seat32 = new(4.64f, 0.475f, 4.25f);
    public static readonly Vector3[] Seats = [Seat01,Seat02,Seat03,Seat04,Seat05,Seat06,Seat07,Seat08,
        Seat09,Seat10,Seat11,Seat12,Seat13,Seat14,Seat15,Seat16,Seat17,Seat18,Seat19,Seat20,Seat21,Seat22,Seat23,Seat24,
        Seat25,Seat26,Seat27,Seat28,Seat29,Seat30,Seat31,Seat32];
    public static readonly Rect2 WalkableBounds = new Rect2(-11, -8, 22, 16).Grow(-.32f);
    public static readonly AreaCoordinateMapper3D Mapper = new(new Rect2(16, 54, 608, 272), WalkableBounds);
    public static IReadOnlyDictionary<string, Vector3> MarkerPositions { get; } = new Dictionary<string, Vector3>(StringComparer.Ordinal)
    {
        ["area:dream.corridor"] = new(0, 0, 7.05f),
        ["observe:target.auditorium.line_set_notches"] = new(0, 0, -3.05f),
        ["pickup:dream.curtain_counterweight"] = new(8.1f, 0, .5f),
        ["target.auditorium.line_set"] = new(8.1f, 0, -5.2f),
        ["observe:target.auditorium.projection_order"] = SideObservation,
        ["pickup:dream.projector_lens"] = new(-8.1f, 0, .5f),
        ["target.auditorium.projector"] = new(-8.1f, 0, -4.8f),
        ["observe:target.auditorium.tape_order"] = new(-8.1f, 0, 3.1f),
        ["puzzle:auditorium_tape_reconstruction"] = new(8.1f, 0, 3.1f),
        ["target.auditorium.master_player"] = new(8.1f, 0, 5.8f),
        ["observe:target.auditorium.handoff_circuit"] = new(-6, 0, 5.7f),
        ["action:handoff_monitor"] = new(-3.3f, 0, 5.7f),
        ["puzzle:auditorium_handoff_plan"] = new(5.8f, 0, 5.7f),
        ["action:left_lever"] = new(-7.55f, 0, -1.8f),
        ["action:open_exit"] = new(7.55f, 0, -1.8f)
    };
    public static readonly LateRoomCollisionSpec[] Collisions = BuildCollisions();
    public static bool IsBlocked(Vector3 p) => LateRoomLayout.IsBlocked(p, WalkableBounds, Collisions);
    public static Vector3[] Waypoints() => LateRoomLayout.Waypoints(WalkableBounds, IsBlocked, MarkerPositions.Values.Append(Entry).Append(LeftLeverCompanion));
    public static Vector3 ResolveSpawn(Vector3 p) => LateRoomLayout.ResolveSpawn(p, IsBlocked, Waypoints());
    private static LateRoomCollisionSpec[] BuildCollisions()
    {
        var specs = new List<LateRoomCollisionSpec>
        {
            new("Floor", new(0, -.1f, 0), new(22, .2f, 16)),
            new("Ceiling", new(0, 7.6f, 0), new(22, .2f, 16)),
            new("WallNorth", new(0, 3.75f, -8.1f), new(22.4f, 7.5f, .2f)),
            new("WallEast", new(11.1f, 3.75f, 0), new(.2f, 7.5f, 16)),
            new("WallWest", new(-11.1f, 3.75f, 0), new(.2f, 7.5f, 16)),
            new("WallSouthWest", new(-5.95f, 3.75f, 8.1f), new(10.1f, 7.5f, .2f)),
            new("WallSouthEast", new(5.95f, 3.75f, 8.1f), new(10.1f, 7.5f, .2f)),
            new("DoorLintel", new(0, 5.15f, 8.1f), new(1.8f, 4.7f, .2f)),
            new("DoorBarrier", new(0, 1.4f, 7.98f), new(1.8f, 2.8f, .16f)),
            new("Stage", StagePosition, StageSize, true),
            new("ScenicStageBarrier", new(0,3.75f,-5.9f), new(14,7.5f,4.2f)),
            new("Projector", ProjectorPosition, ProjectorSize, true),
            new("FlyRail", FlyRailPosition, FlyRailSize, true),
            new("LensTable", LensTablePosition, LensTableSize, true),
            new("WeightTable", WeightTablePosition, WeightTableSize, true),
            new("TapeReferenceTable", TapeReferenceTablePosition, TapeReferenceTableSize, true),
            new("SpliceBench", SpliceBenchPosition, SpliceBenchSize, true),
            new("MasterPlayer", MasterPlayerPosition, MasterPlayerSize, true),
            new("LightConsole", LightConsolePosition, LightConsoleSize, true),
            new("SoundConsole", SoundConsolePosition, SoundConsoleSize, true),
            new("BroadcastConsole", BroadcastConsolePosition, BroadcastConsoleSize, true),
            new("LeftLever", LeftLeverPosition, LeftLeverSize, true),
            new("RightSwitch", RightSwitchPosition, RightSwitchSize, true),
            new("SideStand", SideStandPosition, SideStandSize, true)
        };
        for(var i=0;i<Seats.Length;i++) specs.Add(new($"Seat{i+1:00}", Seats[i], SeatSize, true));
        return specs.ToArray();
    }
}
