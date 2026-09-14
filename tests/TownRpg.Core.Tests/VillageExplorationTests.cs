using TownRpg.Core.Domain;
using TownRpg.Core.Services;
using TownRpg.Presentation;
using Xunit;

namespace TownRpg.Core.Tests;

public sealed class VillageExplorationTests
{
    [Theory]
    [InlineData(DreamEscapeStatus.ExitOpened, DreamEscapeStatus.CompletedEscape)]
    [InlineData(DreamEscapeStatus.EscapePendingWake, DreamEscapeStatus.CompletedEscape)]
    [InlineData(DreamEscapeStatus.PinPendingWake, DreamEscapeStatus.CompletedPin)]
    [InlineData(DreamEscapeStatus.CompletedEscape, DreamEscapeStatus.CompletedEscape)]
    [InlineData(DreamEscapeStatus.CompletedPin, DreamEscapeStatus.CompletedPin)]
    public void WakeIsAtomicAndIdempotent(DreamEscapeStatus source, DreamEscapeStatus expected)
    {
        var game = GameState.CreateDreamPrologue();
        game.DreamEscape!.Status = source;
        var service = new VillageExplorationService(game);
        var inventory = game.Inventory.Money;
        Assert.True(service.BeginWake());
        Assert.Equal(expected, game.DreamEscape.Status);
        Assert.Empty(game.DreamEscape.Items);
        Assert.Equal(inventory, game.Inventory.Money);
        Assert.Equal(VillageExplorationState.HomeLocation, game.Player.LocationId);
        Assert.True(service.CompleteIntroduction());
        game.VillageExploration!.X = 12;
        Assert.False(service.BeginWake());
        Assert.False(service.CompleteIntroduction());
        Assert.Equal(12, game.VillageExploration.X);
        var copy = SaveGameSerializer.Deserialize(SaveGameSerializer.Serialize(game));
        Assert.True(copy.VillageExploration!.WakeIntroductionComplete);
    }

    [Fact]
    public void InvalidWakeLeavesStateUntouched()
    {
        var game = GameState.CreateDreamPrologue();
        var before = SaveGameSerializer.Serialize(game);
        Assert.Throws<InvalidOperationException>(() => new VillageExplorationService(game).BeginWake());
        Assert.Equal(before, SaveGameSerializer.Serialize(game));
        Assert.Throws<InvalidOperationException>(() => new VillageExplorationService(new()).BeginWake());
    }

    [Fact]
    public void RealityPoseAndFixturesRoundTripAndDoNotAdvanceDay()
    {
        var game = GameState.CreateDreamPrologue(); game.DreamEscape!.Status = DreamEscapeStatus.ExitOpened;
        var service = new VillageExplorationService(game); service.BeginWake();
        service.Capture(VillageExplorationState.VillageLocation, new() { X = 22, Y = 1, Z = -3, CameraYaw = 72,
            Doors = new() { ["bedroom"] = true }, Lights = new() { ["living"] = true } });
        new TimeService(game).AdvanceMovementSeconds(10000, false);
        Assert.Equal(480, game.World.MinuteOfDay);
        Assert.Throws<InvalidOperationException>(() => new TimeService(game).Sleep());
        var copy = SaveGameSerializer.Deserialize(SaveGameSerializer.Serialize(game));
        Assert.Equal(VillageExplorationState.VillageLocation, copy.Player.LocationId);
        Assert.Equal((22f, 1f, -3f, 72f), (copy.VillageExploration!.X, copy.VillageExploration.Y, copy.VillageExploration.Z, copy.VillageExploration.CameraYaw));
        Assert.True(copy.VillageExploration.Doors["bedroom"]);
        Assert.True(copy.VillageExploration.Lights["living"]);
        Assert.False(copy.VillageExploration.WakeIntroductionComplete);
    }

    [Fact]
    public void OldTerminalAndLegacyRemainReadableButOldActiveDreamDoesNot()
    {
        var legacy = GameState.CreateVerticalSlice();
        Assert.Null(SaveGameSerializer.Deserialize(SaveGameSerializer.Serialize(legacy)).VillageExploration);
        var game = GameState.CreateDreamPrologue(); game.DreamEscape!.ContentVersion = 5;
        Assert.Throws<NotSupportedException>(() => SaveGameSerializer.Deserialize(SaveGameSerializer.Serialize(game)));
        game.DreamEscape.Status = DreamEscapeStatus.CompletedEscape; game.DreamEscape.Items.Clear();
        game = SaveGameSerializer.Deserialize(SaveGameSerializer.Serialize(game));
        Assert.True(new VillageExplorationService(game).BeginWake());
        Assert.NotNull(SaveGameSerializer.Deserialize(SaveGameSerializer.Serialize(game)).VillageExploration);
    }

    [Fact]
    public void CorruptRealitySectionsAreRejectedWithoutPartialCapture()
    {
        var game = GameState.CreateDreamPrologue(); game.DreamEscape!.Status = DreamEscapeStatus.ExitOpened;
        var service = new VillageExplorationService(game); service.BeginWake();
        var before = SaveGameSerializer.Serialize(game);
        Assert.Throws<InvalidDataException>(() => service.Capture("old_house", new()));
        Assert.Throws<InvalidDataException>(() => service.Capture(VillageExplorationState.HomeLocation, new() { X = float.NaN }));
        Assert.Throws<InvalidDataException>(() => service.Capture(VillageExplorationState.HomeLocation, new() { Doors = new() { ["unknown"] = true } }));
        Assert.Equal(before, SaveGameSerializer.Serialize(game));
        game.DreamEscape.Status = DreamEscapeStatus.InProgress;
        Assert.Throws<InvalidDataException>(() => SaveGameSerializer.Deserialize(SaveGameSerializer.Serialize(game)));
    }

    [Fact]
    public void DoorPersistenceUsesLastStableEndpoint()
    {
        var door = new HomeDoorMotion(HomeLayout3D.Layout.Doors[1]);
        Assert.True(door.Request(new(100,100))); door.Step(.1f); Assert.False(door.StableOpen);
        door.Step(1); Assert.True(door.StableOpen);
        Assert.True(door.Request(new(100,100))); door.Step(.1f); Assert.True(door.StableOpen);
        door.Restore(door.StableOpen); Assert.True(door.IsOpen); Assert.False(door.IsMoving);
        Assert.Equal(VillageExplorationState.DoorIds.Order(), HomeLayout3D.Layout.Doors.Select(d=>d.Id).Order());
        Assert.Equal(VillageExplorationState.LightIds.Order(), HomeLayout3D.Layout.Lights.Select(d=>d.Id).Order());
    }
}
