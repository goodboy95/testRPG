using Godot;
using TownRpg.Application;
using TownRpg.Core.Domain;

namespace TownRpg.Presentation;

public partial class DreamAreaController3D : WorldArea3D, IDreamAreaRuntime, IDreamAreaExitTransition,
    IDreamAreaTransitionPose3D
{
    [Export] public string AreaId { get; set; } = DreamEscapeIds.AreaClassroom;

    private readonly AreaCoordinateMapper3D _coordinateMapper = new(
        new Rect2(16f, 178f, 608f, 152f),
        new Rect2(-5.7f, -4.2f, 11.4f, 8.4f));
    private GameSession _session = null!;
    private DreamClassroomVisual3D _visual = null!;
    private float _doorClosedYaw;
    private bool _doorTransitionPlaying;

    protected override bool InputBlocked => _session.Modal.IsBlocked;

    public override void _Ready()
    {
        _session = GetNode<GameSession>("/root/GameSession");
        _visual = GetNode<DreamClassroomVisual3D>("ClassroomVisual");
        base._Ready();
        var dream = _session.State.DreamEscape
            ?? throw new InvalidOperationException("3D dream area loaded without dream state.");
        var requestedStored = new Vector2(dream.PositionX, dream.PositionY);
        var requestedWorld = _coordinateMapper.ToWorld(requestedStored);
        var resolvedWorld = _visual.ResolveSpawn(requestedWorld);
        if (!resolvedWorld.IsEqualApprox(requestedWorld))
        {
            var resolvedStored = _coordinateMapper.ToStored(resolvedWorld);
            _session.MoveDreamToArea(AreaId, resolvedStored.X, resolvedStored.Y);
        }
        Player.GlobalPosition = resolvedWorld;
        var companion = GetNode<DreamCompanion3D>("XiaoyuCompanion");
        companion.FollowPositionResolver = _visual.ResolveSpawn;
        companion.FollowStepResolver = _visual.AdvanceCompanion;
        companion.FollowPositionIsBlocked = _visual.IsBlocked;
        companion.RoutedMotionPaused = () => _session.Modal.IsBlocked || _doorTransitionPlaying;
        companion.RefreshImmediate();
        CameraRig.SnapToTarget();
        Player.Moved += OnPlayerMoved;
        var protagonistTexture = ResourceLoader.Load<Texture2D>(
            "res://assets/generated/dream_escape/pixel/protagonist-walk-4x4-v1.png");
        if (protagonistTexture is not null)
            Player.ConfigureDefaultCharacterVisual(DreamCharacterFrames.Create(protagonistTexture));
        _visual.ApplyState(dream);
        _doorClosedYaw = _visual.DoorHinge.Rotation.Y;
        ApplySettings(_session.State.Settings);
    }

    public override void _ExitTree()
    {
        if (Player is not null) Player.Moved -= OnPlayerMoved;
        base._ExitTree();
    }

    public override void ApplySettings(PlayerSettings settings)
    {
        base.ApplySettings(settings);
        _visual?.ApplySettings(settings);
    }

    public void RefreshState()
    {
        var dream = _session.State.DreamEscape
            ?? throw new InvalidOperationException("3D dream area refreshed without dream state.");
        _visual.ApplyState(dream);
    }

    protected override bool IsMarkerActive(InteractionMarker3D marker)
    {
        var dream = _session.State.DreamEscape!;
        return marker.TargetId switch
        {
            "puzzle:window_latches" => !dream.SolvedPuzzles.Contains(DreamEscapeIds.PuzzleWindowLatches),
            "observe:target.classroom.maintenance_card" =>
                !dream.DiscoveredClues.Contains(DreamEscapeIds.ClueClassroomWindow),
            "observe:target.classroom.morning_schedule" =>
                !dream.DiscoveredClues.Contains(DreamEscapeIds.ClueClassroomClock),
            "observe:target.classroom.duty_note" =>
                !dream.DiscoveredClues.Contains(DreamEscapeIds.ClueClassroomDutyRoster),
            "pickup:dream.clock_hand" => !dream.SolvedPuzzles.Contains(DreamEscapeIds.PickupClockHand),
            "target.classroom.blackboard" => !dream.SolvedPuzzles.Contains(DreamEscapeIds.PuzzleBlackboard),
            "target.classroom.clock" => !dream.SolvedPuzzles.Contains(DreamEscapeIds.PuzzleClockRestore),
            "target.classroom.duty_roster" => !dream.SolvedPuzzles.Contains(DreamEscapeIds.PuzzleRosterSlider),
            "puzzle:teacher_desk" => !dream.SolvedPuzzles.Contains(DreamEscapeIds.PuzzleTeacherDesk),
            "target.classroom.map_cabinet" =>
                !dream.SolvedPuzzles.Contains(DreamEscapeIds.PuzzleMapCabinet),
            _ => true
        };
    }

    public DreamClassroomVisual3D ClassroomVisual => _visual;
    public PlayerController3D PlayerActor => Player;
    public ThirdPersonCameraRig CameraController => CameraRig;

    public async Task PlayExitTransitionAsync(string targetId, CancellationToken cancellationToken)
    {
        if (targetId != $"area:{DreamEscapeIds.AreaCorridor}" || _doorTransitionPlaying) return;
        _doorTransitionPlaying = true;
        SetRuntimeInputLocked(true);
        const float duration = 0.35f;
        var elapsed = 0f;
        try
        {
            while (elapsed < duration)
            {
                cancellationToken.ThrowIfCancellationRequested();
                await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame);
                elapsed += (float)GetProcessDeltaTime();
                var weight = Mathf.SmoothStep(0f, 1f, Mathf.Clamp(elapsed / duration, 0f, 1f));
                _visual.DoorHinge.Rotation = new Vector3(
                    _visual.DoorHinge.Rotation.X,
                    Mathf.LerpAngle(_doorClosedYaw, _doorClosedYaw - Mathf.DegToRad(72f), weight),
                    _visual.DoorHinge.Rotation.Z);
            }
        }
        catch
        {
            ResetExitTransition();
            throw;
        }
        finally
        {
            _doorTransitionPlaying = false;
        }
    }

    public void ResetExitTransition()
    {
        if (_visual?.DoorHinge is null) return;
        _visual.DoorHinge.Rotation = new Vector3(
            _visual.DoorHinge.Rotation.X,
            _doorClosedYaw,
            _visual.DoorHinge.Rotation.Z);
        SetRuntimeInputLocked(false);
    }

    public void ResetDoor() => ResetExitTransition();

    public float CaptureCameraDoorOffsetDegrees(Vector3 exitForward)
    {
        var doorForward = FlattenDirection(exitForward);
        var cameraForward = FlattenDirection(CameraRig.HorizontalForward);
        return Mathf.RadToDeg(doorForward.SignedAngleTo(cameraForward, Vector3.Up));
    }

    public void ApplyEntryPose(Vector3 playerFacing, float? cameraDoorOffsetDegrees)
    {
        var entryForward = FlattenDirection(playerFacing);
        Player.SetFacingImmediate(entryForward);
        var cameraForward = entryForward.Rotated(
            Vector3.Up,
            Mathf.DegToRad(cameraDoorOffsetDegrees ?? 0f));
        CameraRig.SetHorizontalForwardImmediate(cameraForward);
        CameraRig.SnapToTarget();
    }

    private void OnPlayerMoved(Vector3 worldPosition)
    {
        var stored = _coordinateMapper.ToStored(worldPosition);
        var dream = _session.State.DreamEscape!;
        dream.PositionX = stored.X;
        dream.PositionY = stored.Y;
    }

    private static Vector3 FlattenDirection(Vector3 direction)
    {
        var flattened = new Vector3(direction.X, 0f, direction.Z);
        return flattened.LengthSquared() <= 0.0001f ? Vector3.Forward : flattened.Normalized();
    }
}
