using Godot;
using TownRpg.Application;
using TownRpg.Core.Domain;

namespace TownRpg.Presentation;

public partial class DreamArchiveRoomController3D : WorldArea3D, IDreamAreaRuntime,
    IDreamAreaExitTransition, IDreamAreaTransitionPose3D, IDreamAreaTransientPresentation
{
    private readonly AreaCoordinateMapper3D _coordinateMapper = ArchiveRoomLayout3D.Mapper;
    private GameSession _session = null!;
    private DreamArchiveVisual3D _visual = null!;
    private DreamCompanion3D _companion = null!;
    private float _doorClosedYaw;
    private bool _doorTransitionPlaying;
    private bool _recorderKnown;

    protected override bool InputBlocked => _session.Modal.IsBlocked;

    public DreamArchiveVisual3D ArchiveVisual => _visual;
    public DreamCompanion3D Companion => _companion;
    public PlayerController3D PlayerActor => Player;
    public ThirdPersonCameraRig CameraController => CameraRig;
    public bool DoorTransitionPlaying => _doorTransitionPlaying;

    public override void _Ready()
    {
        _session = GetNode<GameSession>("/root/GameSession");
        _visual = GetNode<DreamArchiveVisual3D>("ArchiveVisual");
        foreach (var marker in GetNode<Node3D>("InteractionMarkers").GetChildren().OfType<InteractionMarker3D>())
            marker.Position = ArchiveRoomLayout3D.MarkerPositions.TryGetValue(marker.TargetId, out var position)
                ? position : throw new InvalidOperationException($"Unknown archive marker '{marker.TargetId}'.");
        base._Ready();
        _companion = GetNode<DreamCompanion3D>("XiaoyuCompanion");
        var dream = _session.State.DreamEscape
            ?? throw new InvalidOperationException("3D archive room loaded without dream state.");
        var requestedWorld = _coordinateMapper.ToWorld(new Vector2(dream.PositionX, dream.PositionY));
        var resolvedWorld = _visual.ResolveSpawn(requestedWorld);
        if (!resolvedWorld.IsEqualApprox(requestedWorld))
        {
            var resolvedStored = _coordinateMapper.ToStored(resolvedWorld);
            _session.MoveDreamToArea(DreamEscapeIds.AreaArchive, resolvedStored.X, resolvedStored.Y);
        }
        Player.GlobalPosition = resolvedWorld;
        CameraRig.SnapToTarget();
        Player.Moved += OnPlayerMoved;
        var protagonistTexture = ResourceLoader.Load<Texture2D>(
            "res://assets/generated/dream_escape/pixel/protagonist-walk-4x4-v1.png");
        if (protagonistTexture is not null)
            Player.ConfigureDefaultCharacterVisual(DreamCharacterFrames.Create(protagonistTexture));
        _visual.ApplyState(dream);
        _recorderKnown = dream.SolvedPuzzles.Contains(DreamEscapeIds.PuzzleArchiveRecorderRebuild);
        _companion.ConfigureBillboardHeight(1.6f);
        _companion.UseRoutedMotion = true;
        _companion.FollowPositionResolver = _visual.ResolveSpawn;
        _companion.FollowStepResolver = _visual.AdvanceCompanion;
        _companion.FollowPositionIsBlocked = _visual.IsBlocked;
        _companion.RoutedMotionPaused = () => _session.Modal.IsBlocked;
        _companion.RefreshImmediate(force: true);
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
            ?? throw new InvalidOperationException("3D archive room refreshed without dream state.");
        var recorderSolved = dream.SolvedPuzzles.Contains(DreamEscapeIds.PuzzleArchiveRecorderRebuild);
        _visual.ApplyState(dream);
        _companion.RefreshImmediate();
        if (recorderSolved && !_recorderKnown)
            _companion.PlayScriptedPose(ArchiveRoomLayout3D.RecorderCompanion, 1.5d, "operate", Vector3.Right);
        _recorderKnown = recorderSolved;
    }

    public void ApplyTransientState(DreamAreaTransientState state) => _visual.ApplyTransientState(state);

    protected override bool IsMarkerActive(InteractionMarker3D marker)
    {
        var dream = _session.State.DreamEscape!;
        return marker.TargetId switch
        {
            "observe:target.archive.registry_rule" =>
                !dream.DiscoveredClues.Contains(DreamEscapeIds.ClueArchiveRegistryRule),
            "observe:target.archive.registry_choir" =>
                !dream.DiscoveredClues.Contains(DreamEscapeIds.ClueArchiveRegistryChoir),
            "observe:target.archive.registry_broadcast" =>
                !dream.DiscoveredClues.Contains(DreamEscapeIds.ClueArchiveRegistryBroadcast),
            "observe:target.archive.registry_art" =>
                !dream.DiscoveredClues.Contains(DreamEscapeIds.ClueArchiveRegistryArt),
            "observe:target.archive.registry_track" =>
                !dream.DiscoveredClues.Contains(DreamEscapeIds.ClueArchiveRegistryTrack),
            "puzzle:archive_retrieval" =>
                !dream.SolvedPuzzles.Contains(DreamEscapeIds.PuzzleArchiveRetrieval),
            "pickup:dream.archive_index_strip" =>
                !dream.SolvedPuzzles.Contains(DreamEscapeIds.PickupArchiveIndexStrip),
            "observe:target.archive.fuse_route" =>
                dream.SolvedPuzzles.Contains(DreamEscapeIds.PuzzleArchiveRetrieval) &&
                !dream.DiscoveredClues.Contains(DreamEscapeIds.ClueArchiveFuseRoute),
            "assembly:archive_fuse" =>
                !dream.SolvedPuzzles.Contains(DreamEscapeIds.ProgressFuseAssembly),
            "target.archive.recorder" =>
                !dream.SolvedPuzzles.Contains(DreamEscapeIds.PuzzleArchiveRecorderRebuild),
            "puzzle:archive_work_timeline" =>
                !dream.SolvedPuzzles.Contains(DreamEscapeIds.PuzzleArchiveWorkTimeline),
            "hint:xiaoyu" => dream.XiaoyuJoined,
            _ => true
        };
    }

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
                _visual.DoorHinge.Rotation = new Vector3(0f,
                    Mathf.LerpAngle(_doorClosedYaw, _doorClosedYaw - Mathf.DegToRad(72f), weight), 0f);
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
        _visual.DoorHinge.Rotation = new Vector3(0f, _doorClosedYaw, 0f);
        SetRuntimeInputLocked(false);
    }

    public float CaptureCameraDoorOffsetDegrees(Vector3 exitForward)
    {
        var doorForward = FlattenDirection(exitForward);
        return Mathf.RadToDeg(doorForward.SignedAngleTo(
            FlattenDirection(CameraRig.HorizontalForward), Vector3.Up));
    }

    public void ApplyEntryPose(Vector3 playerFacing, float? cameraDoorOffsetDegrees)
    {
        var entryForward = FlattenDirection(playerFacing);
        Player.SetFacingImmediate(entryForward);
        CameraRig.SetHorizontalForwardImmediate(entryForward.Rotated(
            Vector3.Up, Mathf.DegToRad(cameraDoorOffsetDegrees ?? 0f)));
        CameraRig.SnapToTarget();
        _companion.RefreshImmediate();
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
