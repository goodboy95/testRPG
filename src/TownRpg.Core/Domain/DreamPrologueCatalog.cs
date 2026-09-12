namespace TownRpg.Core.Domain;

public sealed record DreamPuzzleGroup(string Id, params string[] Puzzles);

/// <summary>Player-facing groups share the existing atomic rule state; no second progress counter is saved.</summary>
public static class DreamPrologueCatalog
{
    public static readonly IReadOnlyList<DreamPuzzleGroup> Groups =
    [
        new("window", DreamEscapeIds.PuzzleWindowLatches),
        new("morning", DreamEscapeIds.PuzzleClockRestore, DreamEscapeIds.PuzzleRosterSlider),
        new("programme", DreamEscapeIds.PuzzleBlackboard, DreamEscapeIds.PuzzleTeacherDesk),
        new("meeting", DreamEscapeIds.PuzzleMusicScore, DreamEscapeIds.PuzzleMusicCage),
        new("calibration", DreamEscapeIds.PuzzleMusicChannelCalibration),
        new("score", DreamEscapeIds.PuzzleMusicPianoRoll, DreamEscapeIds.PuzzleMusicAcousticWall),
        new("registry", DreamEscapeIds.PuzzleArchiveRetrieval),
        new("photo", DreamEscapeIds.PuzzleMapCabinet, DreamEscapeIds.PuzzleMusicBackstagePhoto),
        new("timeline", DreamEscapeIds.PuzzleArchiveRecorderRebuild, DreamEscapeIds.PuzzleArchiveWorkTimeline),
        new("stage", DreamEscapeIds.PuzzleAuditoriumStageAlignment),
        new("tape", DreamEscapeIds.PuzzleAuditoriumTapeReconstruction),
        new("handoff", DreamEscapeIds.PuzzleAuditoriumHandoffPlan, DreamEscapeIds.PuzzleExit)
    ];

    public static string EventId(string groupId) => $"dream.prologue.{groupId}";
    public static string TextKey(string id) => "DREAM_V6_" + id.Replace("dream.", "", StringComparison.Ordinal)
        .Replace('.', '_').ToUpperInvariant();

    public static bool StoryReady(string eventId, DreamEscapeState state)
    {
        if (state.Status is not (DreamEscapeStatus.InProgress or DreamEscapeStatus.ExitOpened)) return false;
        if (eventId == EventId("intro")) return true;
        var group = Groups.FirstOrDefault(group => EventId(group.Id) == eventId);
        return group is not null && group.Puzzles.All(state.SolvedPuzzles.Contains);
    }
}
