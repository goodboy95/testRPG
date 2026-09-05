using Godot;
using TownRpg.Application;
using TownRpg.Core.Domain;

namespace TownRpg.Presentation;

public partial class DreamMusicRoomController3D : WorldArea3D, IDreamAreaRuntime, IDreamAreaExitTransition,
    IDreamAreaTransitionPose3D
{
    private readonly AreaCoordinateMapper3D _coordinateMapper = new(
        new Rect2(16f, 54f, 608f, 272f),
        new Rect2(-5.25f, -3.75f, 10.5f, 7.5f));
    private GameSession _session = null!;
    private DreamMusicVisual3D _visual = null!;
    private DreamCompanion3D _companion = null!;
    private AudioStreamPlayer _ambience = null!;
    private AudioStreamPlayer _mechanismCue = null!;
    private readonly HashSet<string> _knownSolved = new(StringComparer.Ordinal);
    private float _doorClosedYaw;
    private bool _doorTransitionPlaying;

    protected override bool InputBlocked => _session.Modal.IsBlocked;

    public DreamMusicVisual3D MusicVisual => _visual;
    public DreamCompanion3D Companion => _companion;
    public PlayerController3D PlayerActor => Player;
    public ThirdPersonCameraRig CameraController => CameraRig;
    public AudioStreamPlayer AmbiencePlayer => _ambience;
    public AudioStreamPlayer MechanismCuePlayer => _mechanismCue;
    public bool DoorTransitionPlaying => _doorTransitionPlaying;

    public override void _Ready()
    {
        _session = GetNode<GameSession>("/root/GameSession");
        _visual = GetNode<DreamMusicVisual3D>("MusicVisual");
        _ambience = GetNode<AudioStreamPlayer>("AmbiencePlayer");
        _mechanismCue = GetNode<AudioStreamPlayer>("MechanismCuePlayer");
        base._Ready();
        _companion = GetNode<DreamCompanion3D>("XiaoyuCompanion");
        var dream = _session.State.DreamEscape
            ?? throw new InvalidOperationException("3D music room loaded without dream state.");
        var requestedStored = new Vector2(dream.PositionX, dream.PositionY);
        var requestedWorld = _coordinateMapper.ToWorld(requestedStored);
        var resolvedWorld = _visual.ResolveSpawn(requestedWorld);
        if (!resolvedWorld.IsEqualApprox(requestedWorld))
        {
            var resolvedStored = _coordinateMapper.ToStored(resolvedWorld);
            _session.MoveDreamToArea(DreamEscapeIds.AreaMusic, resolvedStored.X, resolvedStored.Y);
        }
        Player.GlobalPosition = resolvedWorld;
        CameraRig.SnapToTarget();
        Player.Moved += OnPlayerMoved;
        var protagonistTexture = ResourceLoader.Load<Texture2D>(
            "res://assets/generated/dream_escape/pixel/protagonist-walk-4x4-v1.png");
        if (protagonistTexture is not null)
            Player.ConfigureDefaultCharacterVisual(DreamCharacterFrames.Create(protagonistTexture));
        _visual.ApplyState(dream);
        _knownSolved.UnionWith(dream.SolvedPuzzles);
        _ambience.Finished += OnAmbienceFinished;
        _ambience.Play();
        _doorClosedYaw = _visual.DoorHinge.Rotation.Y;
        ApplySettings(_session.State.Settings);
    }

    public override void _ExitTree()
    {
        if (Player is not null) Player.Moved -= OnPlayerMoved;
        if (_ambience is not null) _ambience.Finished -= OnAmbienceFinished;
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
            ?? throw new InvalidOperationException("3D music room refreshed without dream state.");
        PlayNewMechanismCue(dream);
        _visual.ApplyState(dream);
        _companion.RefreshImmediate();
    }

    protected override bool IsMarkerActive(InteractionMarker3D marker)
    {
        var dream = _session.State.DreamEscape!;
        return marker.TargetId switch
        {
            "target.music.score" => !dream.SolvedPuzzles.Contains(DreamEscapeIds.PuzzleMusicScore),
            "puzzle:music_cage" => !dream.SolvedPuzzles.Contains(DreamEscapeIds.PuzzleMusicCage),
            "pickup:dream.rubber_mallet" => PickupActive(dream, DreamEscapeIds.PickupRubberMallet),
            "pickup:dream.listening_cone" => PickupActive(dream, DreamEscapeIds.PickupListeningCone),
            "pickup:dream.metronome_weight" => PickupActive(dream, DreamEscapeIds.PickupMetronomeWeight),
            "pickup:dream.torn_piano_roll" => PickupActive(dream, DreamEscapeIds.PickupTornPianoRoll),
            "pickup:dream.wax_patch" => PickupActive(dream, DreamEscapeIds.PickupWaxPatch),
            "target.music.metronome" => !dream.SolvedPuzzles.Contains(DreamEscapeIds.PuzzleMusicMetronome),
            "puzzle:music_tuning_forks" =>
                dream.XiaoyuJoined && !dream.SolvedPuzzles.Contains(DreamEscapeIds.PuzzleMusicTuningForks),
            "puzzle:music_piano_roll" =>
                dream.XiaoyuJoined && !dream.SolvedPuzzles.Contains(DreamEscapeIds.PuzzleMusicPianoRoll),
            "puzzle:music_acoustic_wall" =>
                dream.XiaoyuJoined && !dream.SolvedPuzzles.Contains(DreamEscapeIds.PuzzleMusicAcousticWall),
            "puzzle:music_attendance_reader" =>
                !dream.SolvedPuzzles.Contains(DreamEscapeIds.PuzzleMusicAttendanceReader),
            "target.music.piano_bench" =>
                !dream.SolvedPuzzles.Contains(DreamEscapeIds.PuzzleMusicPianoBenchCache),
            "observe:target.music.tempo_notice" => dream.XiaoyuJoined &&
                !dream.DiscoveredClues.Contains(DreamEscapeIds.ClueMusicTempoNotice),
            "observe:target.music.fork_reference" => dream.XiaoyuJoined &&
                dream.SolvedPuzzles.Contains(DreamEscapeIds.PuzzleMusicCage) &&
                !dream.DiscoveredClues.Contains(DreamEscapeIds.ClueMusicForkReference),
            "observe:target.music.roll_template" => dream.XiaoyuJoined &&
                !dream.DiscoveredClues.Contains(DreamEscapeIds.ClueMusicRollTemplate),
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
                _visual.DoorHinge.Rotation = new Vector3(
                    _visual.DoorHinge.Rotation.X,
                    Mathf.LerpAngle(_doorClosedYaw, _doorClosedYaw + Mathf.DegToRad(72f), weight),
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
            _visual.DoorHinge.Rotation.X, _doorClosedYaw, _visual.DoorHinge.Rotation.Z);
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
        var cameraForward = entryForward.Rotated(Vector3.Up,
            Mathf.DegToRad(cameraDoorOffsetDegrees ?? 0f));
        CameraRig.SetHorizontalForwardImmediate(cameraForward);
        CameraRig.SnapToTarget();
        _companion.RefreshImmediate();
    }

    private static bool PickupActive(DreamEscapeState state, string progressId) =>
        state.XiaoyuJoined && !state.SolvedPuzzles.Contains(progressId);

    private void OnAmbienceFinished()
    {
        if (IsInsideTree()) _ambience.Play();
    }

    private void PlayNewMechanismCue(DreamEscapeState dream)
    {
        var cuePath = new[]
        {
            (DreamEscapeIds.PuzzleMusicAcousticWall,
                "res://assets/generated/dream_escape/audio/music-acoustic-wall.wav"),
            (DreamEscapeIds.PuzzleMusicTuningForks,
                "res://assets/generated/dream_escape/audio/music-tuning-forks-a3-c4-e4-a4.wav"),
            (DreamEscapeIds.PuzzleMusicMetronome,
                "res://assets/generated/dream_escape/audio/music-metronome-72.wav")
        }.FirstOrDefault(cue => dream.SolvedPuzzles.Contains(cue.Item1) &&
                                 !_knownSolved.Contains(cue.Item1)).Item2;
        _knownSolved.Clear();
        _knownSolved.UnionWith(dream.SolvedPuzzles);
        if (string.IsNullOrEmpty(cuePath)) return;
        _mechanismCue.Stream = ResourceLoader.Load<AudioStream>(cuePath)
            ?? throw new InvalidOperationException($"Could not load music-room cue '{cuePath}'.");
        _mechanismCue.Play();
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
