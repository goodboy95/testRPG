namespace TownRpg.Core.Domain;

public enum DreamEscapeStatus
{
    InProgress,
    ExitOpened,
    EscapePendingWake,
    PinPendingWake,
    CompletedEscape,
    CompletedPin
}

public sealed class DreamEscapeState
{
    public DreamEscapeStatus Status { get; set; } = DreamEscapeStatus.InProgress;
    public string CurrentAreaId { get; set; } = DreamEscapeIds.AreaClassroom;
    public float PositionX { get; set; } = 320f;
    public float PositionY { get; set; } = 280f;
    public HashSet<string> Items { get; set; } = new(StringComparer.Ordinal)
    {
        DreamEscapeIds.ItemPin
    };
    public HashSet<string> SolvedPuzzles { get; set; } = new(StringComparer.Ordinal);
    public HashSet<string> UnlockedAreas { get; set; } = new(StringComparer.Ordinal)
    {
        DreamEscapeIds.AreaCorridor,
        DreamEscapeIds.AreaClassroom
    };
    public HashSet<string> DiscoveredClues { get; set; } = new(StringComparer.Ordinal);
    public Dictionary<string, int> HintLevels { get; set; } = new(StringComparer.Ordinal);
    public bool XiaoyuJoined { get; set; }
}

public static class DreamEscapeIds
{
    public const string Location = "dream_school";

    public const string AreaCorridor = "dream.corridor";
    public const string AreaClassroom = "dream.classroom";
    public const string AreaMusic = "dream.music";
    public const string AreaArchive = "dream.archive";
    public const string AreaAuditorium = "dream.auditorium";

    public const string ItemPin = "dream.pin";
    public const string ItemChalk = "dream.chalk";
    public const string ItemRoomTwoKey = "dream.room_two_key";
    public const string ItemMaintenanceKey = "dream.maintenance_key";
    public const string ItemRedFilter = "dream.red_filter";
    public const string ItemClassPhoto = "dream.class_photo";
    public const string ItemTapeReel = "dream.tape_reel";
    public const string ItemRecorderCrank = "dream.recorder_crank";

    public const string PuzzleBlackboard = "dream.classroom.blackboard";
    public const string PuzzleTeacherDesk = "dream.classroom.teacher_desk";
    public const string PuzzleRoomTwoDoor = "dream.corridor.room_two_door";
    public const string PuzzleMusicScore = "dream.music.score";
    public const string PuzzleMusicCage = "dream.music.cage";
    public const string PuzzleListening = "dream.music.listening";
    public const string PuzzleArchiveOrder = "dream.archive.club_order";
    public const string PuzzleMapCabinet = "dream.classroom.map_cabinet";
    public const string PuzzlePhotoFilter = "dream.classroom.photo_filter";
    public const string PuzzleAttendance = "dream.music.attendance";
    public const string PuzzleRecorderTape = "dream.archive.recorder_tape";
    public const string PuzzleRecorderCrank = "dream.archive.recorder_crank";
    public const string PuzzleRoomFourDoor = "dream.corridor.room_four_door";
    public const string PuzzleFinalDesk = "dream.auditorium.desk_panel";
    public const string PuzzleFinalInstruments = "dream.auditorium.instrument_panel";
    public const string PuzzleFinalBroadcast = "dream.auditorium.broadcast_panel";
    public const string PuzzleLeftLever = "dream.auditorium.left_lever";
    public const string PuzzleExit = "dream.auditorium.exit";

    public const string ClueDeskSources = "dream.clue.desk_sources";
    public const string ClueClassroomWindow = "dream.clue.classroom_window";
    public const string ClueClassroomClock = "dream.clue.classroom_clock";
    public const string ClueClassroomDutyRoster = "dream.clue.classroom_duty_roster";
    public const string ClueCageSequence = "dream.clue.cage_sequence";
    public const string ClueListenWindow = "dream.clue.listen_window";
    public const string ClueListenLectern = "dream.clue.listen_lectern";
    public const string ClueListenDoor = "dream.clue.listen_door";
    public const string ClueArchiveRules = "dream.clue.archive_rules";
    public const string ClueMapTool = "dream.clue.map_tool";
    public const string ClueSeatCoordinates = "dream.clue.seat_coordinates";
    public const string ClueBroadcastSequence = "dream.clue.broadcast_sequence";

    public static readonly IReadOnlySet<string> Areas = new HashSet<string>(StringComparer.Ordinal)
    {
        AreaCorridor, AreaClassroom, AreaMusic, AreaArchive, AreaAuditorium
    };

    public static readonly IReadOnlySet<string> Items = new HashSet<string>(StringComparer.Ordinal)
    {
        ItemPin, ItemChalk, ItemRoomTwoKey, ItemMaintenanceKey,
        ItemRedFilter, ItemClassPhoto, ItemTapeReel, ItemRecorderCrank
    };

    public static readonly IReadOnlySet<string> Puzzles = new HashSet<string>(StringComparer.Ordinal)
    {
        PuzzleBlackboard, PuzzleTeacherDesk, PuzzleRoomTwoDoor, PuzzleMusicScore,
        PuzzleMusicCage, PuzzleListening, PuzzleArchiveOrder, PuzzleMapCabinet,
        PuzzlePhotoFilter, PuzzleAttendance, PuzzleRecorderTape, PuzzleRecorderCrank,
        PuzzleRoomFourDoor, PuzzleFinalDesk, PuzzleFinalInstruments,
        PuzzleFinalBroadcast, PuzzleLeftLever, PuzzleExit
    };

    public static readonly IReadOnlySet<string> Clues = new HashSet<string>(StringComparer.Ordinal)
    {
        ClueDeskSources, ClueClassroomWindow, ClueClassroomClock,
        ClueClassroomDutyRoster, ClueCageSequence, ClueListenWindow,
        ClueListenLectern, ClueListenDoor, ClueArchiveRules, ClueMapTool,
        ClueSeatCoordinates, ClueBroadcastSequence
    };
}
