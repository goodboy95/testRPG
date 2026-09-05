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
    public const int CurrentContentVersion = 5;
    public const float NewGameClassroomPositionX = 448f;
    public const float NewGameClassroomPositionY = 214f;

    public int ContentVersion { get; set; }
    public DreamEscapeStatus Status { get; set; } = DreamEscapeStatus.InProgress;
    public string CurrentAreaId { get; set; } = DreamEscapeIds.AreaClassroom;
    public float PositionX { get; set; } = 320f;
    public float PositionY { get; set; } = 280f;
    public HashSet<string> Items { get; set; } = new(StringComparer.Ordinal) { DreamEscapeIds.ItemPin };
    public HashSet<string> SolvedPuzzles { get; set; } = new(StringComparer.Ordinal);
    public HashSet<string> UnlockedAreas { get; set; } = new(StringComparer.Ordinal)
    {
        DreamEscapeIds.AreaCorridor,
        DreamEscapeIds.AreaClassroom
    };
    public HashSet<string> DiscoveredClues { get; set; } = new(StringComparer.Ordinal);
    public HashSet<string> DiscoveredPuzzles { get; set; } = new(StringComparer.Ordinal);
    public HashSet<string> InstalledParts { get; set; } = new(StringComparer.Ordinal);
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
    public const string ItemClockHand = "dream.clock_hand";
    public const string ItemClockKey = "dream.clock_key";
    public const string ItemRosterSlider = "dream.roster_slider";
    public const string ItemRoomTwoKey = "dream.room_two_key";
    public const string ItemClassPhotoNegative = "dream.class_photo_negative";
    public const string ItemAttendanceOverlay = "dream.attendance_overlay";
    public const string ItemTapeFragmentClassroom = "dream.tape_fragment_classroom";
    public const string ItemRubberMallet = "dream.rubber_mallet";
    public const string ItemListeningCone = "dream.listening_cone";
    public const string ItemMetronomeWeight = "dream.metronome_weight";
    public const string ItemTornPianoRoll = "dream.torn_piano_roll";
    public const string ItemWaxPatch = "dream.wax_patch";
    public const string ItemRoomThreeKey = "dream.room_three_key";
    public const string ItemCalibrationReel = "dream.calibration_reel";
    public const string ItemRecorderCrank = "dream.recorder_crank";
    public const string ItemTapeFragmentMusic = "dream.tape_fragment_music";
    public const string ItemMaintenanceKey = "dream.maintenance_key";
    public const string ItemRedFilter = "dream.red_filter";
    public const string ItemArchiveIndexStrip = "dream.archive_index_strip";
    public const string ItemFuseTube = "dream.fuse_tube";
    public const string ItemCopperWire = "dream.copper_wire";
    public const string ItemRepairedFuse = "dream.repaired_fuse";
    public const string ItemSplicingTape = "dream.splicing_tape";
    public const string ItemRoomFourKey = "dream.room_four_key";
    public const string ItemTapeFragmentArchive = "dream.tape_fragment_archive";
    public const string ItemStageHook = "dream.stage_hook";
    public const string ItemBenchClampKey = "dream.bench_clamp_key";
    public const string ItemInspectionMirror = "dream.inspection_mirror";
    public const string ItemCurtainCounterweight = "dream.curtain_counterweight";
    public const string ItemProjectorLens = "dream.projector_lens";
    public const string ItemMasterReelCore = "dream.master_reel_core";
    public const string ItemRestoredMasterTape = "dream.restored_master_tape";

    public const string PuzzleWindowLatches = "dream.classroom.window_latches";
    public const string PuzzleBlackboard = "dream.classroom.blackboard";
    public const string PuzzleClockRestore = "dream.classroom.clock_restore";
    public const string PuzzleRosterSlider = "dream.classroom.roster_slider";
    public const string PuzzleTeacherDesk = "dream.classroom.teacher_desk";
    public const string PuzzleMapCabinet = "dream.classroom.map_cabinet";
    public const string PuzzleTransomCache = "dream.classroom.transom_cache";
    public const string PuzzleMusicScore = "dream.music.score";
    public const string PuzzleMusicCage = "dream.music.cage";
    public const string PuzzleMusicMetronome = "dream.music.metronome";
    public const string PuzzleMusicTuningForks = "dream.music.tuning_forks";
    public const string PuzzleMusicPianoRoll = "dream.music.piano_roll";
    public const string PuzzleMusicAcousticWall = "dream.music.acoustic_wall";
    public const string PuzzleMusicAttendanceReader = "dream.music.attendance_reader";
    public const string PuzzleMusicPianoBenchCache = "dream.music.piano_bench_cache";
    public const string PuzzleArchiveSignalRegistry = "dream.archive.signal_registry";
    public const string PuzzleArchiveDrawerRoute = "dream.archive.drawer_route";
    public const string PuzzleArchiveFuseRepair = "dream.archive.fuse_repair";
    public const string PuzzleArchiveRecorderRebuild = "dream.archive.recorder_rebuild";
    public const string PuzzleArchiveEditingCounter = "dream.archive.editing_counter";
    public const string PuzzleArchiveUpperVault = "dream.archive.upper_vault";
    public const string PuzzleAuditoriumLineSetBalance = "dream.auditorium.line_set_balance";
    public const string PuzzleAuditoriumShadowProjector = "dream.auditorium.shadow_projector";
    public const string PuzzleAuditoriumMasterTapeSplice = "dream.auditorium.master_tape_splice";
    public const string PuzzleAuditoriumLightConsole = "dream.auditorium.light_console";
    public const string PuzzleAuditoriumSoundConsole = "dream.auditorium.sound_console";
    public const string PuzzleAuditoriumBroadcastConsole = "dream.auditorium.broadcast_console";
    public const string PuzzleExit = "dream.auditorium.exit";

    public const string ClueDeskSources = "dream.clue.desk_sources";
    public const string ClueClassroomWindow = "dream.clue.classroom_window";
    public const string ClueClassroomClock = "dream.clue.classroom_clock";
    public const string ClueClassroomDutyRoster = "dream.clue.classroom_duty_roster";
    public const string ClueCageSequence = "dream.clue.cage_sequence";
    public const string ClueMusicTempoNotice = "dream.clue.music_tempo_notice";
    public const string ClueMusicForkReference = "dream.clue.music_fork_reference";
    public const string ClueMusicRollTemplate = "dream.clue.music_roll_template";
    public const string ClueMusicBeatGlyphs = "dream.clue.music_beat_glyphs";
    public const string ClueMusicGlyphPitchMap = "dream.clue.music_glyph_pitch_map";
    public const string ClueMusicPitchInstrumentMap = "dream.clue.music_pitch_instrument_map";
    public const string ClueMusicAcousticSequence = "dream.clue.music_acoustic_sequence";
    public const string ClueArchiveRegistryRule = "dream.clue.archive_registry_rule";
    public const string ClueArchiveRegistryChoir = "dream.clue.archive_registry_choir";
    public const string ClueArchiveRegistryBroadcast = "dream.clue.archive_registry_broadcast";
    public const string ClueArchiveRegistryArt = "dream.clue.archive_registry_art";
    public const string ClueArchiveRegistryTrack = "dream.clue.archive_registry_track";
    public const string ClueArchiveDrawerRoute = "dream.clue.archive_drawer_route";
    public const string ClueArchiveFuseRoute = "dream.clue.archive_fuse_route";
    public const string ClueArchiveEditingStops = "dream.clue.archive_editing_stops";
    public const string ClueMapTool = "dream.clue.map_tool";
    public const string ClueSeatCoordinates = "dream.clue.seat_coordinates";
    public const string ClueBroadcastSequence = "dream.clue.broadcast_sequence";
    public const string ClueAuditoriumLineSetNotches = "dream.clue.auditorium_line_set_notches";
    public const string ClueAuditoriumProjectionOrder = "dream.clue.auditorium_projection_order";
    public const string ClueAuditoriumTapeOrder = "dream.clue.auditorium_tape_order";

    public const string SlotClassroomClockHand = "dream.slot.classroom.clock_hand";
    public const string SlotClassroomClockKey = "dream.slot.classroom.clock_key";
    public const string SlotClassroomRosterSlider = "dream.slot.classroom.roster_slider";
    public const string SlotMusicMetronomeWeight = "dream.slot.music.metronome_weight";
    public const string SlotMusicPianoRoll = "dream.slot.music.piano_roll";
    public const string SlotArchiveRecorderReel = "dream.slot.archive.recorder_reel";
    public const string SlotArchiveRecorderCrank = "dream.slot.archive.recorder_crank";
    public const string SlotArchiveRecorderFuse = "dream.slot.archive.recorder_fuse";
    public const string SlotAuditoriumCurtainCounterweight = "dream.slot.auditorium.curtain_counterweight";
    public const string SlotAuditoriumProjectorLens = "dream.slot.auditorium.projector_lens";
    public const string SlotAuditoriumMasterTape = "dream.slot.auditorium.master_tape";

    public const string PickupClockHand = "dream.pickup.clock_hand";
    public const string PickupRubberMallet = "dream.pickup.rubber_mallet";
    public const string PickupListeningCone = "dream.pickup.listening_cone";
    public const string PickupMetronomeWeight = "dream.pickup.metronome_weight";
    public const string PickupTornPianoRoll = "dream.pickup.torn_piano_roll";
    public const string PickupWaxPatch = "dream.pickup.wax_patch";
    public const string PickupArchiveIndexStrip = "dream.pickup.archive_index_strip";
    public const string PickupCurtainCounterweight = "dream.pickup.curtain_counterweight";
    public const string PickupProjectorLens = "dream.pickup.projector_lens";

    public static readonly IReadOnlySet<string> Areas = Set(AreaCorridor, AreaClassroom, AreaMusic, AreaArchive, AreaAuditorium);

    public static readonly IReadOnlySet<string> Items = Set(
        ItemPin, ItemChalk, ItemClockHand, ItemClockKey, ItemRosterSlider, ItemRoomTwoKey,
        ItemClassPhotoNegative, ItemAttendanceOverlay, ItemTapeFragmentClassroom,
        ItemRubberMallet, ItemListeningCone, ItemMetronomeWeight, ItemTornPianoRoll, ItemWaxPatch,
        ItemRoomThreeKey, ItemCalibrationReel, ItemRecorderCrank, ItemTapeFragmentMusic,
        ItemMaintenanceKey, ItemRedFilter, ItemArchiveIndexStrip, ItemFuseTube, ItemCopperWire,
        ItemRepairedFuse, ItemSplicingTape, ItemRoomFourKey, ItemTapeFragmentArchive,
        ItemStageHook, ItemBenchClampKey, ItemInspectionMirror, ItemCurtainCounterweight,
        ItemProjectorLens, ItemMasterReelCore, ItemRestoredMasterTape);

    public static readonly IReadOnlySet<string> Puzzles = Set(
        PuzzleWindowLatches, PuzzleBlackboard, PuzzleClockRestore, PuzzleRosterSlider,
        PuzzleTeacherDesk, PuzzleMapCabinet, PuzzleTransomCache,
        PuzzleMusicScore, PuzzleMusicCage, PuzzleMusicMetronome, PuzzleMusicTuningForks,
        PuzzleMusicPianoRoll, PuzzleMusicAcousticWall, PuzzleMusicAttendanceReader,
        PuzzleMusicPianoBenchCache,
        PuzzleArchiveSignalRegistry, PuzzleArchiveDrawerRoute, PuzzleArchiveFuseRepair,
        PuzzleArchiveRecorderRebuild, PuzzleArchiveEditingCounter, PuzzleArchiveUpperVault,
        PuzzleAuditoriumLineSetBalance, PuzzleAuditoriumShadowProjector,
        PuzzleAuditoriumMasterTapeSplice, PuzzleAuditoriumLightConsole,
        PuzzleAuditoriumSoundConsole, PuzzleAuditoriumBroadcastConsole, PuzzleExit);

    public static readonly IReadOnlySet<string> Clues = Set(
        ClueDeskSources, ClueClassroomWindow, ClueClassroomClock, ClueClassroomDutyRoster,
        ClueCageSequence, ClueMusicTempoNotice, ClueMusicForkReference, ClueMusicRollTemplate,
        ClueMusicBeatGlyphs, ClueMusicGlyphPitchMap, ClueMusicPitchInstrumentMap,
        ClueMusicAcousticSequence, ClueArchiveRegistryRule, ClueArchiveRegistryChoir,
        ClueArchiveRegistryBroadcast, ClueArchiveRegistryArt, ClueArchiveRegistryTrack,
        ClueArchiveDrawerRoute, ClueArchiveFuseRoute, ClueArchiveEditingStops,
        ClueMapTool, ClueSeatCoordinates, ClueBroadcastSequence,
        ClueAuditoriumLineSetNotches, ClueAuditoriumProjectionOrder, ClueAuditoriumTapeOrder);

    public static readonly IReadOnlySet<string> InstalledPartSlots = Set(
        SlotClassroomClockHand, SlotClassroomClockKey, SlotClassroomRosterSlider,
        SlotMusicMetronomeWeight, SlotMusicPianoRoll,
        SlotArchiveRecorderReel, SlotArchiveRecorderCrank, SlotArchiveRecorderFuse,
        SlotAuditoriumCurtainCounterweight, SlotAuditoriumProjectorLens, SlotAuditoriumMasterTape);

    public static readonly IReadOnlySet<string> PickupProgress = Set(
        PickupClockHand, PickupRubberMallet, PickupListeningCone, PickupMetronomeWeight,
        PickupTornPianoRoll, PickupWaxPatch, PickupArchiveIndexStrip,
        PickupCurtainCounterweight, PickupProjectorLens);

    public static readonly IReadOnlyDictionary<string, IReadOnlyList<string>> PuzzleInputClues =
        new Dictionary<string, IReadOnlyList<string>>(StringComparer.Ordinal)
        {
            [PuzzleWindowLatches] = [ClueClassroomWindow],
            [PuzzleClockRestore] = [ClueClassroomClock],
            [PuzzleRosterSlider] = [ClueClassroomDutyRoster],
            [PuzzleTeacherDesk] = [ClueDeskSources, ClueClassroomWindow, ClueClassroomClock, ClueClassroomDutyRoster],
            [PuzzleMusicCage] = [ClueCageSequence],
            [PuzzleMusicMetronome] = [ClueMusicTempoNotice],
            [PuzzleMusicTuningForks] = [ClueMusicForkReference],
            [PuzzleMusicPianoRoll] = [ClueMusicRollTemplate],
            [PuzzleMusicAcousticWall] = [ClueMusicBeatGlyphs, ClueMusicGlyphPitchMap, ClueMusicPitchInstrumentMap],
            [PuzzleArchiveSignalRegistry] = [ClueArchiveRegistryRule, ClueArchiveRegistryChoir,
                ClueArchiveRegistryBroadcast, ClueArchiveRegistryArt, ClueArchiveRegistryTrack],
            [PuzzleArchiveDrawerRoute] = [ClueArchiveDrawerRoute],
            [PuzzleArchiveFuseRepair] = [ClueArchiveFuseRoute],
            [PuzzleArchiveEditingCounter] = [ClueArchiveEditingStops],
            [PuzzleAuditoriumLineSetBalance] = [ClueAuditoriumLineSetNotches],
            [PuzzleAuditoriumShadowProjector] = [ClueAuditoriumProjectionOrder],
            [PuzzleAuditoriumMasterTapeSplice] = [ClueAuditoriumTapeOrder],
            [PuzzleAuditoriumLightConsole] = [ClueDeskSources],
            [PuzzleAuditoriumSoundConsole] = [ClueMusicAcousticSequence],
            [PuzzleAuditoriumBroadcastConsole] = [ClueBroadcastSequence]
        };

    public static readonly IReadOnlyDictionary<string, IReadOnlyList<string>> TargetItemUses =
        new Dictionary<string, IReadOnlyList<string>>(StringComparer.Ordinal)
        {
            ["target.classroom.blackboard"] = [ItemChalk],
            ["target.classroom.clock"] = [ItemClockHand, ItemClockKey],
            ["target.classroom.duty_roster"] = [ItemRosterSlider],
            ["target.classroom.map_cabinet"] = [ItemMaintenanceKey],
            ["target.classroom.transom_cache"] = [ItemStageHook],
            ["target.corridor.room_two_door"] = [ItemRoomTwoKey],
            ["target.corridor.room_three_door"] = [ItemRoomThreeKey],
            ["target.corridor.room_four_door"] = [ItemRoomFourKey],
            ["target.music.score"] = [ItemChalk],
            ["target.music.metronome"] = [ItemMetronomeWeight],
            ["target.music.piano_bench"] = [ItemBenchClampKey],
            ["target.archive.recorder"] = [ItemCalibrationReel, ItemRecorderCrank, ItemRepairedFuse],
            ["target.archive.upper_vault"] = [ItemInspectionMirror],
            ["target.auditorium.line_set"] = [ItemCurtainCounterweight],
            ["target.auditorium.projector"] = [ItemProjectorLens],
            ["target.auditorium.master_player"] = [ItemRestoredMasterTape]
        };

    public static readonly IReadOnlyDictionary<string, IReadOnlyList<string>> PuzzlePrerequisites =
        new Dictionary<string, IReadOnlyList<string>>(StringComparer.Ordinal)
        {
            [PuzzleBlackboard] = [PuzzleWindowLatches],
            [PuzzleTeacherDesk] = [PuzzleBlackboard, PuzzleClockRestore, PuzzleRosterSlider],
            [PuzzleMapCabinet] = [PuzzleArchiveSignalRegistry],
            [PuzzleTransomCache] = [PuzzleAuditoriumShadowProjector],
            [PuzzleMusicCage] = [PuzzleMusicScore],
            [PuzzleMusicMetronome] = [PuzzleMusicCage],
            [PuzzleMusicTuningForks] = [PuzzleMusicCage],
            [PuzzleMusicPianoRoll] = [PuzzleMusicCage],
            [PuzzleMusicAcousticWall] = [PuzzleMusicMetronome, PuzzleMusicTuningForks, PuzzleMusicPianoRoll],
            [PuzzleMusicAttendanceReader] = [PuzzleMapCabinet, PuzzleArchiveSignalRegistry],
            [PuzzleMusicPianoBenchCache] = [PuzzleAuditoriumShadowProjector],
            [PuzzleArchiveFuseRepair] = [PuzzleArchiveSignalRegistry, PuzzleArchiveDrawerRoute],
            [PuzzleArchiveRecorderRebuild] = [PuzzleArchiveFuseRepair, PuzzleMusicAttendanceReader],
            [PuzzleArchiveEditingCounter] = [PuzzleArchiveRecorderRebuild],
            [PuzzleArchiveUpperVault] = [PuzzleAuditoriumShadowProjector],
            [PuzzleAuditoriumLineSetBalance] = [PuzzleArchiveEditingCounter],
            [PuzzleAuditoriumShadowProjector] = [PuzzleAuditoriumLineSetBalance],
            [PuzzleAuditoriumMasterTapeSplice] = [PuzzleTransomCache, PuzzleMusicPianoBenchCache, PuzzleArchiveUpperVault],
            [PuzzleAuditoriumLightConsole] = [PuzzleAuditoriumMasterTapeSplice],
            [PuzzleAuditoriumSoundConsole] = [PuzzleAuditoriumMasterTapeSplice],
            [PuzzleAuditoriumBroadcastConsole] = [PuzzleAuditoriumMasterTapeSplice],
            [PuzzleExit] = [PuzzleAuditoriumLightConsole, PuzzleAuditoriumSoundConsole,
                PuzzleAuditoriumBroadcastConsole]
        };

    private static IReadOnlySet<string> Set(params string[] values) =>
        new HashSet<string>(values, StringComparer.Ordinal);
}

public static class RetiredDreamEscapeIds
{
    public const string ItemClassPhoto = "dream.class_photo";
    public const string ItemTapeReel = "dream.tape_reel";
    public const string PuzzlePhotoFilter = "dream.classroom.photo_filter";
    public const string PickupChalk = "dream.pickup.chalk";
    public const string PuzzleMusicListening = "dream.music.listening";
    public const string PuzzleMusicAttendance = "dream.music.attendance";
    public const string PuzzleRecorderTape = "dream.archive.recorder_tape";
    public const string PuzzleRecorderCrank = "dream.archive.recorder_crank";
    public const string PuzzleRoomTwoDoor = "dream.corridor.room_two_door";
    public const string PuzzleRoomFourDoor = "dream.corridor.room_four_door";
    public const string PuzzleFinalDesk = "dream.auditorium.desk_panel";
    public const string PuzzleFinalInstruments = "dream.auditorium.instrument_panel";
    public const string PuzzleFinalBroadcast = "dream.auditorium.broadcast_panel";
    public const string PuzzleLeftLever = "dream.auditorium.left_lever";
    public const string PuzzleArchiveClubOrder = "dream.archive.club_order";
    public const string PuzzleAuditoriumCurtainFly = "dream.auditorium.curtain_fly";

    public const string ClueArchiveRules = "dream.clue.archive_rules";
    public const string ClueArchiveScheduleChoir = "dream.clue.archive_schedule_choir";
    public const string ClueArchiveScheduleBroadcast = "dream.clue.archive_schedule_broadcast";
    public const string ClueArchiveScheduleArt = "dream.clue.archive_schedule_art";
    public const string ClueArchiveScheduleTrack = "dream.clue.archive_schedule_track";
    public const string ClueAuditoriumRiggingOrder = "dream.clue.auditorium_rigging_order";

    public const string TargetArchiveRules = "target.archive.rules";
    public const string TargetArchiveScheduleChoir = "target.archive.schedule_choir";
    public const string TargetArchiveScheduleBroadcast = "target.archive.schedule_broadcast";
    public const string TargetArchiveScheduleArt = "target.archive.schedule_art";
    public const string TargetArchiveScheduleTrack = "target.archive.schedule_track";
    public const string TargetAuditoriumRiggingOrder = "target.auditorium.rigging_order";

    public static readonly IReadOnlySet<string> Puzzles = new HashSet<string>(StringComparer.Ordinal)
    {
        PuzzlePhotoFilter, PuzzleMusicListening, PuzzleMusicAttendance, PuzzleRecorderTape,
        PuzzleRecorderCrank, PuzzleRoomTwoDoor, PuzzleRoomFourDoor, PuzzleFinalDesk,
        PuzzleFinalInstruments, PuzzleFinalBroadcast, PuzzleLeftLever,
        PuzzleArchiveClubOrder, PuzzleAuditoriumCurtainFly
    };

    public static readonly IReadOnlySet<string> Clues = new HashSet<string>(StringComparer.Ordinal)
    {
        ClueArchiveRules, ClueArchiveScheduleChoir, ClueArchiveScheduleBroadcast,
        ClueArchiveScheduleArt, ClueArchiveScheduleTrack, ClueAuditoriumRiggingOrder
    };

    public static readonly IReadOnlySet<string> Targets = new HashSet<string>(StringComparer.Ordinal)
    {
        TargetArchiveRules, TargetArchiveScheduleChoir, TargetArchiveScheduleBroadcast,
        TargetArchiveScheduleArt, TargetArchiveScheduleTrack, TargetAuditoriumRiggingOrder
    };
}
