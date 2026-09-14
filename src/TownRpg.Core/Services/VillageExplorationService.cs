using TownRpg.Core.Domain;

namespace TownRpg.Core.Services;

public sealed class VillageExplorationService(GameState game)
{
    public bool BeginWake()
    {
        if (game.VillageExploration is not null) return false;
        var dream = game.DreamEscape ?? throw new InvalidOperationException("A dream is required to wake.");
        if (dream.Status is not (DreamEscapeStatus.ExitOpened or DreamEscapeStatus.EscapePendingWake or
            DreamEscapeStatus.PinPendingWake or DreamEscapeStatus.CompletedEscape or DreamEscapeStatus.CompletedPin))
            throw new InvalidOperationException("The dream has not reached a wake boundary.");
        dream.Status = dream.Status is DreamEscapeStatus.PinPendingWake or DreamEscapeStatus.CompletedPin
            ? DreamEscapeStatus.CompletedPin : DreamEscapeStatus.CompletedEscape;
        dream.Items.Clear();
        game.VillageExploration = new();
        game.Player.LocationId = VillageExplorationState.HomeLocation;
        game.World.Day = 1;
        game.World.MinuteOfDay = 8 * 60;
        return true;
    }

    public bool CompleteIntroduction()
    {
        var state = game.VillageExploration ?? throw new InvalidOperationException("Not awake.");
        if (state.WakeIntroductionComplete) return false;
        state.WakeIntroductionComplete = true;
        return true;
    }

    public void Capture(string location, VillageExplorationState snapshot)
    {
        if (game.VillageExploration is null) throw new InvalidOperationException("Not awake.");
        Validate(location, snapshot);
        snapshot.WakeIntroductionComplete = game.VillageExploration.WakeIntroductionComplete;
        game.Player.LocationId = location;
        game.VillageExploration = snapshot;
    }

    public static void Validate(string location, VillageExplorationState state)
    {
        if (location is not (VillageExplorationState.HomeLocation or VillageExplorationState.VillageLocation) ||
            !float.IsFinite(state.X) || !float.IsFinite(state.Y) || !float.IsFinite(state.Z) ||
            !float.IsFinite(state.FacingRadians) || !float.IsFinite(state.CameraYaw) ||
            state.Doors is null || state.Lights is null ||
            state.Doors.Keys.Any(key => !VillageExplorationState.DoorIds.Contains(key)) ||
            state.Lights.Keys.Any(key => !VillageExplorationState.LightIds.Contains(key)))
            throw new InvalidDataException("Invalid village exploration state.");
    }
}
