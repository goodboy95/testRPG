using TownRpg.Core.Domain;
using static TownRpg.Core.Domain.DreamEscapeIds;

namespace TownRpg.Core.Services;

public enum DreamThoughtKnowledge { None, Partial, Complete }
public sealed record DreamThoughtSelection(DreamThoughtKnowledge Knowledge, IReadOnlyList<string> TextKeys);
public sealed record DreamThoughtMemory(string Id, params string[] RememberedBy)
{
    public bool IsKnown(DreamEscapeState state) => new[] { Id }.Concat(RememberedBy).Any(id =>
        state.DiscoveredClues.Contains(id) || state.Items.Contains(id) ||
        state.InstalledParts.Contains(id) || state.SolvedPuzzles.Contains(id));
}

/// <summary>Read-only narrative selection. Remembering materials never changes puzzle eligibility.</summary>
public static class DreamThoughtRules
{
    public static readonly IReadOnlyDictionary<string, DreamThoughtMemory[]> Profiles = BuildProfiles();
    private static readonly HashSet<string> Failures = new(StringComparer.Ordinal)
    {
        "WRONG_ANSWER", "ITEM_MISSING", "ITEM_DOES_NOT_FIT", "PREREQUISITES_MISSING",
        "CLOCK_PARTS_MISSING", "ROSTER_SLIDER_MISSING", "NEED_BLACKBOARD_CLUE", "NEED_CLASSROOM_OBSERVATIONS",
        "NEED_SCORE_CLUE", "NEED_XIAOYU", "METRONOME_WEIGHT_MISSING", "TUNING_TOOLS_MISSING",
        "PIANO_ROLL_PARTS_MISSING", "NEED_MUSIC_TRANSLATIONS", "ARCHIVE_INDEX_MISSING", "FUSE_PARTS_MISSING",
        "RECORDER_PARTS_MISSING", "TAPE_PARTS_MISSING", "MASTER_TAPE_NOT_PLAYING", "NEED_SEAT_COORDINATES",
        "NEED_TAPE_FIRST", "AREA_LOCKED", "ARCHIVE_DRAWER_CLOSED", "CAGE_CLOSED", "KEY_NOT_READY",
        "MAP_TOOL_NOT_READY", "MASTER_TAPE_NOT_READY", "PROJECTOR_REWARD_CLOSED", "RECORDER_NOT_READY",
        "WINDOW_CACHE_CLOSED", "STAGE_PARTS_MISSING", "FINAL_PANELS_INCOMPLETE", "XIAOYU_NOT_READY",
        "ROOM_THREE_KEY_NOT_READY"
    };

    public static DreamThoughtSelection? Select(DreamEscapeState state, string? context, DreamActionResult result)
    {
        const string prefix = "DREAM_FEEDBACK_";
        if (result.Changed || !result.FeedbackKey.StartsWith(prefix, StringComparison.Ordinal) ||
            !Failures.Contains(result.FeedbackKey[prefix.Length..])) return null;
        var profile = context is not null && Profiles.TryGetValue(context, out var found) ? found : [];
        var known = profile.Where(memory => memory.IsKnown(state)).ToArray();
        var knowledge = known.Length == 0 ? DreamThoughtKnowledge.None :
            known.Length == profile.Length ? DreamThoughtKnowledge.Complete : DreamThoughtKnowledge.Partial;
        var profileId = context is not null && Profiles.ContainsKey(context) ? context : "general";
        var keys = state.XiaoyuJoined ? Pair(profileId, "REACT") : known.Length == 0
            ? Pair(profileId, "NONE") : known.SelectMany(memory => Pair(RecallId(profileId, memory.Id), "RECALL")).ToArray();
        return new(knowledge, keys);
    }

    public static string[] Pair(string id, string kind) => [Key(id, kind, 1), Key(id, kind, 2)];
    private static string Key(string id, string kind, int variant) =>
        "DREAM_THOUGHT_" + id.Replace("dream.", "", StringComparison.Ordinal).Replace('.', '_').ToUpperInvariant() + $"_{kind}_{variant}";
    private static string RecallId(string context, string memory) => (context, memory) switch
    {
        (PuzzleMusicScore, ItemChalk) => "music.score.chalk",
        (PuzzleClockRestore, ClueClassroomDutyRoster) => "classroom.clock_restore.duty_note",
        (PuzzleTeacherDesk, ClueClassroomClock) => "classroom.teacher_desk.schedule",
        (PuzzleTeacherDesk, ClueClassroomDutyRoster) => "classroom.teacher_desk.duty_note",
        _ => memory
    };
    public static IEnumerable<string> AllTextKeys => Profiles.Keys.Append("general")
        .SelectMany(id => Pair(id, "NONE").Concat(Pair(id, "REACT")))
        .Concat(Profiles.SelectMany(p => p.Value.SelectMany(m => Pair(RecallId(p.Key, m.Id), "RECALL")))).Distinct();

    private static Dictionary<string, DreamThoughtMemory[]> BuildProfiles()
    {
        var items = new Dictionary<string, DreamThoughtMemory[]>(StringComparer.Ordinal)
        {
            [PuzzleBlackboard] = [new(ItemChalk, PuzzleBlackboard)],
            [PuzzleClockRestore] = [new(ItemClockHand, SlotClassroomClockHand, PuzzleClockRestore), new(ItemClockKey, SlotClassroomClockKey, PuzzleClockRestore)],
            [PuzzleRosterSlider] = [new(ItemRosterSlider, SlotClassroomRosterSlider, PuzzleRosterSlider)],
            [PuzzleMapCabinet] = [new(ItemMaintenanceKey, PuzzleMapCabinet)],
            [PuzzleMusicScore] = [new(ItemChalk, PuzzleMusicScore)],
            [PuzzleMusicChannelCalibration] = [new(ItemRubberMallet, PuzzleMusicChannelCalibration), new(ItemListeningCone, PuzzleMusicChannelCalibration), new(ItemMetronomeWeight, SlotMusicMetronomeWeight)],
            [PuzzleMusicPianoRoll] = [new(ItemTornPianoRoll, SlotMusicPianoRoll), new(ItemWaxPatch, PuzzleMusicPianoRoll)],
            [PuzzleMusicBackstagePhoto] = [new(ItemClassPhotoNegative, PuzzleMusicBackstagePhoto), new(ItemAttendanceOverlay, PuzzleMusicBackstagePhoto), new(ItemRedFilter, PuzzleMusicBackstagePhoto)],
            [PuzzleArchiveRetrieval] = [new(ItemArchiveIndexStrip, PickupArchiveIndexStrip)],
            [PuzzleArchiveRecorderRebuild] = [new(ItemCalibrationReel, SlotArchiveRecorderReel), new(ItemRecorderCrank, SlotArchiveRecorderCrank), new(ItemRepairedFuse, SlotArchiveRecorderFuse)],
            [PuzzleAuditoriumStageAlignment] = [new(ItemCurtainCounterweight, SlotAuditoriumCurtainCounterweight), new(ItemProjectorLens, SlotAuditoriumProjectorLens)],
            [PuzzleAuditoriumTapeReconstruction] = [new(ItemTapeFragmentClassroom, PuzzleAuditoriumTapeReconstruction), new(ItemTapeFragmentMusic, PuzzleAuditoriumTapeReconstruction), new(ItemTapeFragmentArchive, PuzzleAuditoriumTapeReconstruction), new(ItemSplicingTape, PuzzleAuditoriumTapeReconstruction), new(ItemMasterReelCore, PuzzleAuditoriumTapeReconstruction)],
            [PuzzleExit] = [new(ClueHandoffCircuit), new(PuzzleAuditoriumHandoffPlan)],
            [ProgressMetronomeReady] = [new(ItemMetronomeWeight, SlotMusicMetronomeWeight)],
            [ProgressFuseAssembly] = [new(ClueArchiveFuseRoute), new(ItemFuseTube, ProgressFuseAssembly), new(ItemCopperWire, ProgressFuseAssembly)],
            ["door"] = [],
            ["master_player"] = [new(ItemRestoredMasterTape, SlotAuditoriumMasterTape)]
        };
        foreach (var puzzle in Puzzles)
            items[puzzle] = PuzzleInputClues.GetValueOrDefault(puzzle, []).Select(clue => new DreamThoughtMemory(clue))
                .Concat(items.GetValueOrDefault(puzzle, [])).ToArray();
        return items;
    }
}
