using Godot;
using TownRpg.Core.Domain;

namespace TownRpg.Presentation;

public readonly record struct DreamAreaSpawnRoute(Vector2 StoredPosition, Vector3 EntryFacing);

public static class DreamAreaSpawnCatalog
{
    private static readonly DreamMansionCoordinateMapper3D MansionMapper = new();

    public static readonly Vector3 MansionRoomOneEntryWorld = MansionLayout3D.RoomOneEntry;
    public static readonly Vector3 MansionRoomTwoEntryWorld = MansionLayout3D.RoomTwoEntry;
    public static readonly Vector3 MansionRoomThreeEntryWorld = MansionLayout3D.RoomThreeEntry;
    public static readonly Vector3 MansionRoomFourEntryWorld = MansionLayout3D.RoomFourEntry;
    public static readonly Vector2 ClassroomDoorEntryStored = new(592.4f, 305.8f);
    public static readonly Vector2 MusicDoorEntryStored = MusicRoomLayout3D.Mapper.ToStored(MusicRoomLayout3D.Entry);
    public static readonly Vector2 ArchiveDoorEntryStored = ArchiveRoomLayout3D.Mapper.ToStored(ArchiveRoomLayout3D.Entry);
    public static readonly Vector2 AuditoriumDoorEntryStored = AuditoriumLayout3D.Mapper.ToStored(AuditoriumLayout3D.Entry);

    public static DreamAreaSpawnRoute Resolve(string sourceAreaId, string targetAreaId)
    {
        return (sourceAreaId, targetAreaId) switch
        {
            (DreamEscapeIds.AreaClassroom, DreamEscapeIds.AreaCorridor) =>
                MansionRoute(MansionRoomOneEntryWorld, Vector3.Right),
            (DreamEscapeIds.AreaMusic, DreamEscapeIds.AreaCorridor) =>
                MansionRoute(MansionRoomTwoEntryWorld, Vector3.Left),
            (DreamEscapeIds.AreaArchive, DreamEscapeIds.AreaCorridor) =>
                MansionRoute(MansionRoomThreeEntryWorld, Vector3.Right),
            (DreamEscapeIds.AreaAuditorium, DreamEscapeIds.AreaCorridor) =>
                MansionRoute(MansionRoomFourEntryWorld, Vector3.Forward),
            (DreamEscapeIds.AreaCorridor, DreamEscapeIds.AreaClassroom) =>
                new DreamAreaSpawnRoute(ClassroomDoorEntryStored, Vector3.Left),
            (DreamEscapeIds.AreaCorridor, DreamEscapeIds.AreaMusic) =>
                new DreamAreaSpawnRoute(MusicDoorEntryStored, Vector3.Right),
            (DreamEscapeIds.AreaCorridor, DreamEscapeIds.AreaArchive) =>
                new DreamAreaSpawnRoute(ArchiveDoorEntryStored, Vector3.Left),
            (DreamEscapeIds.AreaCorridor, DreamEscapeIds.AreaAuditorium) =>
                new DreamAreaSpawnRoute(AuditoriumDoorEntryStored, Vector3.Forward),
            _ => throw new ArgumentOutOfRangeException(
                nameof(targetAreaId),
                $"Unsupported dream area transition '{sourceAreaId}' -> '{targetAreaId}'.")
        };
    }

    public static bool TryGetExitForward(
        string sourceAreaId,
        string targetAreaId,
        out Vector3 exitForward)
    {
        exitForward = (sourceAreaId, targetAreaId) switch
        {
            (DreamEscapeIds.AreaClassroom, DreamEscapeIds.AreaCorridor) => Vector3.Right,
            (DreamEscapeIds.AreaCorridor, DreamEscapeIds.AreaClassroom) => Vector3.Left,
            (DreamEscapeIds.AreaMusic, DreamEscapeIds.AreaCorridor) => Vector3.Left,
            (DreamEscapeIds.AreaCorridor, DreamEscapeIds.AreaMusic) => Vector3.Right,
            (DreamEscapeIds.AreaArchive, DreamEscapeIds.AreaCorridor) => Vector3.Right,
            (DreamEscapeIds.AreaCorridor, DreamEscapeIds.AreaArchive) => Vector3.Left,
            (DreamEscapeIds.AreaAuditorium, DreamEscapeIds.AreaCorridor) => Vector3.Back,
            (DreamEscapeIds.AreaCorridor, DreamEscapeIds.AreaAuditorium) => Vector3.Back,
            _ => Vector3.Zero
        };
        return exitForward != Vector3.Zero;
    }

    private static DreamAreaSpawnRoute MansionRoute(Vector3 worldPosition, Vector3 facing) =>
        new(MansionMapper.ToStored(worldPosition), facing);

}
