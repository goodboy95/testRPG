using System.Text.Json;
using TownRpg.Core.Domain;
using TownRpg.Core.Services;

namespace TownRpg.Core.Tests;

public sealed class DreamEscapeServiceTests
{
    [Fact]
    public void CreateDreamPrologue_StartsWithOnlyPinAndNoRealityRelationships()
    {
        var state = GameState.CreateDreamPrologue();

        Assert.Equal(DreamEscapeIds.Location, state.Player.LocationId);
        Assert.Empty(state.Relationships);
        Assert.NotNull(state.DreamEscape);
        Assert.Equal(DreamEscapeStatus.InProgress, state.DreamEscape.Status);
        Assert.Equal(DreamEscapeIds.AreaClassroom, state.DreamEscape.CurrentAreaId);
        Assert.Equal(DreamEscapeState.NewGameClassroomPositionX, state.DreamEscape.PositionX);
        Assert.Equal(DreamEscapeState.NewGameClassroomPositionY, state.DreamEscape.PositionY);
        Assert.Equal([DreamEscapeIds.ItemPin], state.DreamEscape.Items);
        Assert.Contains(DreamEscapeIds.AreaCorridor, state.DreamEscape.UnlockedAreas);
        Assert.Contains(DreamEscapeIds.AreaClassroom, state.DreamEscape.UnlockedAreas);
    }

    [Fact]
    public void ClassroomFlow_RequiresClueAndCorrectAnswerBeforeRoomTwoUnlocks()
    {
        var (state, service) = NewService();
        Move(service, DreamEscapeIds.AreaClassroom);

        Assert.True(service.Pickup(DreamEscapeIds.ItemChalk).Changed);
        Assert.False(service.SubmitPuzzle(DreamEscapeIds.PuzzleTeacherDesk, "3", "7", "2").Changed);
        Assert.False(service.Observe("target.classroom.window").Changed);
        Assert.False(service.UseItem(DreamEscapeIds.ItemPin, "target.classroom.blackboard").Changed);
        Assert.True(service.UseItem(DreamEscapeIds.ItemChalk, "target.classroom.blackboard").Changed);
        Assert.False(service.SubmitPuzzle(DreamEscapeIds.PuzzleTeacherDesk, "3", "7", "2").Changed);
        Assert.True(service.Observe("target.classroom.window").Changed);
        Assert.True(service.Observe("target.classroom.clock").Changed);
        Assert.True(service.Observe("target.classroom.duty_roster").Changed);
        Assert.False(service.SubmitPuzzle(DreamEscapeIds.PuzzleTeacherDesk, "3", "2", "7").Changed);
        Assert.True(service.SubmitPuzzle(DreamEscapeIds.PuzzleTeacherDesk, "3", "7", "2").Changed);
        Assert.Contains(DreamEscapeIds.ItemRoomTwoKey, state.DreamEscape!.Items);

        Move(service, DreamEscapeIds.AreaCorridor);
        Assert.True(service.UseItem(
            DreamEscapeIds.ItemRoomTwoKey,
            "target.corridor.room_two_door").Changed);
        Assert.Contains(DreamEscapeIds.AreaMusic, state.DreamEscape.UnlockedAreas);
        Assert.DoesNotContain(DreamEscapeIds.ItemRoomTwoKey, state.DreamEscape.Items);
    }

    [Fact]
    public void FullPuzzleChain_AllowsRequiredBacktrackingAndOpensExit()
    {
        var (state, service) = NewService();

        SolveThroughArchive(state, service);

        Move(service, DreamEscapeIds.AreaClassroom);
        Assert.True(service.UseItem(
            DreamEscapeIds.ItemMaintenanceKey,
            "target.classroom.map_cabinet").Changed);
        Assert.True(service.UseItem(
            DreamEscapeIds.ItemRedFilter,
            "target.classroom.class_photo").Changed);
        Assert.Contains(DreamEscapeIds.ClueSeatCoordinates, state.DreamEscape!.DiscoveredClues);

        Move(service, DreamEscapeIds.AreaMusic);
        Assert.True(service.SubmitPuzzle(
            DreamEscapeIds.PuzzleAttendance,
            "B2", "D1", "A3", "C4").Changed);

        Move(service, DreamEscapeIds.AreaArchive);
        Assert.True(service.UseItem(
            DreamEscapeIds.ItemTapeReel,
            "target.archive.recorder").Changed);
        Assert.True(service.UseItem(
            DreamEscapeIds.ItemRecorderCrank,
            "target.archive.recorder").Changed);

        Move(service, DreamEscapeIds.AreaCorridor);
        Assert.True(service.SubmitPuzzle(
            DreamEscapeIds.PuzzleRoomFourDoor,
            "curtain", "clock", "bell", "register").Changed);
        Move(service, DreamEscapeIds.AreaAuditorium);
        Assert.True(service.SubmitPuzzle(DreamEscapeIds.PuzzleFinalDesk, "3", "7", "2").Changed);
        Assert.True(service.SubmitPuzzle(
            DreamEscapeIds.PuzzleFinalInstruments,
            "woodblock", "piano", "triangle", "handbell").Changed);
        Assert.True(service.SubmitPuzzle(
            DreamEscapeIds.PuzzleFinalBroadcast,
            "curtain", "clock", "bell", "register").Changed);
        Assert.True(service.PrepareLeftLever().Changed);
        Assert.True(service.OpenExit().Changed);

        Assert.Equal(DreamEscapeStatus.ExitOpened, state.DreamEscape.Status);
        Assert.Empty(state.Inventory.Items);
        Assert.False(service.OpenExit().Changed);
    }

    [Fact]
    public void ListeningPuzzle_RequiresXiaoyuAndAllThreeObservations()
    {
        var (_, service) = NewService();
        SolveClassroom(service);
        Move(service, DreamEscapeIds.AreaMusic);

        Assert.False(service.Observe("target.music.listen_window").Changed);
        Assert.True(service.UseItem(DreamEscapeIds.ItemChalk, "target.music.score").Changed);
        Assert.True(service.SubmitPuzzle(DreamEscapeIds.PuzzleMusicCage, "1", "4", "2", "3").Changed);
        Assert.True(service.Observe("target.music.listen_window").Changed);
        Assert.True(service.Observe("target.music.listen_lectern").Changed);
        Assert.False(service.SubmitPuzzle(
            DreamEscapeIds.PuzzleListening,
            "woodblock", "piano", "triangle", "handbell").Changed);
        Assert.True(service.Observe("target.music.listen_door").Changed);
        Assert.True(service.SubmitPuzzle(
            DreamEscapeIds.PuzzleListening,
            "woodblock", "piano", "triangle", "handbell").Changed);
    }

    [Fact]
    public void HintsStayLockedUntilXiaoyuJoins()
    {
        var (state, _) = NewService();
        state.DreamEscape!.HintLevels[DreamEscapeIds.PuzzleBlackboard] = 2;
        var restored = SaveGameSerializer.Deserialize(SaveGameSerializer.Serialize(state));
        var service = new DreamEscapeService(restored);

        Assert.Empty(service.AvailableHintPuzzles());
        Assert.False(service.AdvanceHint(DreamEscapeIds.PuzzleBlackboard).Changed);
        Assert.Equal(2, restored.DreamEscape!.HintLevels[DreamEscapeIds.PuzzleBlackboard]);

        SolveClassroom(service);
        Move(service, DreamEscapeIds.AreaMusic);
        service.UseItem(DreamEscapeIds.ItemChalk, "target.music.score");
        service.SubmitPuzzle(DreamEscapeIds.PuzzleMusicCage, "1", "4", "2", "3");

        Assert.Contains(DreamEscapeIds.PuzzleListening, service.AvailableHintPuzzles());
    }

    [Fact]
    public void HintsAdvanceToThreeWithoutChangingPuzzleProgress()
    {
        var (state, service) = NewService();
        SolveClassroom(service);
        Move(service, DreamEscapeIds.AreaMusic);
        service.UseItem(DreamEscapeIds.ItemChalk, "target.music.score");
        service.SubmitPuzzle(DreamEscapeIds.PuzzleMusicCage, "1", "4", "2", "3");

        Assert.True(service.AdvanceHint(DreamEscapeIds.PuzzleListening).Changed);
        Assert.True(service.AdvanceHint(DreamEscapeIds.PuzzleListening).Changed);
        Assert.True(service.AdvanceHint(DreamEscapeIds.PuzzleListening).Changed);
        Assert.False(service.AdvanceHint(DreamEscapeIds.PuzzleListening).Changed);

        Assert.Equal(3, state.DreamEscape!.HintLevels[DreamEscapeIds.PuzzleListening]);
        Assert.DoesNotContain(DreamEscapeIds.PuzzleListening, state.DreamEscape.SolvedPuzzles);
    }

    [Theory]
    [InlineData(DreamEscapeIds.AreaCorridor)]
    [InlineData(DreamEscapeIds.AreaClassroom)]
    [InlineData(DreamEscapeIds.AreaMusic)]
    [InlineData(DreamEscapeIds.AreaArchive)]
    [InlineData(DreamEscapeIds.AreaAuditorium)]
    public void PinWake_ClearsDreamItemsAndCannotBeRepeated(string areaId)
    {
        var (state, service) = NewService();
        state.DreamEscape!.UnlockedAreas.UnionWith(DreamEscapeIds.Areas);
        Move(service, areaId);
        state.DreamEscape.Items.Add(DreamEscapeIds.ItemChalk);

        Assert.True(service.UseItem(DreamEscapeIds.ItemPin, "target.self").Changed);
        Assert.Equal(DreamEscapeStatus.PinPendingWake, state.DreamEscape.Status);
        Assert.Empty(state.DreamEscape.Items);
        Assert.True(service.CompletePinWake().Changed);
        Assert.Equal(DreamEscapeStatus.CompletedPin, state.DreamEscape.Status);
        Assert.Equal("old_house", state.Player.LocationId);
        Assert.Empty(state.Inventory.Items);
        Assert.False(service.CompletePinWake().Changed);
    }

    [Fact]
    public void SaveRoundTrip_PreservesDreamProgressAndSupportsLegacySaveWithoutDreamSection()
    {
        var (state, service) = NewService();
        SolveClassroom(service);
        Move(service, DreamEscapeIds.AreaMusic);
        service.UseItem(DreamEscapeIds.ItemChalk, "target.music.score");
        service.SubmitPuzzle(DreamEscapeIds.PuzzleMusicCage, "1", "4", "2", "3");
        service.AdvanceHint(DreamEscapeIds.PuzzleListening);

        var restored = SaveGameSerializer.Deserialize(SaveGameSerializer.Serialize(state));

        Assert.NotNull(restored.DreamEscape);
        Assert.Equal(DreamEscapeIds.AreaMusic, restored.DreamEscape.CurrentAreaId);
        Assert.Contains(DreamEscapeIds.AreaMusic, restored.DreamEscape.UnlockedAreas);
        Assert.Equal(1, restored.DreamEscape.HintLevels[DreamEscapeIds.PuzzleListening]);

        var legacyJson = SaveGameSerializer.Serialize(GameState.CreateVerticalSlice());
        using var document = JsonDocument.Parse(legacyJson);
        var root = document.RootElement;
        var gameProperties = root.GetProperty("game").EnumerateObject()
            .Where(property => property.Name != "dreamEscape")
            .ToDictionary(property => property.Name, property => property.Value.Clone());
        var withoutDream = JsonSerializer.Serialize(new
        {
            formatVersion = 1,
            game = gameProperties
        });

        var legacy = SaveGameSerializer.Deserialize(withoutDream);
        Assert.Null(legacy.DreamEscape);
        Assert.Equal("old_house", legacy.Player.LocationId);
    }

    [Fact]
    public void Deserialize_LegacyDreamWithoutCoordinatesKeepsLegacyCoordinateDefaults()
    {
        var json = SaveGameSerializer.Serialize(GameState.CreateDreamPrologue());
        using var document = JsonDocument.Parse(json);
        var root = document.RootElement;
        var game = root.GetProperty("game");
        var dream = game.GetProperty("dreamEscape").EnumerateObject()
            .Where(property => property.Name is not "positionX" and not "positionY")
            .ToDictionary(property => property.Name, property => property.Value.Clone());
        var gameProperties = game.EnumerateObject()
            .Where(property => property.Name != "dreamEscape")
            .ToDictionary(property => property.Name, property => property.Value.Clone());
        gameProperties["dreamEscape"] = JsonSerializer.SerializeToElement(dream);
        var legacyJson = JsonSerializer.Serialize(new
        {
            formatVersion = 1,
            game = gameProperties
        });

        var restored = SaveGameSerializer.Deserialize(legacyJson);

        Assert.NotNull(restored.DreamEscape);
        Assert.Equal(320f, restored.DreamEscape.PositionX);
        Assert.Equal(280f, restored.DreamEscape.PositionY);
    }

    [Fact]
    public void SaveValidation_RejectsUnknownDreamItemAndHintLevel()
    {
        var state = GameState.CreateDreamPrologue();
        state.DreamEscape!.Items.Add("dream.unknown");
        Assert.Throws<InvalidDataException>(() =>
            SaveGameSerializer.Deserialize(SaveGameSerializer.Serialize(state)));

        state = GameState.CreateDreamPrologue();
        state.DreamEscape!.HintLevels[DreamEscapeIds.PuzzleBlackboard] = 4;
        Assert.Throws<InvalidDataException>(() =>
            SaveGameSerializer.Deserialize(SaveGameSerializer.Serialize(state)));
    }

    private static (GameState State, DreamEscapeService Service) NewService()
    {
        var state = GameState.CreateDreamPrologue();
        return (state, new DreamEscapeService(state));
    }

    private static void SolveClassroom(DreamEscapeService service)
    {
        Move(service, DreamEscapeIds.AreaClassroom);
        service.Pickup(DreamEscapeIds.ItemChalk);
        service.UseItem(DreamEscapeIds.ItemChalk, "target.classroom.blackboard");
        service.Observe("target.classroom.window");
        service.Observe("target.classroom.clock");
        service.Observe("target.classroom.duty_roster");
        service.SubmitPuzzle(DreamEscapeIds.PuzzleTeacherDesk, "3", "7", "2");
        Move(service, DreamEscapeIds.AreaCorridor);
        service.UseItem(DreamEscapeIds.ItemRoomTwoKey, "target.corridor.room_two_door");
    }

    private static void SolveThroughArchive(GameState state, DreamEscapeService service)
    {
        SolveClassroom(service);
        Move(service, DreamEscapeIds.AreaMusic);
        service.UseItem(DreamEscapeIds.ItemChalk, "target.music.score");
        service.SubmitPuzzle(DreamEscapeIds.PuzzleMusicCage, "1", "4", "2", "3");
        service.Observe("target.music.listen_window");
        service.Observe("target.music.listen_lectern");
        service.Observe("target.music.listen_door");
        service.SubmitPuzzle(
            DreamEscapeIds.PuzzleListening,
            "woodblock", "piano", "triangle", "handbell");
        Assert.True(state.DreamEscape!.XiaoyuJoined);
        Move(service, DreamEscapeIds.AreaArchive);
        service.Observe("target.archive.rules");
        service.SubmitPuzzle(
            DreamEscapeIds.PuzzleArchiveOrder,
            "choir", "broadcast", "art", "track");
    }

    private static void Move(DreamEscapeService service, string areaId) =>
        service.MoveToArea(areaId, 320f, 260f);
}
