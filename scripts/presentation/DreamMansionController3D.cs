using Godot;
using TownRpg.Application;
using TownRpg.Core.Domain;

namespace TownRpg.Presentation;

public partial class DreamMansionController3D : WorldArea3D, IDreamAreaRuntime,
    IDreamAreaExitTransition, IDreamAreaTransitionPose3D
{
    [Export] public string AreaId { get; set; } = DreamEscapeIds.AreaCorridor;

    private readonly DreamMansionCoordinateMapper3D _coordinateMapper = new();
    private GameSession _session = null!;
    private DreamMansionVisual3D _visual = null!;
    private bool _doorTransitionPlaying;

    protected override bool InputBlocked => _session.Modal.IsBlocked;

    public DreamMansionVisual3D MansionVisual => _visual;
    public PlayerController3D PlayerActor => Player;
    public ThirdPersonCameraRig CameraController => CameraRig;
    public bool DoorTransitionPlaying => _doorTransitionPlaying;

    public override void _Ready()
    {
        _session = GetNode<GameSession>("/root/GameSession");
        _visual = GetNode<DreamMansionVisual3D>("MansionVisual");
        foreach (var marker in GetNode<Node3D>("InteractionMarkers").GetChildren().OfType<InteractionMarker3D>())
            marker.Position = MansionLayout3D.MarkerPositions[marker.TargetId];
        base._Ready();
        var dream = _session.State.DreamEscape
            ?? throw new InvalidOperationException("3D dream mansion loaded without dream state.");
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
            ?? throw new InvalidOperationException("3D dream mansion refreshed without dream state.");
        _visual.ApplyState(dream);
    }

    protected override bool IsMarkerActive(InteractionMarker3D marker)
    {
        var dream = _session.State.DreamEscape!;
        return marker.TargetId switch
        {
            $"area:{DreamEscapeIds.AreaMusic}" =>
                dream.UnlockedAreas.Contains(DreamEscapeIds.AreaMusic),
            "target.corridor.room_two_door" =>
                !dream.UnlockedAreas.Contains(DreamEscapeIds.AreaMusic),
            $"area:{DreamEscapeIds.AreaArchive}" =>
                dream.UnlockedAreas.Contains(DreamEscapeIds.AreaArchive),
            "target.corridor.room_three_door" =>
                !dream.UnlockedAreas.Contains(DreamEscapeIds.AreaArchive),
            $"area:{DreamEscapeIds.AreaAuditorium}" =>
                dream.UnlockedAreas.Contains(DreamEscapeIds.AreaAuditorium),
            "target.corridor.room_four_door" =>
                !dream.UnlockedAreas.Contains(DreamEscapeIds.AreaAuditorium),
            _ => true
        };
    }

    public async Task PlayExitTransitionAsync(string targetId, CancellationToken cancellationToken)
    {
        if (!targetId.StartsWith("area:", StringComparison.Ordinal) || _doorTransitionPlaying) return;
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
                _visual.SetDoorOpenProgress(targetId, weight);
            }
            _visual.SetDoorOpenProgress(targetId, 1f);
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
        _visual?.ResetDoors();
        _doorTransitionPlaying = false;
        SetRuntimeInputLocked(false);
    }

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
