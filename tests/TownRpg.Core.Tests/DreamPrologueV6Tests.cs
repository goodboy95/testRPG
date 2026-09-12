using TownRpg.Core.Domain;
using TownRpg.Core.Services;

namespace TownRpg.Core.Tests;

public sealed class DreamPrologueV6Tests
{
    [Fact]
    public void StoryCompletionIsGatedAtomicAndIdempotentWithoutWorldChanges()
    {
        var game = GameState.CreateDreamPrologue();
        var service = new DreamEscapeService(game);
        var before = SaveGameSerializer.Serialize(game);
        Assert.False(service.CompleteStory(DreamPrologueCatalog.EventId("meeting")).Changed);
        Assert.Equal(before, SaveGameSerializer.Serialize(game));
        Assert.Throws<ArgumentOutOfRangeException>(() => service.CompleteStory("dream.prologue.unknown"));
        game.DreamEscape!.SolvedPuzzles.UnionWith(DreamEscapeIds.Puzzles);
        foreach (var id in new[] { "intro" }.Concat(DreamPrologueCatalog.Groups.Select(group => group.Id)))
        {
            var eventId = DreamPrologueCatalog.EventId(id);
            Assert.True(service.CompleteStory(eventId).Changed);
            var committed = SaveGameSerializer.Serialize(game);
            Assert.False(service.CompleteStory(eventId).Changed);
            Assert.Equal(committed, SaveGameSerializer.Serialize(game));
        }
        Assert.Equal(13, game.CompletedEvents.Count);
        Assert.Equal(13, game.AppliedOutcomes.Count);
        Assert.Empty(game.World.Facts);
        Assert.Empty(game.World.Evidence);
        Assert.Empty(game.Investigation.Leads);
        Assert.Empty(game.Relationships);
    }

    [Fact]
    public void TwelveGroupsPartitionAllActivePuzzleNodes()
    {
        Assert.Equal(12, DreamPrologueCatalog.Groups.Count);
        var nodes = DreamPrologueCatalog.Groups.SelectMany(group => group.Puzzles).ToArray();
        Assert.Equal(nodes.Length, nodes.Distinct().Count());
        Assert.True(DreamEscapeIds.Puzzles.SetEquals(nodes));
        Assert.Empty(DreamEscapeIds.Puzzles.Intersect(RetiredDreamEscapeIds.Puzzles));
        Assert.Empty(DreamEscapeIds.Clues.Intersect(RetiredDreamEscapeIds.Clues));
    }

    [Fact]
    public void StageHasTwoFrontCandidatesAndExactlyOneDualViewSolution()
    {
        var front = new List<string[]>();
        var both = new List<string[]>();
        for (var a = 1; a <= 4; a++)
        for (var b = 1; b <= 4; b++)
        for (var c = 1; c <= 4; c++)
        for (var d = 1; d <= 4; d++)
        {
            string[] answer = [a.ToString(), b.ToString(), c.ToString(), d.ToString(), "window", "figure", "bell"];
            var observed = DreamStageRules.Observe(answer);
            if (observed.UpperFrame && observed.LowerFrame && observed.Rails) front.Add(answer);
            if (observed.Aligned) both.Add(answer);
        }
        Assert.Equal(2, front.Count);
        Assert.Equal(new[] { "3", "1", "4", "2", "window", "figure", "bell" }, Assert.Single(both));
        Assert.False(DreamStageRules.Observe(["4", "2", "3", "1", "window", "figure", "bell"]).SideMark);
    }

    [Theory]
    [InlineData(DreamEscapeStatus.InProgress)]
    [InlineData(DreamEscapeStatus.ExitOpened)]
    public void V5MechanicsProgressRequiresRestart(DreamEscapeStatus status)
    {
        var game = GameState.CreateDreamPrologue();
        game.DreamEscape!.ContentVersion = 5;
        game.DreamEscape.Status = status;
        Assert.Throws<NotSupportedException>(() => SaveGameSerializer.Deserialize(SaveGameSerializer.Serialize(game)));
    }

    [Theory]
    [InlineData(DreamEscapeStatus.CompletedEscape)]
    [InlineData(DreamEscapeStatus.CompletedPin)]
    public void V5CompletedEmptyTerminalRemainsCompatible(DreamEscapeStatus status)
    {
        var game = GameState.CreateDreamPrologue();
        game.DreamEscape!.ContentVersion = 5;
        game.DreamEscape.Status = status;
        game.DreamEscape.Items.Clear();
        var restored = SaveGameSerializer.Deserialize(SaveGameSerializer.Serialize(game));
        Assert.Equal(status, restored.DreamEscape!.Status);
    }

    [Fact]
    public void DoorVoiceIsAvailableBeforeMeetingWithoutUnlockingHints()
    {
        var game = GameState.CreateDreamPrologue();
        var service = new DreamEscapeService(game);
        game.DreamEscape!.UnlockedAreas.Add(DreamEscapeIds.AreaMusic);
        service.MoveToArea(DreamEscapeIds.AreaMusic, 100, 100);
        Assert.True(service.Observe("target.music.door_voice").Changed);
        Assert.False(service.Observe("target.music.door_voice").Changed);
        Assert.False(game.DreamEscape.XiaoyuJoined);
        Assert.Empty(service.AvailableHintPuzzles());
        Assert.False(service.Observe("target.music.side_wave").Changed);
    }

    [Fact]
    public void StagePreviewCannotWriteProgressOrCreateWorldEvidence()
    {
        var game = GameState.CreateDreamPrologue();
        var before = SaveGameSerializer.Serialize(game);
        Assert.True(DreamStageRules.Observe(["3", "1", "4", "2", "window", "figure", "bell"]).Aligned);
        Assert.Equal(before, SaveGameSerializer.Serialize(game));
    }

    [Fact]
    public void RetiredInputsFailLoudlyAndCannotBeRelabelledAsNewPuzzles()
    {
        var game = GameState.CreateDreamPrologue();
        var service = new DreamEscapeService(game);
        foreach (var puzzle in RetiredDreamEscapeIds.Puzzles)
            Assert.Throws<ArgumentOutOfRangeException>(() => service.SubmitPuzzle(puzzle, "372"));
        Assert.Throws<ArgumentOutOfRangeException>(() => service.Observe("target.music.unknown"));
    }
}
