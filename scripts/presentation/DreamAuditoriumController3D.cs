using Godot;
using TownRpg.Application;
using TownRpg.Core.Domain;

namespace TownRpg.Presentation;

public partial class DreamAuditoriumController3D : WorldArea3D, IDreamAreaRuntime,
    IDreamAreaExitTransition, IDreamAreaTransitionPose3D, IDreamAreaTransientPresentation
{
    private static readonly Vector3 LeftLeverCompanionPosition = new(-6.75f, 0f, -1.25f);
    private readonly AreaCoordinateMapper3D _coordinateMapper = new(
        new Rect2(16f, 54f, 608f, 272f),
        new Rect2(-8.5f, -5.5f, 17f, 11f));
    private GameSession _session = null!;
    private DreamAuditoriumVisual3D _visual = null!;
    private DreamCompanion3D _companion = null!;
    private float _doorClosedYaw;
    private bool _doorTransitionPlaying;

    protected override bool InputBlocked => _session.Modal.IsBlocked;

    public DreamAuditoriumVisual3D AuditoriumVisual => _visual;
    public DreamCompanion3D Companion => _companion;
    public PlayerController3D PlayerActor => Player;
    public ThirdPersonCameraRig CameraController => CameraRig;
    public bool DoorTransitionPlaying => _doorTransitionPlaying;

    public override void _Ready()
    {
        _session = GetNode<GameSession>("/root/GameSession");
        _visual = GetNode<DreamAuditoriumVisual3D>("AuditoriumVisual");
        base._Ready();
        _companion = GetNode<DreamCompanion3D>("XiaoyuCompanion");
        var dream = _session.State.DreamEscape
            ?? throw new InvalidOperationException("3D auditorium loaded without dream state.");
        var requestedWorld = _coordinateMapper.ToWorld(new Vector2(dream.PositionX, dream.PositionY));
        var resolvedWorld = _visual.ResolveSpawn(requestedWorld);
        if (!resolvedWorld.IsEqualApprox(requestedWorld))
        {
            var resolvedStored = _coordinateMapper.ToStored(resolvedWorld);
            _session.MoveDreamToArea(DreamEscapeIds.AreaAuditorium, resolvedStored.X, resolvedStored.Y);
        }
        Player.GlobalPosition = resolvedWorld;
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
            ?? throw new InvalidOperationException("3D auditorium refreshed without dream state.");
        _visual.ApplyState(dream);
        _companion.RefreshImmediate();
    }

    public void ApplyTransientState(DreamAreaTransientState state)
    {
        _visual.ApplyTransientState(state);
        _companion.SetHeldPose(state.XiaoyuHoldingExitLever, LeftLeverCompanionPosition);
    }

    protected override bool IsMarkerActive(InteractionMarker3D marker)
    {
        var dream = _session.State.DreamEscape!;
        return marker.TargetId switch
        {
            "observe:target.auditorium.line_set_notches" =>
                !dream.DiscoveredClues.Contains(DreamEscapeIds.ClueAuditoriumLineSetNotches),
            "pickup:dream.curtain_counterweight" =>
                !dream.SolvedPuzzles.Contains(DreamEscapeIds.PickupCurtainCounterweight),
            "target.auditorium.line_set" =>
                !dream.SolvedPuzzles.Contains(DreamEscapeIds.PuzzleAuditoriumLineSetBalance),
            "observe:target.auditorium.projection_order" =>
                dream.SolvedPuzzles.Contains(DreamEscapeIds.PuzzleAuditoriumLineSetBalance) &&
                !dream.DiscoveredClues.Contains(DreamEscapeIds.ClueAuditoriumProjectionOrder),
            "pickup:dream.projector_lens" =>
                !dream.SolvedPuzzles.Contains(DreamEscapeIds.PickupProjectorLens),
            "target.auditorium.projector" =>
                !dream.SolvedPuzzles.Contains(DreamEscapeIds.PuzzleAuditoriumShadowProjector),
            "observe:target.auditorium.tape_order" =>
                dream.SolvedPuzzles.Contains(DreamEscapeIds.PuzzleAuditoriumShadowProjector) &&
                !dream.DiscoveredClues.Contains(DreamEscapeIds.ClueAuditoriumTapeOrder),
            "puzzle:auditorium_master_tape_splice" =>
                !dream.SolvedPuzzles.Contains(DreamEscapeIds.PuzzleAuditoriumMasterTapeSplice),
            "target.auditorium.master_player" =>
                !dream.InstalledParts.Contains(DreamEscapeIds.SlotAuditoriumMasterTape),
            "puzzle:auditorium_light_console" =>
                !dream.SolvedPuzzles.Contains(DreamEscapeIds.PuzzleAuditoriumLightConsole),
            "puzzle:auditorium_sound_console" =>
                !dream.SolvedPuzzles.Contains(DreamEscapeIds.PuzzleAuditoriumSoundConsole),
            "puzzle:auditorium_broadcast_console" =>
                !dream.SolvedPuzzles.Contains(DreamEscapeIds.PuzzleAuditoriumBroadcastConsole),
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
                    Mathf.LerpAngle(_doorClosedYaw, _doorClosedYaw + Mathf.DegToRad(72f), weight), 0f);
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
