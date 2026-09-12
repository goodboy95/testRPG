using Godot;
using TownRpg.Core.Domain;

namespace TownRpg.Presentation;

public readonly record struct MansionCollisionSpec(
    string Name, Vector3 Position, Vector3 Size, bool Furniture = false, bool Door = false);

// Godot metres, Y up. The Blender v3 producer reads these constants and anchors.
public static class MansionLayout3D
{
    public const float FoyerWidth = 10f;
    public const float FoyerDepth = 8f;
    public const float FoyerHeight = 6.6f;
    public const float HallWidth = 7f;
    public const float HallDepth = 12f;
    public const float HallHeight = 4.2f;
    public const float WallThickness = 0.2f;
    public const float SingleDoorWidth = 1.3f;
    public const float SingleDoorHeight = 2.55f;
    public const float FrontDoorWidth = 2.6f;
    public const float FrontDoorHeight = 3.05f;
    public const float DoubleDoorWidth = 2.8f;
    public const float DoubleDoorHeight = 2.75f;
    public const float ClearanceRadius = 0.32f;

    public static readonly Vector3 FrontDoor = new(0f, 0f, -8f);
    public static readonly Vector3 RoomOneDoor = new(-3.5f, 0f, 2.4f);
    public static readonly Vector3 RoomTwoDoor = new(3.5f, 0f, 2.4f);
    public static readonly Vector3 RoomThreeDoor = new(-3.5f, 0f, 8.8f);
    public static readonly Vector3 RoomFourDoor = new(0f, 0f, 12f);
    public static readonly Vector3 RoomOneEntry = new(-1.6f, 0f, 4f);
    public static readonly Vector3 RoomTwoEntry = new(1.6f, 0f, 4f);
    public static readonly Vector3 RoomThreeEntry = new(-1.6f, 0f, 7.2f);
    public static readonly Vector3 RoomFourEntry = new(0f, 0f, 9f);
    public static readonly Vector3 FireplacePosition = new(-4.56f, 1.15f, -4.2f);
    public static readonly Vector3 FireplaceSize = new(0.88f, 2.3f, 2.2f);
    public static readonly Vector3 ChairSouthPosition = new(-2.75f, 0.55f, -5.8f);
    public static readonly Vector3 ChairNorthPosition = new(-2.75f, 0.55f, -2.45f);
    public static readonly Vector3 ChairSize = new(1.12f, 1.1f, 1.02f);
    public static readonly Vector3 SideTablePosition = new(-2.95f, 0.32f, -4.15f);
    public static readonly Vector3 SideTableSize = new(0.66f, 0.64f, 0.66f);
    public static readonly Vector3 ConsolePosition = new(3.65f, 0.48f, -7.2f);
    public static readonly Vector3 ConsoleSize = new(1.8f, 0.96f, 0.62f);
    public static readonly Vector3 BenchPosition = new(3.08f, 0.57f, 6.6f);
    public static readonly Vector3 BenchSize = new(0.64f, 1.14f, 1.84f);
    public static readonly Vector3 StairPosition = new(3.7f, 2.6f, -3.3f);
    public static readonly Vector3 StairSize = new(2.4f, 5.2f, 4.6f);

    public static readonly MansionCollisionSpec[] Collisions = BuildCollisions();
    public static readonly Vector3[] SafeWaypoints = BuildWaypoints();

    public static IReadOnlyDictionary<string, Vector3> MarkerPositions { get; } =
        new Dictionary<string, Vector3>(StringComparer.Ordinal)
        {
            [$"area:{DreamEscapeIds.AreaClassroom}"] = RoomOneDoor + new Vector3(1.15f, 0f, 0f),
            ["target.corridor.room_two_door"] = RoomTwoDoor + new Vector3(-1.15f, 0f, 0f),
            [$"area:{DreamEscapeIds.AreaMusic}"] = RoomTwoDoor + new Vector3(-1.15f, 0f, 0f),
            ["target.corridor.room_three_door"] = RoomThreeDoor + new Vector3(1.15f, 0f, 0f),
            [$"area:{DreamEscapeIds.AreaArchive}"] = RoomThreeDoor + new Vector3(1.15f, 0f, 0f),
            ["target.corridor.room_four_door"] = RoomFourDoor + new Vector3(0f, 0f, -1.4f),
            [$"area:{DreamEscapeIds.AreaAuditorium}"] = RoomFourDoor + new Vector3(0f, 0f, -1.4f),
            ["locked:mansion_front_door"] = FrontDoor + new Vector3(0f, 0f, 1.4f)
        };

    public static bool IsBlocked(Vector3 position)
    {
        var inFoyer = position.X >= -FoyerWidth / 2 && position.X <= FoyerWidth / 2 &&
                      position.Z >= -FoyerDepth && position.Z <= 0f;
        var inHall = position.X >= -HallWidth / 2 && position.X <= HallWidth / 2 &&
                     position.Z >= 0f && position.Z <= HallDepth;
        if (!inFoyer && !inHall) return true;
        foreach (var spec in Collisions)
        {
            // Floors, ceilings and the high portal beam do not obstruct a standing capsule.
            if (spec.Position.Y + spec.Size.Y / 2 <= 0.05f ||
                spec.Position.Y - spec.Size.Y / 2 >= 1.6f) continue;
            var footprint = new Rect2(spec.Position.X - spec.Size.X / 2,
                spec.Position.Z - spec.Size.Z / 2, spec.Size.X, spec.Size.Z).Grow(ClearanceRadius);
            if (footprint.HasPoint(new Vector2(position.X, position.Z))) return true;
        }
        return false;
    }

    public static Vector3 ResolveSpawn(Vector3 requested)
    {
        requested.Y = 0f;
        if (!IsBlocked(requested)) return requested;
        return SafeWaypoints.OrderBy(point => point.DistanceSquaredTo(requested)).First();
    }

    private static MansionCollisionSpec[] BuildCollisions()
    {
        var halfWall = WallThickness / 2;
        var shoulder = (FoyerWidth - HallWidth) / 2;
        return
        [
            new("FloorFoyer", new(0, -0.1f, -FoyerDepth / 2), new(FoyerWidth, 0.2f, FoyerDepth)),
            new("FloorHall", new(0, -0.1f, HallDepth / 2), new(HallWidth, 0.2f, HallDepth)),
            new("CeilingFoyer", new(0, FoyerHeight + halfWall, -FoyerDepth / 2), new(FoyerWidth, WallThickness, FoyerDepth)),
            new("CeilingHall", new(0, HallHeight + halfWall, HallDepth / 2), new(HallWidth, WallThickness, HallDepth)),
            new("WallFoyerWest", new(-FoyerWidth / 2 - halfWall, FoyerHeight / 2, -FoyerDepth / 2), new(WallThickness, FoyerHeight, FoyerDepth)),
            new("WallFoyerEast", new(FoyerWidth / 2 + halfWall, FoyerHeight / 2, -FoyerDepth / 2), new(WallThickness, FoyerHeight, FoyerDepth)),
            new("WallHallWest", new(-HallWidth / 2 - halfWall, HallHeight / 2, HallDepth / 2), new(WallThickness, HallHeight, HallDepth)),
            new("WallHallEast", new(HallWidth / 2 + halfWall, HallHeight / 2, HallDepth / 2), new(WallThickness, HallHeight, HallDepth)),
            new("WallSouth", new(0, FoyerHeight / 2, -FoyerDepth - halfWall), new(FoyerWidth + WallThickness, FoyerHeight, WallThickness)),
            new("WallNorth", new(0, HallHeight / 2, HallDepth + halfWall), new(HallWidth + WallThickness, HallHeight, WallThickness)),
            new("WallShoulderWest", new(-(HallWidth + shoulder) / 2, FoyerHeight / 2, halfWall), new(shoulder, FoyerHeight, WallThickness)),
            new("WallShoulderEast", new((HallWidth + shoulder) / 2, FoyerHeight / 2, halfWall), new(shoulder, FoyerHeight, WallThickness)),
            new("PortalBeam", new(0, (HallHeight + FoyerHeight) / 2, 0), new(HallWidth, FoyerHeight - HallHeight, 0.3f)),
            new("DoorBarrierFront", FrontDoor + new Vector3(0, FrontDoorHeight / 2, 0.02f), new(FrontDoorWidth, FrontDoorHeight, 0.24f), Door: true),
            new("DoorBarrierRoom1", RoomOneDoor + new Vector3(0.02f, SingleDoorHeight / 2, 0), new(0.24f, SingleDoorHeight, SingleDoorWidth), Door: true),
            new("DoorBarrierRoom2", RoomTwoDoor + new Vector3(-0.02f, SingleDoorHeight / 2, 0), new(0.24f, SingleDoorHeight, SingleDoorWidth), Door: true),
            new("DoorBarrierRoom3", RoomThreeDoor + new Vector3(0.02f, SingleDoorHeight / 2, 0), new(0.24f, SingleDoorHeight, SingleDoorWidth), Door: true),
            new("DoorBarrierRoom4", RoomFourDoor + new Vector3(0, DoubleDoorHeight / 2, -0.02f), new(DoubleDoorWidth, DoubleDoorHeight, 0.24f), Door: true),
            new("Fireplace", FireplacePosition, FireplaceSize, Furniture: true),
            new("ChairSouth", ChairSouthPosition, ChairSize, Furniture: true),
            new("ChairNorth", ChairNorthPosition, ChairSize, Furniture: true),
            new("SideTable", SideTablePosition, SideTableSize, Furniture: true),
            new("ConsoleTable", ConsolePosition, ConsoleSize, Furniture: true),
            new("FoyerBench", BenchPosition, BenchSize, Furniture: true),
            new("StairEnclosure", StairPosition, StairSize, Furniture: true)
        ];
    }

    private static Vector3[] BuildWaypoints()
    {
        float[] xs = [-4.35f, -3.7f, -1.7f, 0f, 1.7f, 3.7f, 4.4f];
        float[] zs = [-7.15f, -6.5f, -5.1f, -3.4f, -1f, 0.8f, 2.4f, 4.3f, 6.5f, 8.8f, 10.6f];
        return xs.SelectMany(x => zs.Select(z => new Vector3(x, 0, z)))
            .Concat([RoomOneEntry, RoomTwoEntry, RoomThreeEntry, RoomFourEntry])
            .Where(point => !IsBlocked(point)).Distinct().ToArray();
    }
}
