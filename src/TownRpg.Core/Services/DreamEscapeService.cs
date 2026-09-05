using TownRpg.Core.Domain;

namespace TownRpg.Core.Services;

public sealed record DreamActionResult(bool Changed, string FeedbackKey)
{
    public static DreamActionResult Success(string feedbackKey) => new(true, feedbackKey);
    public static DreamActionResult NoChange(string feedbackKey) => new(false, feedbackKey);
}

public sealed class DreamEscapeService(GameState game)
{
    private DreamEscapeState State => game.DreamEscape
        ?? throw new InvalidOperationException("The current game has no dream escape state.");

    public DreamActionResult MoveToArea(string areaId, float positionX, float positionY)
    {
        RequireInProgressOrExitOpened();
        if (!DreamEscapeIds.Areas.Contains(areaId) || !State.UnlockedAreas.Contains(areaId))
            return DreamActionResult.NoChange("DREAM_FEEDBACK_AREA_LOCKED");
        if (State.CurrentAreaId == areaId &&
            Math.Abs(State.PositionX - positionX) < 0.001f &&
            Math.Abs(State.PositionY - positionY) < 0.001f)
            return DreamActionResult.NoChange("DREAM_FEEDBACK_ALREADY_THERE");

        State.CurrentAreaId = areaId;
        State.PositionX = positionX;
        State.PositionY = positionY;
        return DreamActionResult.Success("DREAM_FEEDBACK_AREA_CHANGED");
    }

    public DreamActionResult Pickup(string itemId)
    {
        RequireInProgress();
        if (!DreamEscapeIds.Items.Contains(itemId))
            throw new ArgumentOutOfRangeException(nameof(itemId), itemId, "Unknown dream item.");

        var pickup = itemId switch
        {
            DreamEscapeIds.ItemClockHand => (DreamEscapeIds.AreaClassroom, DreamEscapeIds.PickupClockHand),
            DreamEscapeIds.ItemRubberMallet => (DreamEscapeIds.AreaMusic, DreamEscapeIds.PickupRubberMallet),
            DreamEscapeIds.ItemListeningCone => (DreamEscapeIds.AreaMusic, DreamEscapeIds.PickupListeningCone),
            DreamEscapeIds.ItemMetronomeWeight => (DreamEscapeIds.AreaMusic, DreamEscapeIds.PickupMetronomeWeight),
            DreamEscapeIds.ItemTornPianoRoll => (DreamEscapeIds.AreaMusic, DreamEscapeIds.PickupTornPianoRoll),
            DreamEscapeIds.ItemWaxPatch => (DreamEscapeIds.AreaMusic, DreamEscapeIds.PickupWaxPatch),
            DreamEscapeIds.ItemArchiveIndexStrip => (DreamEscapeIds.AreaArchive, DreamEscapeIds.PickupArchiveIndexStrip),
            DreamEscapeIds.ItemCurtainCounterweight =>
                (DreamEscapeIds.AreaAuditorium, DreamEscapeIds.PickupCurtainCounterweight),
            DreamEscapeIds.ItemProjectorLens =>
                (DreamEscapeIds.AreaAuditorium, DreamEscapeIds.PickupProjectorLens),
            _ => (string.Empty, string.Empty)
        };
        if (string.IsNullOrEmpty(pickup.Item1) || State.CurrentAreaId != pickup.Item1)
            return DreamActionResult.NoChange("DREAM_FEEDBACK_ITEM_NOT_AVAILABLE");
        if (pickup.Item1 == DreamEscapeIds.AreaMusic && !State.XiaoyuJoined)
            return DreamActionResult.NoChange("DREAM_FEEDBACK_NEED_XIAOYU");
        if (State.Items.Contains(itemId) || State.SolvedPuzzles.Contains(pickup.Item2))
            return DreamActionResult.NoChange("DREAM_FEEDBACK_ALREADY_HAVE_ITEM");

        State.Items.Add(itemId);
        State.SolvedPuzzles.Add(pickup.Item2);
        if (itemId == DreamEscapeIds.ItemArchiveIndexStrip)
            State.DiscoveredClues.Add(DreamEscapeIds.ClueArchiveDrawerRoute);
        return DreamActionResult.Success("DREAM_FEEDBACK_ITEM_PICKED_UP");
    }

    public DreamActionResult DiscoverPuzzle(string puzzleId)
    {
        RequireInProgress();
        if (!DreamEscapeIds.Puzzles.Contains(puzzleId))
            throw new ArgumentOutOfRangeException(nameof(puzzleId), puzzleId, "Unknown dream puzzle.");
        return State.DiscoveredPuzzles.Add(puzzleId)
            ? DreamActionResult.Success("DREAM_FEEDBACK_PUZZLE_DISCOVERED")
            : DreamActionResult.NoChange("DREAM_FEEDBACK_PUZZLE_ALREADY_DISCOVERED");
    }

    public IReadOnlyList<string> MissingInputClues(string puzzleId)
    {
        if (!DreamEscapeIds.Puzzles.Contains(puzzleId))
            throw new ArgumentOutOfRangeException(nameof(puzzleId), puzzleId, "Unknown dream puzzle.");
        return DreamEscapeIds.PuzzleInputClues.GetValueOrDefault(puzzleId, [])
            .Where(clue => !State.DiscoveredClues.Contains(clue))
            .ToArray();
    }

    public DreamActionResult UseItem(string itemId, string targetId)
    {
        RequireInProgress();
        if (!State.Items.Contains(itemId))
            return DreamActionResult.NoChange("DREAM_FEEDBACK_ITEM_MISSING");
        var targetArea = TargetArea(targetId);
        if (targetArea is not null && State.CurrentAreaId != targetArea)
            return DreamActionResult.NoChange("DREAM_FEEDBACK_TARGET_NOT_HERE");

        return (itemId, targetId) switch
        {
            (DreamEscapeIds.ItemChalk, "target.classroom.blackboard") => RevealBlackboard(),
            (DreamEscapeIds.ItemClockHand, "target.classroom.clock") =>
                InstallPart(itemId, DreamEscapeIds.SlotClassroomClockHand, "DREAM_FEEDBACK_CLOCK_HAND_INSTALLED"),
            (DreamEscapeIds.ItemClockKey, "target.classroom.clock") =>
                InstallPart(itemId, DreamEscapeIds.SlotClassroomClockKey, "DREAM_FEEDBACK_CLOCK_KEY_INSTALLED"),
            (DreamEscapeIds.ItemRosterSlider, "target.classroom.duty_roster") =>
                InstallPart(itemId, DreamEscapeIds.SlotClassroomRosterSlider,
                    "DREAM_FEEDBACK_ROSTER_SLIDER_INSTALLED"),
            (DreamEscapeIds.ItemRoomTwoKey, "target.corridor.room_two_door") =>
                UnlockAreaWithKey(itemId, DreamEscapeIds.AreaMusic, DreamEscapeIds.PuzzleTeacherDesk,
                    "DREAM_FEEDBACK_ROOM_TWO_OPEN"),
            (DreamEscapeIds.ItemChalk, "target.music.score") => RevealMusicScore(),
            (DreamEscapeIds.ItemMetronomeWeight, "target.music.metronome") =>
                InstallPart(itemId, DreamEscapeIds.SlotMusicMetronomeWeight,
                    "DREAM_FEEDBACK_METRONOME_WEIGHT_INSTALLED"),
            (DreamEscapeIds.ItemRoomThreeKey, "target.corridor.room_three_door") =>
                UnlockAreaWithKey(itemId, DreamEscapeIds.AreaArchive, DreamEscapeIds.PuzzleMusicAcousticWall,
                    "DREAM_FEEDBACK_ROOM_THREE_OPEN"),
            (DreamEscapeIds.ItemMaintenanceKey, "target.classroom.map_cabinet") => OpenMapCabinet(),
            (DreamEscapeIds.ItemStageHook, "target.classroom.transom_cache") => OpenTransomCache(),
            (DreamEscapeIds.ItemBenchClampKey, "target.music.piano_bench") => OpenPianoBenchCache(),
            (DreamEscapeIds.ItemCalibrationReel, "target.archive.recorder") =>
                InstallRecorderPart(itemId, DreamEscapeIds.SlotArchiveRecorderReel,
                    "DREAM_FEEDBACK_TAPE_INSERTED"),
            (DreamEscapeIds.ItemRecorderCrank, "target.archive.recorder") =>
                InstallRecorderPart(itemId, DreamEscapeIds.SlotArchiveRecorderCrank,
                    "DREAM_FEEDBACK_RECORDER_CRANK_INSTALLED"),
            (DreamEscapeIds.ItemRepairedFuse, "target.archive.recorder") =>
                InstallRecorderPart(itemId, DreamEscapeIds.SlotArchiveRecorderFuse,
                    "DREAM_FEEDBACK_RECORDER_FUSE_INSTALLED"),
            (DreamEscapeIds.ItemInspectionMirror, "target.archive.upper_vault") => OpenUpperVault(),
            (DreamEscapeIds.ItemRoomFourKey, "target.corridor.room_four_door") =>
                UnlockAreaWithKey(itemId, DreamEscapeIds.AreaAuditorium, DreamEscapeIds.PuzzleArchiveEditingCounter,
                    "DREAM_FEEDBACK_ROOM_FOUR_OPEN"),
            (DreamEscapeIds.ItemCurtainCounterweight, "target.auditorium.line_set") =>
                InstallPart(itemId, DreamEscapeIds.SlotAuditoriumCurtainCounterweight,
                    "DREAM_FEEDBACK_CURTAIN_WEIGHT_INSTALLED"),
            (DreamEscapeIds.ItemProjectorLens, "target.auditorium.projector") =>
                InstallPart(itemId, DreamEscapeIds.SlotAuditoriumProjectorLens,
                    "DREAM_FEEDBACK_PROJECTOR_LENS_INSTALLED"),
            (DreamEscapeIds.ItemRestoredMasterTape, "target.auditorium.master_player") =>
                InstallMasterTape(),
            (DreamEscapeIds.ItemPin, "target.self") => PreparePinWake(),
            _ => DreamActionResult.NoChange("DREAM_FEEDBACK_ITEM_DOES_NOT_FIT")
        };
    }

    public DreamActionResult UseAvailableItems(string targetId)
    {
        RequireInProgress();
        if (!DreamEscapeIds.TargetItemUses.TryGetValue(targetId, out var itemIds))
            return DreamActionResult.NoChange("DREAM_FEEDBACK_ITEM_DOES_NOT_FIT");

        var targetArea = TargetArea(targetId);
        if (targetArea is not null && State.CurrentAreaId != targetArea)
            return DreamActionResult.NoChange("DREAM_FEEDBACK_TARGET_NOT_HERE");

        var availableItems = itemIds.Where(State.Items.Contains).ToArray();
        if (availableItems.Length == 0)
            return CompleteAutomaticItemTarget(targetId,
                DreamActionResult.NoChange("DREAM_FEEDBACK_ITEM_MISSING"));

        var changedResults = new List<DreamActionResult>(availableItems.Length);
        DreamActionResult? firstFailure = null;
        foreach (var itemId in availableItems)
        {
            var result = UseItem(itemId, targetId);
            if (result.Changed)
                changedResults.Add(result);
            else
                firstFailure ??= result;
        }

        var batchResult = changedResults.Count switch
        {
            0 => firstFailure ?? DreamActionResult.NoChange("DREAM_FEEDBACK_ITEM_MISSING"),
            1 => changedResults[0],
            _ => DreamActionResult.Success("DREAM_FEEDBACK_MATCHING_ITEMS_INSTALLED")
        };
        return CompleteAutomaticItemTarget(targetId, batchResult);
    }

    private DreamActionResult CompleteAutomaticItemTarget(string targetId, DreamActionResult fallback)
    {
        if (targetId == "target.archive.recorder" &&
            !State.SolvedPuzzles.Contains(DreamEscapeIds.PuzzleArchiveRecorderRebuild) &&
            HasSlots(DreamEscapeIds.SlotArchiveRecorderReel, DreamEscapeIds.SlotArchiveRecorderCrank,
                DreamEscapeIds.SlotArchiveRecorderFuse))
        {
            var completed = SubmitRecorderRebuild(["confirm"]);
            if (completed.Changed) return completed;
        }
        return fallback;
    }

    public DreamActionResult Observe(string targetId)
    {
        RequireInProgress();
        var targetArea = TargetArea(targetId);
        if (targetArea is not null && State.CurrentAreaId != targetArea)
            return DreamActionResult.NoChange("DREAM_FEEDBACK_TARGET_NOT_HERE");

        var clueId = targetId switch
        {
            "target.classroom.maintenance_card" => DreamEscapeIds.ClueClassroomWindow,
            "target.classroom.morning_schedule" => DreamEscapeIds.ClueClassroomClock,
            "target.classroom.duty_note" => DreamEscapeIds.ClueClassroomDutyRoster,
            "target.music.tempo_notice" => DreamEscapeIds.ClueMusicTempoNotice,
            "target.music.fork_reference" => DreamEscapeIds.ClueMusicForkReference,
            "target.music.roll_template" => DreamEscapeIds.ClueMusicRollTemplate,
            "target.archive.registry_rule" => DreamEscapeIds.ClueArchiveRegistryRule,
            "target.archive.registry_choir" => DreamEscapeIds.ClueArchiveRegistryChoir,
            "target.archive.registry_broadcast" => DreamEscapeIds.ClueArchiveRegistryBroadcast,
            "target.archive.registry_art" => DreamEscapeIds.ClueArchiveRegistryArt,
            "target.archive.registry_track" => DreamEscapeIds.ClueArchiveRegistryTrack,
            "target.archive.fuse_route" => DreamEscapeIds.ClueArchiveFuseRoute,
            "target.auditorium.line_set_notches" => DreamEscapeIds.ClueAuditoriumLineSetNotches,
            "target.auditorium.projection_order" => DreamEscapeIds.ClueAuditoriumProjectionOrder,
            "target.auditorium.tape_order" => DreamEscapeIds.ClueAuditoriumTapeOrder,
            _ => throw new ArgumentOutOfRangeException(nameof(targetId), targetId, "Unknown observation target.")
        };

        if (clueId is DreamEscapeIds.ClueMusicTempoNotice or DreamEscapeIds.ClueMusicForkReference or
            DreamEscapeIds.ClueMusicRollTemplate && !State.XiaoyuJoined)
            return DreamActionResult.NoChange("DREAM_FEEDBACK_NEED_XIAOYU");
        if (clueId == DreamEscapeIds.ClueMusicForkReference &&
            !State.SolvedPuzzles.Contains(DreamEscapeIds.PuzzleMusicCage))
            return DreamActionResult.NoChange("DREAM_FEEDBACK_CAGE_CLOSED");
        if (clueId == DreamEscapeIds.ClueArchiveFuseRoute &&
            !State.SolvedPuzzles.Contains(DreamEscapeIds.PuzzleArchiveSignalRegistry))
            return DreamActionResult.NoChange("DREAM_FEEDBACK_ARCHIVE_DRAWER_CLOSED");
        if (clueId == DreamEscapeIds.ClueAuditoriumProjectionOrder &&
            !State.SolvedPuzzles.Contains(DreamEscapeIds.PuzzleAuditoriumLineSetBalance))
            return DreamActionResult.NoChange("DREAM_FEEDBACK_CURTAIN_CLOSED");
        if (clueId == DreamEscapeIds.ClueAuditoriumTapeOrder &&
            !State.SolvedPuzzles.Contains(DreamEscapeIds.PuzzleAuditoriumShadowProjector))
            return DreamActionResult.NoChange("DREAM_FEEDBACK_PROJECTOR_REWARD_CLOSED");

        return State.DiscoveredClues.Add(clueId)
            ? DreamActionResult.Success("DREAM_FEEDBACK_CLUE_RECORDED")
            : DreamActionResult.NoChange("DREAM_FEEDBACK_CLUE_ALREADY_RECORDED");
    }

    public DreamActionResult SubmitPuzzle(string puzzleId, params string[] answer)
    {
        RequireInProgress();
        if (!DreamEscapeIds.Puzzles.Contains(puzzleId))
            throw new ArgumentOutOfRangeException(nameof(puzzleId), puzzleId, "Unknown dream puzzle.");
        if (State.SolvedPuzzles.Contains(puzzleId))
            return DreamActionResult.NoChange("DREAM_FEEDBACK_PUZZLE_ALREADY_SOLVED");
        var puzzleArea = PuzzleArea(puzzleId);
        if (puzzleArea is not null && State.CurrentAreaId != puzzleArea)
            return DreamActionResult.NoChange("DREAM_FEEDBACK_TARGET_NOT_HERE");

        return puzzleId switch
        {
            DreamEscapeIds.PuzzleWindowLatches => SubmitWindowLatches(answer),
            DreamEscapeIds.PuzzleClockRestore => SubmitClockRestore(answer),
            DreamEscapeIds.PuzzleRosterSlider => SubmitRosterSlider(answer),
            DreamEscapeIds.PuzzleTeacherDesk => SubmitTeacherDesk(answer),
            DreamEscapeIds.PuzzleMusicCage => SubmitMusicCage(answer),
            DreamEscapeIds.PuzzleMusicMetronome => SubmitMusicMetronome(answer),
            DreamEscapeIds.PuzzleMusicTuningForks => SubmitMusicTuningForks(answer),
            DreamEscapeIds.PuzzleMusicPianoRoll => SubmitMusicPianoRoll(answer),
            DreamEscapeIds.PuzzleMusicAcousticWall => SubmitMusicAcousticWall(answer),
            DreamEscapeIds.PuzzleMusicAttendanceReader => SubmitAttendance(answer),
            DreamEscapeIds.PuzzleArchiveSignalRegistry => SubmitArchiveSignalRegistry(answer),
            DreamEscapeIds.PuzzleArchiveDrawerRoute => SubmitArchiveDrawerRoute(answer),
            DreamEscapeIds.PuzzleArchiveFuseRepair => SubmitFuseRepair(answer),
            DreamEscapeIds.PuzzleArchiveRecorderRebuild => SubmitRecorderRebuild(answer),
            DreamEscapeIds.PuzzleArchiveEditingCounter => SubmitEditingCounter(answer),
            DreamEscapeIds.PuzzleAuditoriumLineSetBalance => SubmitLineSetBalance(answer),
            DreamEscapeIds.PuzzleAuditoriumShadowProjector => SubmitShadowProjector(answer),
            DreamEscapeIds.PuzzleAuditoriumMasterTapeSplice => SubmitMasterTapeSplice(answer),
            DreamEscapeIds.PuzzleAuditoriumLightConsole =>
                SubmitFinalConsole(puzzleId, answer, ["3", "7", "2"], requireXiaoyu: true),
            DreamEscapeIds.PuzzleAuditoriumSoundConsole =>
                SubmitFinalConsole(puzzleId, answer, ["woodblock", "piano", "triangle", "handbell"]),
            DreamEscapeIds.PuzzleAuditoriumBroadcastConsole =>
                SubmitFinalConsole(puzzleId, answer, ["curtain", "clock", "bell", "register"]),
            _ => DreamActionResult.NoChange("DREAM_FEEDBACK_PUZZLE_NOT_SUBMITTABLE")
        };
    }

    public DreamActionResult PrepareLeftLever()
    {
        RequireInProgress();
        if (State.CurrentAreaId != DreamEscapeIds.AreaAuditorium)
            return DreamActionResult.NoChange("DREAM_FEEDBACK_TARGET_NOT_HERE");
        if (!State.XiaoyuJoined || !FinalConsolesSolved())
            return DreamActionResult.NoChange("DREAM_FEEDBACK_FINAL_PANELS_INCOMPLETE");
        return DreamActionResult.NoChange("DREAM_FEEDBACK_XIAOYU_READY");
    }

    public DreamActionResult OpenExit()
    {
        if (State.Status == DreamEscapeStatus.ExitOpened)
            return DreamActionResult.NoChange("DREAM_FEEDBACK_EXIT_ALREADY_OPEN");
        RequireInProgress();
        if (State.CurrentAreaId != DreamEscapeIds.AreaAuditorium)
            return DreamActionResult.NoChange("DREAM_FEEDBACK_TARGET_NOT_HERE");
        if (!State.XiaoyuJoined || !FinalConsolesSolved())
            return DreamActionResult.NoChange("DREAM_FEEDBACK_XIAOYU_NOT_READY");
        State.SolvedPuzzles.Add(DreamEscapeIds.PuzzleExit);
        State.Status = DreamEscapeStatus.ExitOpened;
        return DreamActionResult.Success("DREAM_FEEDBACK_EXIT_OPENED");
    }

    public DreamActionResult AdvanceHint(string puzzleId)
    {
        RequireInProgress();
        if (!State.XiaoyuJoined || !DreamEscapeIds.Puzzles.Contains(puzzleId) || !IsPuzzleDiscovered(puzzleId))
            return DreamActionResult.NoChange("DREAM_FEEDBACK_HINT_NOT_AVAILABLE");
        if (State.SolvedPuzzles.Contains(puzzleId))
            return DreamActionResult.NoChange("DREAM_FEEDBACK_PUZZLE_ALREADY_SOLVED");
        var current = State.HintLevels.GetValueOrDefault(puzzleId);
        if (current >= 3)
            return DreamActionResult.NoChange("DREAM_FEEDBACK_HINT_MAXIMUM");
        State.HintLevels[puzzleId] = current + 1;
        return DreamActionResult.Success($"DREAM_HINT_{HintKey(puzzleId)}_{current + 1}");
    }

    public DreamActionResult CompletePinWake()
    {
        if (State.Status == DreamEscapeStatus.CompletedPin)
            return DreamActionResult.NoChange("DREAM_FEEDBACK_PIN_ALREADY_COMPLETED");
        if (State.Status != DreamEscapeStatus.PinPendingWake)
            return DreamActionResult.NoChange("DREAM_FEEDBACK_PIN_NOT_PENDING");
        State.Status = DreamEscapeStatus.CompletedPin;
        game.Player.LocationId = "old_house";
        game.Player.PositionX = 320f;
        game.Player.PositionY = 260f;
        return DreamActionResult.Success("DREAM_FEEDBACK_PIN_WAKE_COMPLETED");
    }

    public IReadOnlyList<string> AvailableHintPuzzles() => !State.XiaoyuJoined
        ? []
        : DreamEscapeIds.Puzzles.Where(IsPuzzleDiscovered)
            .Where(puzzle => !State.SolvedPuzzles.Contains(puzzle))
            .Order(StringComparer.Ordinal).ToArray();

    private DreamActionResult SubmitWindowLatches(IReadOnlyList<string> answer)
    {
        if (!Matches(answer, "up", "down", "middle"))
            return Wrong();
        State.SolvedPuzzles.Add(DreamEscapeIds.PuzzleWindowLatches);
        State.Items.UnionWith([DreamEscapeIds.ItemChalk, DreamEscapeIds.ItemClockKey,
            DreamEscapeIds.ItemRosterSlider]);
        return DreamActionResult.Success("DREAM_FEEDBACK_WINDOW_CACHE_OPENED");
    }

    private DreamActionResult RevealBlackboard()
    {
        if (!State.SolvedPuzzles.Contains(DreamEscapeIds.PuzzleWindowLatches))
            return DreamActionResult.NoChange("DREAM_FEEDBACK_WINDOW_CACHE_CLOSED");
        return SolveWithItem(DreamEscapeIds.PuzzleBlackboard, null, DreamEscapeIds.ClueDeskSources,
            "DREAM_FEEDBACK_BLACKBOARD_REVEALED");
    }

    private DreamActionResult SubmitClockRestore(IReadOnlyList<string> answer)
    {
        if (!HasSlots(DreamEscapeIds.SlotClassroomClockHand, DreamEscapeIds.SlotClassroomClockKey))
            return DreamActionResult.NoChange("DREAM_FEEDBACK_CLOCK_PARTS_MISSING");
        if (!Matches(answer, "7", "00"))
            return Wrong();
        return MarkSolved(DreamEscapeIds.PuzzleClockRestore, "DREAM_FEEDBACK_CLOCK_RESTORED");
    }

    private DreamActionResult SubmitRosterSlider(IReadOnlyList<string> answer)
    {
        if (!HasSlots(DreamEscapeIds.SlotClassroomRosterSlider))
            return DreamActionResult.NoChange("DREAM_FEEDBACK_ROSTER_SLIDER_MISSING");
        if (!Matches(answer, "center", "2"))
            return Wrong();
        return MarkSolved(DreamEscapeIds.PuzzleRosterSlider, "DREAM_FEEDBACK_ROSTER_RESTORED");
    }

    private DreamActionResult SubmitTeacherDesk(IReadOnlyList<string> answer)
    {
        if (!HasSolved(DreamEscapeIds.PuzzleBlackboard, DreamEscapeIds.PuzzleClockRestore,
                DreamEscapeIds.PuzzleRosterSlider))
            return DreamActionResult.NoChange("DREAM_FEEDBACK_NEED_CLASSROOM_OBSERVATIONS");
        if (!Matches(answer, "3", "7", "2"))
            return Wrong();
        State.SolvedPuzzles.Add(DreamEscapeIds.PuzzleTeacherDesk);
        State.Items.Add(DreamEscapeIds.ItemRoomTwoKey);
        return DreamActionResult.Success("DREAM_FEEDBACK_DESK_OPENED");
    }

    private DreamActionResult RevealMusicScore()
    {
        var result = SolveWithItem(DreamEscapeIds.PuzzleMusicScore, DreamEscapeIds.ItemChalk,
            DreamEscapeIds.ClueCageSequence, "DREAM_FEEDBACK_SCORE_REVEALED");
        return result;
    }

    private DreamActionResult SubmitMusicCage(IReadOnlyList<string> answer)
    {
        if (!State.SolvedPuzzles.Contains(DreamEscapeIds.PuzzleMusicScore))
            return DreamActionResult.NoChange("DREAM_FEEDBACK_NEED_SCORE_CLUE");
        if (!Matches(answer, "1", "4", "2", "3"))
            return Wrong();
        State.SolvedPuzzles.Add(DreamEscapeIds.PuzzleMusicCage);
        State.XiaoyuJoined = true;
        return DreamActionResult.Success("DREAM_FEEDBACK_XIAOYU_JOINED");
    }

    private DreamActionResult SubmitMusicMetronome(IReadOnlyList<string> answer)
    {
        if (!State.XiaoyuJoined)
            return DreamActionResult.NoChange("DREAM_FEEDBACK_NEED_XIAOYU");
        if (!HasSlots(DreamEscapeIds.SlotMusicMetronomeWeight))
            return DreamActionResult.NoChange("DREAM_FEEDBACK_METRONOME_WEIGHT_MISSING");
        if (!Matches(answer, "72"))
            return Wrong();
        State.SolvedPuzzles.Add(DreamEscapeIds.PuzzleMusicMetronome);
        State.DiscoveredClues.Add(DreamEscapeIds.ClueMusicBeatGlyphs);
        return DreamActionResult.Success("DREAM_FEEDBACK_METRONOME_CALIBRATED");
    }

    private DreamActionResult SubmitMusicTuningForks(IReadOnlyList<string> answer)
    {
        if (!State.XiaoyuJoined)
            return DreamActionResult.NoChange("DREAM_FEEDBACK_NEED_XIAOYU");
        if (!HasItems(DreamEscapeIds.ItemRubberMallet, DreamEscapeIds.ItemListeningCone))
            return DreamActionResult.NoChange("DREAM_FEEDBACK_TUNING_TOOLS_MISSING");
        if (!Matches(answer, "A3", "C4", "E4", "A4"))
            return Wrong();
        State.SolvedPuzzles.Add(DreamEscapeIds.PuzzleMusicTuningForks);
        State.Items.Remove(DreamEscapeIds.ItemRubberMallet);
        State.Items.Remove(DreamEscapeIds.ItemListeningCone);
        State.DiscoveredClues.Add(DreamEscapeIds.ClueMusicGlyphPitchMap);
        return DreamActionResult.Success("DREAM_FEEDBACK_TUNING_FORKS_CALIBRATED");
    }

    private DreamActionResult SubmitMusicPianoRoll(IReadOnlyList<string> answer)
    {
        if (!State.XiaoyuJoined)
            return DreamActionResult.NoChange("DREAM_FEEDBACK_NEED_XIAOYU");
        if (!HasItems(DreamEscapeIds.ItemTornPianoRoll, DreamEscapeIds.ItemWaxPatch))
            return DreamActionResult.NoChange("DREAM_FEEDBACK_PIANO_ROLL_PARTS_MISSING");
        if (!Matches(answer, "2", "4", "1", "3"))
            return Wrong();
        State.SolvedPuzzles.Add(DreamEscapeIds.PuzzleMusicPianoRoll);
        State.Items.Remove(DreamEscapeIds.ItemTornPianoRoll);
        State.Items.Remove(DreamEscapeIds.ItemWaxPatch);
        State.InstalledParts.Add(DreamEscapeIds.SlotMusicPianoRoll);
        State.DiscoveredClues.Add(DreamEscapeIds.ClueMusicPitchInstrumentMap);
        return DreamActionResult.Success("DREAM_FEEDBACK_PIANO_ROLL_REPAIRED");
    }

    private DreamActionResult SubmitMusicAcousticWall(IReadOnlyList<string> answer)
    {
        if (!State.XiaoyuJoined)
            return DreamActionResult.NoChange("DREAM_FEEDBACK_NEED_XIAOYU");
        if (!HasSolved(DreamEscapeIds.PuzzleMusicMetronome, DreamEscapeIds.PuzzleMusicTuningForks,
                DreamEscapeIds.PuzzleMusicPianoRoll))
            return DreamActionResult.NoChange("DREAM_FEEDBACK_NEED_MUSIC_TRANSLATIONS");
        if (!Matches(answer, "woodblock", "piano", "triangle", "handbell"))
            return Wrong();
        State.SolvedPuzzles.Add(DreamEscapeIds.PuzzleMusicAcousticWall);
        State.DiscoveredClues.Add(DreamEscapeIds.ClueMusicAcousticSequence);
        State.Items.Add(DreamEscapeIds.ItemRoomThreeKey);
        return DreamActionResult.Success("DREAM_FEEDBACK_ACOUSTIC_WALL_OPENED");
    }

    private DreamActionResult SubmitAttendance(IReadOnlyList<string> answer)
    {
        if (!HasItems(DreamEscapeIds.ItemClassPhotoNegative, DreamEscapeIds.ItemAttendanceOverlay,
                DreamEscapeIds.ItemRedFilter))
            return DreamActionResult.NoChange("DREAM_FEEDBACK_NEED_SEAT_COORDINATES");
        if (!Matches(answer, "B2", "D1", "A3", "C4"))
            return Wrong();
        State.SolvedPuzzles.Add(DreamEscapeIds.PuzzleMusicAttendanceReader);
        State.Items.ExceptWith([DreamEscapeIds.ItemClassPhotoNegative, DreamEscapeIds.ItemAttendanceOverlay,
            DreamEscapeIds.ItemRedFilter]);
        State.DiscoveredClues.Add(DreamEscapeIds.ClueSeatCoordinates);
        State.Items.UnionWith([DreamEscapeIds.ItemCalibrationReel, DreamEscapeIds.ItemRecorderCrank]);
        return DreamActionResult.Success("DREAM_FEEDBACK_ATTENDANCE_OPENED");
    }

    private DreamActionResult SubmitArchiveSignalRegistry(IReadOnlyList<string> answer)
    {
        if (!Matches(answer, "choir", "broadcast", "art", "track"))
            return Wrong();
        State.SolvedPuzzles.Add(DreamEscapeIds.PuzzleArchiveSignalRegistry);
        State.Items.UnionWith([DreamEscapeIds.ItemMaintenanceKey, DreamEscapeIds.ItemRedFilter,
            DreamEscapeIds.ItemFuseTube]);
        State.DiscoveredClues.Add(DreamEscapeIds.ClueMapTool);
        return DreamActionResult.Success("DREAM_FEEDBACK_SIGNAL_REGISTRY_OPENED");
    }

    private DreamActionResult SubmitArchiveDrawerRoute(IReadOnlyList<string> answer)
    {
        if (!State.Items.Contains(DreamEscapeIds.ItemArchiveIndexStrip))
            return DreamActionResult.NoChange("DREAM_FEEDBACK_ARCHIVE_INDEX_MISSING");
        if (!Matches(answer, "C1", "A4", "D2", "B3"))
            return Wrong();
        State.SolvedPuzzles.Add(DreamEscapeIds.PuzzleArchiveDrawerRoute);
        State.Items.UnionWith([DreamEscapeIds.ItemCopperWire, DreamEscapeIds.ItemSplicingTape]);
        return DreamActionResult.Success("DREAM_FEEDBACK_ARCHIVE_ROUTE_OPENED");
    }

    private DreamActionResult SubmitFuseRepair(IReadOnlyList<string> answer)
    {
        if (!HasItems(DreamEscapeIds.ItemFuseTube, DreamEscapeIds.ItemCopperWire))
            return DreamActionResult.NoChange("DREAM_FEEDBACK_FUSE_PARTS_MISSING");
        if (!Matches(answer, "left", "middle", "right"))
            return Wrong();
        State.SolvedPuzzles.Add(DreamEscapeIds.PuzzleArchiveFuseRepair);
        State.Items.ExceptWith([DreamEscapeIds.ItemFuseTube, DreamEscapeIds.ItemCopperWire]);
        State.Items.Add(DreamEscapeIds.ItemRepairedFuse);
        return DreamActionResult.Success("DREAM_FEEDBACK_FUSE_REPAIRED");
    }

    private DreamActionResult SubmitRecorderRebuild(IReadOnlyList<string> answer)
    {
        if (!State.XiaoyuJoined)
            return DreamActionResult.NoChange("DREAM_FEEDBACK_NEED_XIAOYU");
        if (!HasSlots(DreamEscapeIds.SlotArchiveRecorderReel, DreamEscapeIds.SlotArchiveRecorderCrank,
                DreamEscapeIds.SlotArchiveRecorderFuse))
            return DreamActionResult.NoChange("DREAM_FEEDBACK_RECORDER_PARTS_MISSING");
        if (answer.Count > 0 && !Matches(answer, "confirm"))
            return Wrong();
        State.SolvedPuzzles.Add(DreamEscapeIds.PuzzleArchiveRecorderRebuild);
        State.DiscoveredClues.Add(DreamEscapeIds.ClueArchiveEditingStops);
        State.DiscoveredClues.Add(DreamEscapeIds.ClueBroadcastSequence);
        return DreamActionResult.Success("DREAM_FEEDBACK_RECORDER_PLAYING");
    }

    private DreamActionResult SubmitEditingCounter(IReadOnlyList<string> answer)
    {
        if (!State.SolvedPuzzles.Contains(DreamEscapeIds.PuzzleArchiveRecorderRebuild))
            return DreamActionResult.NoChange("DREAM_FEEDBACK_RECORDER_NOT_READY");
        if (!Matches(answer, "12", "24", "31", "45"))
            return Wrong();
        State.SolvedPuzzles.Add(DreamEscapeIds.PuzzleArchiveEditingCounter);
        State.Items.Remove(DreamEscapeIds.ItemArchiveIndexStrip);
        State.Items.Add(DreamEscapeIds.ItemRoomFourKey);
        return DreamActionResult.Success("DREAM_FEEDBACK_EDITING_COUNTER_OPENED");
    }

    private DreamActionResult SubmitLineSetBalance(IReadOnlyList<string> answer)
    {
        if (!HasSlots(DreamEscapeIds.SlotAuditoriumCurtainCounterweight))
            return DreamActionResult.NoChange("DREAM_FEEDBACK_CURTAIN_WEIGHT_MISSING");
        if (!Matches(answer, "3", "1", "4", "2"))
            return Wrong();
        return MarkSolved(DreamEscapeIds.PuzzleAuditoriumLineSetBalance,
            "DREAM_FEEDBACK_LINE_SET_BALANCED");
    }

    private DreamActionResult SubmitShadowProjector(IReadOnlyList<string> answer)
    {
        if (!State.SolvedPuzzles.Contains(DreamEscapeIds.PuzzleAuditoriumLineSetBalance))
            return DreamActionResult.NoChange("DREAM_FEEDBACK_CURTAIN_CLOSED");
        if (!HasSlots(DreamEscapeIds.SlotAuditoriumProjectorLens))
            return DreamActionResult.NoChange("DREAM_FEEDBACK_PROJECTOR_LENS_MISSING");
        if (!Matches(answer, "window", "figure", "bell"))
            return Wrong();
        State.SolvedPuzzles.Add(DreamEscapeIds.PuzzleAuditoriumShadowProjector);
        State.Items.UnionWith([DreamEscapeIds.ItemStageHook, DreamEscapeIds.ItemBenchClampKey,
            DreamEscapeIds.ItemInspectionMirror, DreamEscapeIds.ItemMasterReelCore]);
        return DreamActionResult.Success("DREAM_FEEDBACK_PROJECTOR_OPENED");
    }

    private DreamActionResult SubmitMasterTapeSplice(IReadOnlyList<string> answer)
    {
        if (!HasItems(DreamEscapeIds.ItemTapeFragmentClassroom, DreamEscapeIds.ItemTapeFragmentMusic,
                DreamEscapeIds.ItemTapeFragmentArchive, DreamEscapeIds.ItemSplicingTape,
                DreamEscapeIds.ItemMasterReelCore))
            return DreamActionResult.NoChange("DREAM_FEEDBACK_TAPE_PARTS_MISSING");
        if (!Matches(answer, "classroom", "music", "archive"))
            return Wrong();
        State.SolvedPuzzles.Add(DreamEscapeIds.PuzzleAuditoriumMasterTapeSplice);
        State.Items.ExceptWith([DreamEscapeIds.ItemTapeFragmentClassroom,
            DreamEscapeIds.ItemTapeFragmentMusic, DreamEscapeIds.ItemTapeFragmentArchive,
            DreamEscapeIds.ItemSplicingTape, DreamEscapeIds.ItemMasterReelCore]);
        State.Items.Add(DreamEscapeIds.ItemRestoredMasterTape);
        return DreamActionResult.Success("DREAM_FEEDBACK_MASTER_TAPE_RESTORED");
    }

    private DreamActionResult SubmitFinalConsole(string puzzleId, IReadOnlyList<string> answer,
        IReadOnlyList<string> expected, bool requireXiaoyu = false)
    {
        if (!HasSlots(DreamEscapeIds.SlotAuditoriumMasterTape))
            return DreamActionResult.NoChange("DREAM_FEEDBACK_MASTER_TAPE_NOT_PLAYING");
        if (requireXiaoyu && !State.XiaoyuJoined)
            return DreamActionResult.NoChange("DREAM_FEEDBACK_NEED_XIAOYU");
        if (!answer.SequenceEqual(expected, StringComparer.Ordinal))
            return Wrong();
        return MarkSolved(puzzleId, "DREAM_FEEDBACK_FINAL_PANEL_SOLVED");
    }

    private DreamActionResult OpenMapCabinet()
    {
        if (!State.SolvedPuzzles.Contains(DreamEscapeIds.PuzzleArchiveSignalRegistry))
            return DreamActionResult.NoChange("DREAM_FEEDBACK_MAP_TOOL_NOT_READY");
        var result = SolveWithItem(DreamEscapeIds.PuzzleMapCabinet, DreamEscapeIds.ItemMaintenanceKey, null,
            "DREAM_FEEDBACK_MAP_CABINET_OPENED");
        if (result.Changed)
            State.Items.UnionWith([DreamEscapeIds.ItemClassPhotoNegative, DreamEscapeIds.ItemAttendanceOverlay]);
        return result;
    }

    private DreamActionResult OpenTransomCache()
    {
        if (!State.SolvedPuzzles.Contains(DreamEscapeIds.PuzzleAuditoriumShadowProjector))
            return DreamActionResult.NoChange("DREAM_FEEDBACK_TRANSOM_TOOL_NOT_READY");
        var result = SolveWithItem(DreamEscapeIds.PuzzleTransomCache, DreamEscapeIds.ItemStageHook, null,
            "DREAM_FEEDBACK_TRANSOM_CACHE_OPENED");
        if (result.Changed)
            State.Items.Add(DreamEscapeIds.ItemTapeFragmentClassroom);
        return result;
    }

    private DreamActionResult OpenPianoBenchCache()
    {
        if (!State.SolvedPuzzles.Contains(DreamEscapeIds.PuzzleAuditoriumShadowProjector))
            return DreamActionResult.NoChange("DREAM_FEEDBACK_BENCH_TOOL_NOT_READY");
        var result = SolveWithItem(DreamEscapeIds.PuzzleMusicPianoBenchCache,
            DreamEscapeIds.ItemBenchClampKey, null, "DREAM_FEEDBACK_PIANO_BENCH_OPENED");
        if (result.Changed)
            State.Items.Add(DreamEscapeIds.ItemTapeFragmentMusic);
        return result;
    }

    private DreamActionResult OpenUpperVault()
    {
        if (!State.SolvedPuzzles.Contains(DreamEscapeIds.PuzzleAuditoriumShadowProjector))
            return DreamActionResult.NoChange("DREAM_FEEDBACK_VAULT_TOOL_NOT_READY");
        var result = SolveWithItem(DreamEscapeIds.PuzzleArchiveUpperVault,
            DreamEscapeIds.ItemInspectionMirror, null, "DREAM_FEEDBACK_UPPER_VAULT_OPENED");
        if (result.Changed)
            State.Items.Add(DreamEscapeIds.ItemTapeFragmentArchive);
        return result;
    }

    private DreamActionResult InstallRecorderPart(string itemId, string slotId, string feedbackKey)
    {
        if (!State.XiaoyuJoined)
            return DreamActionResult.NoChange("DREAM_FEEDBACK_NEED_XIAOYU");
        return InstallPart(itemId, slotId, feedbackKey);
    }

    private DreamActionResult InstallMasterTape()
    {
        if (!State.SolvedPuzzles.Contains(DreamEscapeIds.PuzzleAuditoriumMasterTapeSplice))
            return DreamActionResult.NoChange("DREAM_FEEDBACK_MASTER_TAPE_NOT_READY");
        return InstallPart(DreamEscapeIds.ItemRestoredMasterTape,
            DreamEscapeIds.SlotAuditoriumMasterTape, "DREAM_FEEDBACK_MASTER_TAPE_PLAYING");
    }

    private DreamActionResult UnlockAreaWithKey(string itemId, string areaId, string requiredPuzzle,
        string feedbackKey)
    {
        if (!State.SolvedPuzzles.Contains(requiredPuzzle))
            return DreamActionResult.NoChange("DREAM_FEEDBACK_KEY_NOT_READY");
        if (State.UnlockedAreas.Contains(areaId))
            return DreamActionResult.NoChange("DREAM_FEEDBACK_AREA_ALREADY_OPEN");
        State.Items.Remove(itemId);
        State.UnlockedAreas.Add(areaId);
        return DreamActionResult.Success(feedbackKey);
    }

    private DreamActionResult InstallPart(string itemId, string slotId, string feedbackKey)
    {
        if (State.InstalledParts.Contains(slotId))
            return DreamActionResult.NoChange("DREAM_FEEDBACK_PART_ALREADY_INSTALLED");
        State.Items.Remove(itemId);
        State.InstalledParts.Add(slotId);
        return DreamActionResult.Success(feedbackKey);
    }

    private DreamActionResult PreparePinWake()
    {
        if (State.Status != DreamEscapeStatus.InProgress)
            return DreamActionResult.NoChange("DREAM_FEEDBACK_PIN_NOT_AVAILABLE");
        State.Status = DreamEscapeStatus.PinPendingWake;
        State.Items.Clear();
        return DreamActionResult.Success("DREAM_FEEDBACK_PIN_USED");
    }

    private DreamActionResult SolveWithItem(string puzzleId, string? consumedItem, string? clueId,
        string feedbackKey)
    {
        if (State.SolvedPuzzles.Contains(puzzleId))
            return DreamActionResult.NoChange("DREAM_FEEDBACK_PUZZLE_ALREADY_SOLVED");
        State.SolvedPuzzles.Add(puzzleId);
        if (consumedItem is not null)
            State.Items.Remove(consumedItem);
        if (clueId is not null)
            State.DiscoveredClues.Add(clueId);
        return DreamActionResult.Success(feedbackKey);
    }

    private DreamActionResult MarkSolved(string puzzleId, string feedbackKey) =>
        State.SolvedPuzzles.Add(puzzleId)
            ? DreamActionResult.Success(feedbackKey)
            : DreamActionResult.NoChange("DREAM_FEEDBACK_PUZZLE_ALREADY_SOLVED");

    private bool IsPuzzleDiscovered(string puzzleId) => State.DiscoveredPuzzles.Contains(puzzleId);

    private static string HintKey(string puzzleId) => puzzleId
        .Replace("dream.", string.Empty, StringComparison.Ordinal)
        .Replace('.', '_').ToUpperInvariant();

    private static string? TargetArea(string targetId)
    {
        if (targetId.StartsWith("target.classroom.", StringComparison.Ordinal)) return DreamEscapeIds.AreaClassroom;
        if (targetId.StartsWith("target.music.", StringComparison.Ordinal)) return DreamEscapeIds.AreaMusic;
        if (targetId.StartsWith("target.archive.", StringComparison.Ordinal)) return DreamEscapeIds.AreaArchive;
        if (targetId.StartsWith("target.auditorium.", StringComparison.Ordinal)) return DreamEscapeIds.AreaAuditorium;
        if (targetId.StartsWith("target.corridor.", StringComparison.Ordinal)) return DreamEscapeIds.AreaCorridor;
        return null;
    }

    private static string? PuzzleArea(string puzzleId)
    {
        if (puzzleId.StartsWith("dream.classroom.", StringComparison.Ordinal)) return DreamEscapeIds.AreaClassroom;
        if (puzzleId.StartsWith("dream.music.", StringComparison.Ordinal)) return DreamEscapeIds.AreaMusic;
        if (puzzleId.StartsWith("dream.archive.", StringComparison.Ordinal)) return DreamEscapeIds.AreaArchive;
        if (puzzleId.StartsWith("dream.auditorium.", StringComparison.Ordinal)) return DreamEscapeIds.AreaAuditorium;
        return null;
    }

    private bool FinalConsolesSolved() => HasSolved(
        DreamEscapeIds.PuzzleAuditoriumLightConsole,
        DreamEscapeIds.PuzzleAuditoriumSoundConsole,
        DreamEscapeIds.PuzzleAuditoriumBroadcastConsole);

    private bool HasSolved(params string[] puzzleIds) => puzzleIds.All(State.SolvedPuzzles.Contains);
    private bool HasItems(params string[] itemIds) => itemIds.All(State.Items.Contains);
    private bool HasSlots(params string[] slotIds) => slotIds.All(State.InstalledParts.Contains);
    private static DreamActionResult Wrong() => DreamActionResult.NoChange("DREAM_FEEDBACK_WRONG_ANSWER");
    private static bool Matches(IReadOnlyList<string> answer, params string[] expected) =>
        answer.SequenceEqual(expected, StringComparer.Ordinal);

    private void RequireInProgress()
    {
        if (State.Status != DreamEscapeStatus.InProgress)
            throw new InvalidOperationException($"Dream escape is not active ({State.Status}).");
    }

    private void RequireInProgressOrExitOpened()
    {
        if (State.Status is not (DreamEscapeStatus.InProgress or DreamEscapeStatus.ExitOpened))
            throw new InvalidOperationException($"Dream escape cannot move in status {State.Status}.");
    }
}
