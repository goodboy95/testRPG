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
        {
            return DreamActionResult.NoChange("DREAM_FEEDBACK_AREA_LOCKED");
        }

        if (State.CurrentAreaId == areaId &&
            Math.Abs(State.PositionX - positionX) < 0.001f &&
            Math.Abs(State.PositionY - positionY) < 0.001f)
        {
            return DreamActionResult.NoChange("DREAM_FEEDBACK_ALREADY_THERE");
        }

        State.CurrentAreaId = areaId;
        State.PositionX = positionX;
        State.PositionY = positionY;
        return DreamActionResult.Success("DREAM_FEEDBACK_AREA_CHANGED");
    }

    public DreamActionResult Pickup(string itemId)
    {
        RequireInProgress();
        if (!DreamEscapeIds.Items.Contains(itemId))
        {
            throw new ArgumentOutOfRangeException(nameof(itemId), itemId, "Unknown dream item.");
        }

        if (itemId != DreamEscapeIds.ItemChalk || State.CurrentAreaId != DreamEscapeIds.AreaClassroom)
        {
            return DreamActionResult.NoChange("DREAM_FEEDBACK_ITEM_NOT_AVAILABLE");
        }

        if (State.Items.Contains(itemId))
        {
            return DreamActionResult.NoChange("DREAM_FEEDBACK_ALREADY_HAVE_ITEM");
        }

        if (itemId == DreamEscapeIds.ItemChalk && State.SolvedPuzzles.Contains("dream.pickup.chalk"))
        {
            return DreamActionResult.NoChange("DREAM_FEEDBACK_ALREADY_HAVE_ITEM");
        }

        State.Items.Add(itemId);
        if (itemId == DreamEscapeIds.ItemChalk)
        {
            State.SolvedPuzzles.Add("dream.pickup.chalk");
        }
        return DreamActionResult.Success("DREAM_FEEDBACK_ITEM_PICKED_UP");
    }

    public DreamActionResult UseItem(string itemId, string targetId)
    {
        RequireInProgress();
        if (!State.Items.Contains(itemId))
        {
            return DreamActionResult.NoChange("DREAM_FEEDBACK_ITEM_MISSING");
        }

        var targetArea = TargetArea(targetId);
        if (targetArea is not null && State.CurrentAreaId != targetArea)
        {
            return DreamActionResult.NoChange("DREAM_FEEDBACK_TARGET_NOT_HERE");
        }

        return (itemId, targetId) switch
        {
            (DreamEscapeIds.ItemChalk, "target.classroom.blackboard") =>
                SolveWithItem(DreamEscapeIds.PuzzleBlackboard, null, DreamEscapeIds.ClueDeskSources,
                    "DREAM_FEEDBACK_BLACKBOARD_REVEALED"),
            (DreamEscapeIds.ItemRoomTwoKey, "target.corridor.room_two_door") =>
                SolveWithItem(DreamEscapeIds.PuzzleRoomTwoDoor, DreamEscapeIds.ItemRoomTwoKey, null,
                    "DREAM_FEEDBACK_ROOM_TWO_OPEN", DreamEscapeIds.AreaMusic),
            (DreamEscapeIds.ItemChalk, "target.music.score") =>
                SolveWithItem(DreamEscapeIds.PuzzleMusicScore, null, DreamEscapeIds.ClueCageSequence,
                    "DREAM_FEEDBACK_SCORE_REVEALED"),
            (DreamEscapeIds.ItemMaintenanceKey, "target.classroom.map_cabinet") =>
                OpenMapCabinet(),
            (DreamEscapeIds.ItemRedFilter, "target.classroom.class_photo") =>
                RevealSeatCoordinates(),
            (DreamEscapeIds.ItemTapeReel, "target.archive.recorder") =>
                SolveWithItem(DreamEscapeIds.PuzzleRecorderTape, DreamEscapeIds.ItemTapeReel, null,
                    "DREAM_FEEDBACK_TAPE_INSERTED"),
            (DreamEscapeIds.ItemRecorderCrank, "target.archive.recorder") =>
                RepairRecorder(),
            (DreamEscapeIds.ItemPin, "target.self") => PreparePinWake(),
            _ => DreamActionResult.NoChange("DREAM_FEEDBACK_ITEM_DOES_NOT_FIT")
        };
    }

    public DreamActionResult Observe(string targetId)
    {
        RequireInProgress();
        var targetArea = TargetArea(targetId);
        if (targetArea is not null && State.CurrentAreaId != targetArea)
        {
            return DreamActionResult.NoChange("DREAM_FEEDBACK_TARGET_NOT_HERE");
        }
        if (targetId.StartsWith("target.classroom.", StringComparison.Ordinal) &&
            !State.SolvedPuzzles.Contains(DreamEscapeIds.PuzzleBlackboard))
        {
            return DreamActionResult.NoChange("DREAM_FEEDBACK_NEED_BLACKBOARD_CLUE");
        }
        var clueId = targetId switch
        {
            "target.classroom.window" => DreamEscapeIds.ClueClassroomWindow,
            "target.classroom.clock" => DreamEscapeIds.ClueClassroomClock,
            "target.classroom.duty_roster" => DreamEscapeIds.ClueClassroomDutyRoster,
            "target.music.listen_window" => DreamEscapeIds.ClueListenWindow,
            "target.music.listen_lectern" => DreamEscapeIds.ClueListenLectern,
            "target.music.listen_door" => DreamEscapeIds.ClueListenDoor,
            "target.archive.rules" => DreamEscapeIds.ClueArchiveRules,
            _ => throw new ArgumentOutOfRangeException(nameof(targetId), targetId, "Unknown observation target.")
        };

        if (targetId.StartsWith("target.music.listen_", StringComparison.Ordinal) && !State.XiaoyuJoined)
        {
            return DreamActionResult.NoChange("DREAM_FEEDBACK_NEED_XIAOYU");
        }

        return State.DiscoveredClues.Add(clueId)
            ? DreamActionResult.Success("DREAM_FEEDBACK_CLUE_RECORDED")
            : DreamActionResult.NoChange("DREAM_FEEDBACK_CLUE_ALREADY_RECORDED");
    }

    public DreamActionResult SubmitPuzzle(string puzzleId, params string[] answer)
    {
        RequireInProgress();
        if (!DreamEscapeIds.Puzzles.Contains(puzzleId))
        {
            throw new ArgumentOutOfRangeException(nameof(puzzleId), puzzleId, "Unknown dream puzzle.");
        }
        if (State.SolvedPuzzles.Contains(puzzleId))
        {
            return DreamActionResult.NoChange("DREAM_FEEDBACK_PUZZLE_ALREADY_SOLVED");
        }
        var puzzleArea = PuzzleArea(puzzleId);
        if (puzzleArea is not null && State.CurrentAreaId != puzzleArea)
        {
            return DreamActionResult.NoChange("DREAM_FEEDBACK_TARGET_NOT_HERE");
        }

        return puzzleId switch
        {
            DreamEscapeIds.PuzzleTeacherDesk => SubmitTeacherDesk(answer),
            DreamEscapeIds.PuzzleMusicCage => SubmitMusicCage(answer),
            DreamEscapeIds.PuzzleListening => SubmitListening(answer),
            DreamEscapeIds.PuzzleArchiveOrder => SubmitArchiveOrder(answer),
            DreamEscapeIds.PuzzleAttendance => SubmitAttendance(answer),
            DreamEscapeIds.PuzzleRoomFourDoor => SubmitRoomFourDoor(answer),
            DreamEscapeIds.PuzzleFinalDesk => SubmitFinalPanel(puzzleId, answer, ["3", "7", "2"]),
            DreamEscapeIds.PuzzleFinalInstruments => SubmitFinalPanel(
                puzzleId, answer, ["woodblock", "piano", "triangle", "handbell"]),
            DreamEscapeIds.PuzzleFinalBroadcast => SubmitFinalPanel(
                puzzleId, answer, ["curtain", "clock", "bell", "register"]),
            _ => DreamActionResult.NoChange("DREAM_FEEDBACK_PUZZLE_NOT_SUBMITTABLE")
        };
    }

    public DreamActionResult PrepareLeftLever()
    {
        RequireInProgress();
        if (State.CurrentAreaId != DreamEscapeIds.AreaAuditorium)
        {
            return DreamActionResult.NoChange("DREAM_FEEDBACK_TARGET_NOT_HERE");
        }
        if (!State.XiaoyuJoined || !FinalPanelsSolved())
        {
            return DreamActionResult.NoChange("DREAM_FEEDBACK_FINAL_PANELS_INCOMPLETE");
        }
        return MarkSolved(DreamEscapeIds.PuzzleLeftLever, "DREAM_FEEDBACK_XIAOYU_READY");
    }

    public DreamActionResult OpenExit()
    {
        if (State.Status == DreamEscapeStatus.ExitOpened)
        {
            return DreamActionResult.NoChange("DREAM_FEEDBACK_EXIT_ALREADY_OPEN");
        }
        RequireInProgress();
        if (State.CurrentAreaId != DreamEscapeIds.AreaAuditorium)
        {
            return DreamActionResult.NoChange("DREAM_FEEDBACK_TARGET_NOT_HERE");
        }
        if (!State.SolvedPuzzles.Contains(DreamEscapeIds.PuzzleLeftLever))
        {
            return DreamActionResult.NoChange("DREAM_FEEDBACK_XIAOYU_NOT_READY");
        }

        if (State.SolvedPuzzles.Contains(DreamEscapeIds.PuzzleExit))
        {
            return DreamActionResult.NoChange("DREAM_FEEDBACK_EXIT_ALREADY_OPEN");
        }

        State.SolvedPuzzles.Add(DreamEscapeIds.PuzzleExit);
        State.Status = DreamEscapeStatus.ExitOpened;
        return DreamActionResult.Success("DREAM_FEEDBACK_EXIT_OPENED");
    }

    public DreamActionResult AdvanceHint(string puzzleId)
    {
        RequireInProgress();
        if (!State.XiaoyuJoined)
        {
            return DreamActionResult.NoChange("DREAM_FEEDBACK_HINT_NOT_AVAILABLE");
        }
        if (!DreamEscapeIds.Puzzles.Contains(puzzleId) || !IsPuzzleDiscovered(puzzleId))
        {
            return DreamActionResult.NoChange("DREAM_FEEDBACK_HINT_NOT_AVAILABLE");
        }
        if (State.SolvedPuzzles.Contains(puzzleId))
        {
            return DreamActionResult.NoChange("DREAM_FEEDBACK_PUZZLE_ALREADY_SOLVED");
        }

        var current = State.HintLevels.GetValueOrDefault(puzzleId);
        if (current >= 3)
        {
            return DreamActionResult.NoChange("DREAM_FEEDBACK_HINT_MAXIMUM");
        }
        State.HintLevels[puzzleId] = current + 1;
        return DreamActionResult.Success($"DREAM_HINT_{HintKey(puzzleId)}_{current + 1}");
    }

    public DreamActionResult CompletePinWake()
    {
        if (State.Status == DreamEscapeStatus.CompletedPin)
        {
            return DreamActionResult.NoChange("DREAM_FEEDBACK_PIN_ALREADY_COMPLETED");
        }
        if (State.Status != DreamEscapeStatus.PinPendingWake)
        {
            return DreamActionResult.NoChange("DREAM_FEEDBACK_PIN_NOT_PENDING");
        }

        State.Status = DreamEscapeStatus.CompletedPin;
        game.Player.LocationId = "old_house";
        game.Player.PositionX = 320f;
        game.Player.PositionY = 260f;
        return DreamActionResult.Success("DREAM_FEEDBACK_PIN_WAKE_COMPLETED");
    }

    public IReadOnlyList<string> AvailableHintPuzzles()
    {
        if (!State.XiaoyuJoined)
        {
            return [];
        }
        return DreamEscapeIds.Puzzles
            .Where(IsPuzzleDiscovered)
            .Where(puzzle => !State.SolvedPuzzles.Contains(puzzle))
            .Order(StringComparer.Ordinal)
            .ToArray();
    }

    private DreamActionResult SubmitTeacherDesk(IReadOnlyList<string> answer)
    {
        if (!State.SolvedPuzzles.Contains(DreamEscapeIds.PuzzleBlackboard))
        {
            return DreamActionResult.NoChange("DREAM_FEEDBACK_NEED_BLACKBOARD_CLUE");
        }
        if (!HasAllClues(
                DreamEscapeIds.ClueClassroomWindow,
                DreamEscapeIds.ClueClassroomClock,
                DreamEscapeIds.ClueClassroomDutyRoster))
        {
            return DreamActionResult.NoChange("DREAM_FEEDBACK_NEED_CLASSROOM_OBSERVATIONS");
        }
        if (!Matches(answer, "3", "7", "2"))
        {
            return DreamActionResult.NoChange("DREAM_FEEDBACK_WRONG_ANSWER");
        }
        State.SolvedPuzzles.Add(DreamEscapeIds.PuzzleTeacherDesk);
        State.Items.Add(DreamEscapeIds.ItemRoomTwoKey);
        State.DiscoveredClues.Add(DreamEscapeIds.ClueMapTool);
        return DreamActionResult.Success("DREAM_FEEDBACK_DESK_OPENED");
    }

    private DreamActionResult SubmitMusicCage(IReadOnlyList<string> answer)
    {
        if (!State.SolvedPuzzles.Contains(DreamEscapeIds.PuzzleMusicScore))
        {
            return DreamActionResult.NoChange("DREAM_FEEDBACK_NEED_SCORE_CLUE");
        }
        if (!Matches(answer, "1", "4", "2", "3"))
        {
            return DreamActionResult.NoChange("DREAM_FEEDBACK_WRONG_ANSWER");
        }
        State.SolvedPuzzles.Add(DreamEscapeIds.PuzzleMusicCage);
        State.XiaoyuJoined = true;
        return DreamActionResult.Success("DREAM_FEEDBACK_XIAOYU_JOINED");
    }

    private DreamActionResult SubmitListening(IReadOnlyList<string> answer)
    {
        if (!HasAllClues(DreamEscapeIds.ClueListenWindow, DreamEscapeIds.ClueListenLectern,
                DreamEscapeIds.ClueListenDoor))
        {
            return DreamActionResult.NoChange("DREAM_FEEDBACK_NEED_LISTENING_CLUES");
        }
        if (!Matches(answer, "woodblock", "piano", "triangle", "handbell"))
        {
            return DreamActionResult.NoChange("DREAM_FEEDBACK_WRONG_ANSWER");
        }
        State.SolvedPuzzles.Add(DreamEscapeIds.PuzzleListening);
        State.UnlockedAreas.Add(DreamEscapeIds.AreaArchive);
        return DreamActionResult.Success("DREAM_FEEDBACK_ROOM_THREE_OPEN");
    }

    private DreamActionResult SubmitArchiveOrder(IReadOnlyList<string> answer)
    {
        if (!State.DiscoveredClues.Contains(DreamEscapeIds.ClueArchiveRules))
        {
            return DreamActionResult.NoChange("DREAM_FEEDBACK_NEED_ARCHIVE_RULES");
        }
        if (!Matches(answer, "choir", "broadcast", "art", "track"))
        {
            return DreamActionResult.NoChange("DREAM_FEEDBACK_WRONG_ANSWER");
        }
        State.SolvedPuzzles.Add(DreamEscapeIds.PuzzleArchiveOrder);
        State.Items.Add(DreamEscapeIds.ItemMaintenanceKey);
        State.Items.Add(DreamEscapeIds.ItemRedFilter);
        return DreamActionResult.Success("DREAM_FEEDBACK_ARCHIVE_DRAWER_OPENED");
    }

    private DreamActionResult SubmitAttendance(IReadOnlyList<string> answer)
    {
        if (!State.DiscoveredClues.Contains(DreamEscapeIds.ClueSeatCoordinates))
        {
            return DreamActionResult.NoChange("DREAM_FEEDBACK_NEED_SEAT_COORDINATES");
        }
        if (!Matches(answer, "B2", "D1", "A3", "C4"))
        {
            return DreamActionResult.NoChange("DREAM_FEEDBACK_WRONG_ANSWER");
        }
        State.SolvedPuzzles.Add(DreamEscapeIds.PuzzleAttendance);
        State.Items.Add(DreamEscapeIds.ItemTapeReel);
        State.Items.Add(DreamEscapeIds.ItemRecorderCrank);
        return DreamActionResult.Success("DREAM_FEEDBACK_ATTENDANCE_OPENED");
    }

    private DreamActionResult SubmitRoomFourDoor(IReadOnlyList<string> answer)
    {
        if (!State.DiscoveredClues.Contains(DreamEscapeIds.ClueBroadcastSequence))
        {
            return DreamActionResult.NoChange("DREAM_FEEDBACK_NEED_BROADCAST_CLUE");
        }
        if (!Matches(answer, "curtain", "clock", "bell", "register"))
        {
            return DreamActionResult.NoChange("DREAM_FEEDBACK_WRONG_ANSWER");
        }
        State.SolvedPuzzles.Add(DreamEscapeIds.PuzzleRoomFourDoor);
        State.UnlockedAreas.Add(DreamEscapeIds.AreaAuditorium);
        return DreamActionResult.Success("DREAM_FEEDBACK_ROOM_FOUR_OPEN");
    }

    private DreamActionResult SubmitFinalPanel(
        string puzzleId,
        IReadOnlyList<string> answer,
        IReadOnlyList<string> expected)
    {
        if (!Matches(answer, expected.ToArray()))
        {
            return DreamActionResult.NoChange("DREAM_FEEDBACK_WRONG_ANSWER");
        }
        return MarkSolved(puzzleId, "DREAM_FEEDBACK_FINAL_PANEL_SOLVED");
    }

    private DreamActionResult OpenMapCabinet()
    {
        if (!State.SolvedPuzzles.Contains(DreamEscapeIds.PuzzleArchiveOrder))
        {
            return DreamActionResult.NoChange("DREAM_FEEDBACK_MAP_TOOL_NOT_READY");
        }
        var result = SolveWithItem(DreamEscapeIds.PuzzleMapCabinet, DreamEscapeIds.ItemMaintenanceKey,
            null, "DREAM_FEEDBACK_MAP_CABINET_OPENED");
        if (result.Changed)
        {
            State.Items.Add(DreamEscapeIds.ItemClassPhoto);
        }
        return result;
    }

    private DreamActionResult RevealSeatCoordinates()
    {
        if (!State.Items.Contains(DreamEscapeIds.ItemClassPhoto))
        {
            return DreamActionResult.NoChange("DREAM_FEEDBACK_NEED_CLASS_PHOTO");
        }
        return SolveWithItem(DreamEscapeIds.PuzzlePhotoFilter, null,
            DreamEscapeIds.ClueSeatCoordinates, "DREAM_FEEDBACK_SEATS_REVEALED");
    }

    private DreamActionResult RepairRecorder()
    {
        if (!State.SolvedPuzzles.Contains(DreamEscapeIds.PuzzleRecorderTape))
        {
            return DreamActionResult.NoChange("DREAM_FEEDBACK_NEED_TAPE_FIRST");
        }
        return SolveWithItem(DreamEscapeIds.PuzzleRecorderCrank, DreamEscapeIds.ItemRecorderCrank,
            DreamEscapeIds.ClueBroadcastSequence, "DREAM_FEEDBACK_RECORDER_PLAYING");
    }

    private DreamActionResult PreparePinWake()
    {
        if (State.Status != DreamEscapeStatus.InProgress)
        {
            return DreamActionResult.NoChange("DREAM_FEEDBACK_PIN_NOT_AVAILABLE");
        }
        State.Status = DreamEscapeStatus.PinPendingWake;
        State.Items.Clear();
        return DreamActionResult.Success("DREAM_FEEDBACK_PIN_USED");
    }

    private DreamActionResult SolveWithItem(
        string puzzleId,
        string? consumedItem,
        string? clueId,
        string feedbackKey,
        string? unlockedArea = null)
    {
        if (State.SolvedPuzzles.Contains(puzzleId))
        {
            return DreamActionResult.NoChange("DREAM_FEEDBACK_PUZZLE_ALREADY_SOLVED");
        }
        State.SolvedPuzzles.Add(puzzleId);
        if (consumedItem is not null)
        {
            State.Items.Remove(consumedItem);
        }
        if (clueId is not null)
        {
            State.DiscoveredClues.Add(clueId);
        }
        if (unlockedArea is not null)
        {
            State.UnlockedAreas.Add(unlockedArea);
        }
        return DreamActionResult.Success(feedbackKey);
    }

    private DreamActionResult MarkSolved(string puzzleId, string feedbackKey)
    {
        return State.SolvedPuzzles.Add(puzzleId)
            ? DreamActionResult.Success(feedbackKey)
            : DreamActionResult.NoChange("DREAM_FEEDBACK_PUZZLE_ALREADY_SOLVED");
    }

    private bool IsPuzzleDiscovered(string puzzleId)
    {
        return puzzleId switch
        {
            DreamEscapeIds.PuzzleBlackboard or DreamEscapeIds.PuzzleTeacherDesk =>
                State.UnlockedAreas.Contains(DreamEscapeIds.AreaClassroom),
            DreamEscapeIds.PuzzleRoomTwoDoor =>
                State.SolvedPuzzles.Contains(DreamEscapeIds.PuzzleTeacherDesk),
            DreamEscapeIds.PuzzleMusicScore or DreamEscapeIds.PuzzleMusicCage =>
                State.UnlockedAreas.Contains(DreamEscapeIds.AreaMusic),
            DreamEscapeIds.PuzzleListening => State.XiaoyuJoined,
            DreamEscapeIds.PuzzleArchiveOrder => State.UnlockedAreas.Contains(DreamEscapeIds.AreaArchive),
            DreamEscapeIds.PuzzleMapCabinet => State.DiscoveredClues.Contains(DreamEscapeIds.ClueMapTool),
            DreamEscapeIds.PuzzlePhotoFilter => State.Items.Contains(DreamEscapeIds.ItemClassPhoto),
            DreamEscapeIds.PuzzleAttendance => State.UnlockedAreas.Contains(DreamEscapeIds.AreaMusic),
            DreamEscapeIds.PuzzleRecorderTape or DreamEscapeIds.PuzzleRecorderCrank =>
                State.UnlockedAreas.Contains(DreamEscapeIds.AreaArchive),
            DreamEscapeIds.PuzzleRoomFourDoor =>
                State.DiscoveredClues.Contains(DreamEscapeIds.ClueBroadcastSequence),
            DreamEscapeIds.PuzzleFinalDesk or DreamEscapeIds.PuzzleFinalInstruments or
                DreamEscapeIds.PuzzleFinalBroadcast or DreamEscapeIds.PuzzleLeftLever or
                DreamEscapeIds.PuzzleExit => State.UnlockedAreas.Contains(DreamEscapeIds.AreaAuditorium),
            _ => false
        };
    }

    private static string HintKey(string puzzleId) => puzzleId
        .Replace("dream.", string.Empty, StringComparison.Ordinal)
        .Replace('.', '_')
        .ToUpperInvariant();

    private static string? TargetArea(string targetId) => targetId switch
    {
        "target.classroom.blackboard" or "target.classroom.window" or
            "target.classroom.clock" or "target.classroom.duty_roster" or
            "target.classroom.map_cabinet" or "target.classroom.class_photo" =>
            DreamEscapeIds.AreaClassroom,
        "target.corridor.room_two_door" => DreamEscapeIds.AreaCorridor,
        "target.music.score" or "target.music.listen_window" or
            "target.music.listen_lectern" or "target.music.listen_door" => DreamEscapeIds.AreaMusic,
        "target.archive.rules" or "target.archive.recorder" => DreamEscapeIds.AreaArchive,
        "target.self" => null,
        _ => null
    };

    private static string? PuzzleArea(string puzzleId) => puzzleId switch
    {
        DreamEscapeIds.PuzzleTeacherDesk => DreamEscapeIds.AreaClassroom,
        DreamEscapeIds.PuzzleMusicCage or DreamEscapeIds.PuzzleListening or
            DreamEscapeIds.PuzzleAttendance => DreamEscapeIds.AreaMusic,
        DreamEscapeIds.PuzzleArchiveOrder => DreamEscapeIds.AreaArchive,
        DreamEscapeIds.PuzzleRoomFourDoor => DreamEscapeIds.AreaCorridor,
        DreamEscapeIds.PuzzleFinalDesk or DreamEscapeIds.PuzzleFinalInstruments or
            DreamEscapeIds.PuzzleFinalBroadcast => DreamEscapeIds.AreaAuditorium,
        _ => null
    };

    private bool FinalPanelsSolved() =>
        State.SolvedPuzzles.Contains(DreamEscapeIds.PuzzleFinalDesk) &&
        State.SolvedPuzzles.Contains(DreamEscapeIds.PuzzleFinalInstruments) &&
        State.SolvedPuzzles.Contains(DreamEscapeIds.PuzzleFinalBroadcast);

    private bool HasAllClues(params string[] clueIds) => clueIds.All(State.DiscoveredClues.Contains);

    private static bool Matches(IReadOnlyList<string> answer, params string[] expected) =>
        answer.SequenceEqual(expected, StringComparer.Ordinal);

    private void RequireInProgress()
    {
        if (State.Status != DreamEscapeStatus.InProgress)
        {
            throw new InvalidOperationException($"Dream escape is not active ({State.Status}).");
        }
    }

    private void RequireInProgressOrExitOpened()
    {
        if (State.Status is not (DreamEscapeStatus.InProgress or DreamEscapeStatus.ExitOpened))
        {
            throw new InvalidOperationException($"Dream escape cannot move in status {State.Status}.");
        }
    }
}
