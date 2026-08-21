using Godot;
using TownRpg.Application;
using TownRpg.Core.Domain;

namespace TownRpg.Presentation;

public partial class DreamAreaController3D : WorldArea3D, IDreamAreaRuntime
{
    [Export] public string AreaId { get; set; } = DreamEscapeIds.AreaClassroom;

    private readonly AreaCoordinateMapper3D _coordinateMapper = new(
        new Rect2(16f, 178f, 608f, 152f),
        new Rect2(-4.52f, -3.30f, 9.04f, 6.60f));
    private GameSession _session = null!;
    private DreamClassroomVisual3D _visual = null!;

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
        Player.Moved += OnPlayerMoved;
        var protagonistTexture = ResourceLoader.Load<Texture2D>(
            "res://assets/generated/dream_escape/pixel/protagonist-walk-4x4-v1.png");
        if (protagonistTexture is not null)
            Player.ConfigureCharacterFrames(DreamCharacterFrames.Create(protagonistTexture));
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
            ?? throw new InvalidOperationException("3D dream area refreshed without dream state.");
        _visual.ApplyState(dream);
    }

    protected override bool IsMarkerActive(InteractionMarker3D marker)
    {
        var dream = _session.State.DreamEscape!;
        return marker.TargetId switch
        {
            "pickup:dream.chalk" => !dream.SolvedPuzzles.Contains("dream.pickup.chalk"),
            "observe:target.classroom.window" or
            "observe:target.classroom.clock" or
            "observe:target.classroom.duty_roster" =>
                dream.SolvedPuzzles.Contains(DreamEscapeIds.PuzzleBlackboard),
            "target.classroom.map_cabinet" =>
                !dream.SolvedPuzzles.Contains(DreamEscapeIds.PuzzleMapCabinet),
            "target.classroom.class_photo" =>
                dream.SolvedPuzzles.Contains(DreamEscapeIds.PuzzleMapCabinet),
            _ => true
        };
    }

    protected override string BuildPrompt(InteractionMarker3D marker)
    {
        var button = UseGamepadPrompts ? "A" : "F";
        if (!string.IsNullOrEmpty(SelectedItemId) &&
            marker.TargetId.StartsWith("target.", StringComparison.Ordinal))
        {
            return $"[{button}] 对目标使用：{DreamInventoryPanel.ItemName(SelectedItemId)}";
        }
        return $"[{button}] {marker.Prompt}";
    }

    public DreamClassroomVisual3D ClassroomVisual => _visual;
    public PlayerController3D PlayerActor => Player;
    public QuarterTurnCameraRig CameraController => CameraRig;

    private void OnPlayerMoved(Vector3 worldPosition)
    {
        var stored = _coordinateMapper.ToStored(worldPosition);
        var dream = _session.State.DreamEscape!;
        dream.PositionX = stored.X;
        dream.PositionY = stored.Y;
    }
}
