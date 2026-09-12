using TownRpg.Core.Domain;
using TownRpg.Core.Services;
using static TownRpg.Core.Domain.DreamEscapeIds;

namespace TownRpg.Core.Tests;

public sealed class DreamThoughtRulesTests
{
    private static readonly DreamActionResult Failure = DreamActionResult.NoChange("DREAM_FEEDBACK_WRONG_ANSWER");

    [Fact]
    public void RecallOnlyUsesObservedMaterialsAndDoesNotMutateProgress()
    {
        var game = GameState.CreateDreamPrologue();
        var state = game.DreamEscape!;
        var none = DreamThoughtRules.Select(state, PuzzleClockRestore, Failure)!;
        Assert.Equal(DreamThoughtKnowledge.None, none.Knowledge);
        Assert.Equal(2, none.TextKeys.Count);
        state.DiscoveredClues.Add(ClueClassroomClock);
        var before = SaveGameSerializer.Serialize(game);
        for (var repeat = 0; repeat < 5; repeat++)
        {
            var partial = DreamThoughtRules.Select(state, PuzzleClockRestore, Failure)!;
            Assert.Equal(DreamThoughtKnowledge.Partial, partial.Knowledge);
            Assert.Equal(DreamThoughtRules.Pair(ClueClassroomClock, "RECALL"), partial.TextKeys);
        }
        Assert.Equal(before, SaveGameSerializer.Serialize(game));
        state.DiscoveredClues.Add(ClueClassroomDutyRoster);
        state.InstalledParts.UnionWith([SlotClassroomClockHand, SlotClassroomClockKey]);
        Assert.Equal(DreamThoughtKnowledge.Complete, DreamThoughtRules.Select(state, PuzzleClockRestore, Failure)!.Knowledge);
        Assert.DoesNotContain(state.Items, i => i is ItemClockHand or ItemClockKey);
    }

    [Fact]
    public void ConsumedMaterialsRemainKnownAndJoinedCompanionDoesNotAdvanceHelp()
    {
        var state = GameState.CreateDreamPrologue().DreamEscape!;
        state.SolvedPuzzles.Add(ProgressFuseAssembly);
        state.DiscoveredClues.Add(ClueArchiveFuseRoute);
        Assert.Equal(DreamThoughtKnowledge.Complete, DreamThoughtRules.Select(state, ProgressFuseAssembly, Failure)!.Knowledge);
        state.XiaoyuJoined = true;
        var result = DreamThoughtRules.Select(state, ProgressFuseAssembly, Failure)!;
        Assert.Equal(DreamThoughtRules.Pair(ProgressFuseAssembly, "REACT"), result.TextKeys);
        Assert.Empty(state.HintLevels);
    }

    [Fact]
    public void SuccessfulDuplicateAndTechnicalResultsAreNotPuzzleFailures()
    {
        var state = GameState.CreateDreamPrologue().DreamEscape!;
        foreach (var key in new[] { "PUZZLE_ALREADY_SOLVED", "CLUE_ALREADY_RECORDED", "PART_ALREADY_INSTALLED", "TARGET_NOT_HERE", "HINT_MAXIMUM" })
            Assert.Null(DreamThoughtRules.Select(state, PuzzleWindowLatches, DreamActionResult.NoChange("DREAM_FEEDBACK_" + key)));
        Assert.Null(DreamThoughtRules.Select(state, PuzzleWindowLatches, DreamActionResult.Success("DREAM_FEEDBACK_WRONG_ANSWER")));
        Assert.Equal(2, DreamThoughtRules.Select(state, null, Failure)!.TextKeys.Count);
    }

    [Fact]
    public void EveryProfileHasValidKnowledgeSourcesAndTwoVariants()
    {
        Assert.All(Puzzles, id => Assert.Contains(id, DreamThoughtRules.Profiles.Keys));
        var valid = Clues.Concat(Items).Concat(InstalledPartSlots).Concat(Puzzles).Concat(PickupProgress).ToHashSet();
        foreach (var profile in DreamThoughtRules.Profiles)
        {
            Assert.All(profile.Value.SelectMany(m => new[] { m.Id }.Concat(m.RememberedBy)), id => Assert.Contains(id, valid));
            var empty = new DreamEscapeState { Items = [] };
            Assert.Equal(2, DreamThoughtRules.Select(empty, profile.Key, Failure)!.TextKeys.Count);
        }
    }

    [Fact]
    public void TheSameToolHasDifferentRecallAtTheBlackboardAndMusicScore()
    {
        var state = new DreamEscapeState { Items = [ItemChalk] };
        var board = DreamThoughtRules.Select(state, PuzzleBlackboard, Failure)!;
        var score = DreamThoughtRules.Select(state, PuzzleMusicScore, Failure)!;
        Assert.Equal(DreamThoughtKnowledge.Complete, board.Knowledge);
        Assert.Equal(DreamThoughtKnowledge.Complete, score.Knowledge);
        Assert.DoesNotContain(board.TextKeys, key => score.TextKeys.Contains(key));
        Assert.Equal(2, score.TextKeys.Count);
    }
}
