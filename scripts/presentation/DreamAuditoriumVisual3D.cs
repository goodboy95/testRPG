using Godot;
using TownRpg.Core.Domain;

namespace TownRpg.Presentation;

public partial class DreamAuditoriumVisual3D : DreamLateRoomVisual3D
{
    private static readonly CollisionSpec[] Specs = AuditoriumLayout3D.Collisions
        .Select(s => new CollisionSpec(s.Name, s.Position, s.Size, s.Furniture)).ToArray();
    private static readonly Vector3[] Waypoints = AuditoriumLayout3D.Waypoints();
    private GroundWaypointNavigator3D? _navigator;
    private readonly List<Node3D> _discs = [];
    private Node3D _installedCounterweight = null!;
    private Node3D _unsplicedTape = null!;
    private Node3D _exitClosed = null!;
    private Node3D _leftHandle = null!;
    private Node3D _rightHandle = null!;
    public Vector3 AdvanceCompanion(Vector3 current, Vector3 desired, float distance) =>
        (_navigator ??= new GroundWaypointNavigator3D(Waypoints, IsBlocked, ResolveSpawn)).Advance(current, desired, distance);

    private readonly List<Node3D> _lineWeights = [];
    private Node3D _doorHinge = null!;
    private Node3D _curtain = null!;
    private Node3D _projectionClue = null!;
    private Node3D _lens = null!;
    private Node3D _projectorSolved = null!;
    private Node3D _tapeOrder = null!;
    private Node3D _spliceReel = null!;
    private Node3D _masterTape = null!;
    private Node3D _masterPlayerLight = null!;
    private Node3D _lightConsole = null!;
    private Node3D _soundConsole = null!;
    private Node3D _broadcastConsole = null!;
    private Node3D _leftLeverLight = null!;
    private Node3D _exitOpen = null!;
    private Node3D _counterweightPickup = null!;
    private Node3D _lensPickup = null!;
    private bool _lineSetSolved;
    private int[] _lineValues = [1, 1, 1, 1];

    protected override IReadOnlyList<CollisionSpec> CollisionSpecs => Specs;
    protected override IReadOnlyList<Vector3> SafeWaypoints => Waypoints;
    public override Rect2 WalkableBounds => AuditoriumLayout3D.WalkableBounds;
    public override bool IsBlocked(Vector3 position) => AuditoriumLayout3D.IsBlocked(position);
    public override Vector3 ResolveSpawn(Vector3 requested) => AuditoriumLayout3D.ResolveSpawn(requested);
    public override Node3D DoorHinge => _doorHinge;

    public bool CurtainRaised => _lineSetSolved;
    public bool ProjectorSolved => _projectorSolved.Visible;
    public bool MasterTapeInstalled => _masterTape.Visible;
    public bool ExitLeverHeld => _leftLeverLight.Visible;
    public bool ExitOpen => _exitOpen.Visible;
    public int SolvedConsoleCount => new[] { _lightConsole, _soundConsole, _broadcastConsole }
        .Count(node => node.Visible);
    public int VisiblePickupCount => new[] { _counterweightPickup, _lensPickup }.Count(node => node.Visible);
    public IReadOnlyList<int> LineSetValues => _lineValues;

    public override void _Process(double delta)
    {
        if (_masterTape.Visible) _masterTape.RotateX((float)delta * 2.2f);
    }

    public override void ApplyState(DreamEscapeState state)
    {
        _lineSetSolved = state.SolvedPuzzles.Contains(DreamEscapeIds.PuzzleAuditoriumStageAlignment);
        ApplyLineValues(_lineSetSolved ? [3, 1, 4, 2] : [1, 1, 1, 1]);
        _curtain.Scale = new Vector3(1, _lineSetSolved ? .18f : 1, 1);
        ApplyDiscs(_lineSetSolved ? [0, 1, 2] : [0, 0, 0]);
        _installedCounterweight.Visible = state.InstalledParts.Contains(DreamEscapeIds.SlotAuditoriumCurtainCounterweight) || _lineSetSolved;
        _projectionClue.Visible = true;
        _lens.Visible = state.InstalledParts.Contains(DreamEscapeIds.SlotAuditoriumProjectorLens) ||
                        state.SolvedPuzzles.Contains(DreamEscapeIds.PuzzleAuditoriumStageAlignment);
        _projectorSolved.Visible = state.SolvedPuzzles.Contains(DreamEscapeIds.PuzzleAuditoriumStageAlignment);
        _tapeOrder.Visible = _projectorSolved.Visible;
        _spliceReel.Visible = state.SolvedPuzzles.Contains(DreamEscapeIds.PuzzleAuditoriumTapeReconstruction);
        _masterTape.Visible = state.InstalledParts.Contains(DreamEscapeIds.SlotAuditoriumMasterTape);
        _masterPlayerLight.Visible = _masterTape.Visible;
        _spliceReel.Visible &= !_masterTape.Visible;
        _unsplicedTape.Visible = !state.SolvedPuzzles.Contains(DreamEscapeIds.PuzzleAuditoriumTapeReconstruction);
        _lightConsole.Visible = state.Status == DreamEscapeStatus.ExitOpened;
        _soundConsole.Visible = state.Status == DreamEscapeStatus.ExitOpened;
        _broadcastConsole.Visible = state.SolvedPuzzles.Contains(DreamEscapeIds.PuzzleAuditoriumHandoffPlan);
        _exitOpen.Visible = state.Status == DreamEscapeStatus.ExitOpened;
        _exitClosed.Visible = !_exitOpen.Visible;
        _rightHandle.RotationDegrees = new(_exitOpen.Visible ? -35 : 0, 0, 0);
        _counterweightPickup.Visible = !state.SolvedPuzzles.Contains(DreamEscapeIds.PickupCurtainCounterweight);
        _lensPickup.Visible = !state.SolvedPuzzles.Contains(DreamEscapeIds.PickupProjectorLens);
    }

    public override void ApplyTransientState(DreamAreaTransientState state)
    {
        _leftLeverLight.Visible = state.XiaoyuHoldingExitLever;
        _leftHandle.RotationDegrees = new(state.XiaoyuHoldingExitLever ? -35 : 0, 0, 0);
        if (!_exitOpen.Visible)
        {
            _soundConsole.Visible = state.HandoffStep >= 2;
            _lightConsole.Visible = state.HandoffStep >= 3;
        }
        if (_lineSetSolved) return;
        if (state.PreviewPuzzleId == DreamEscapeIds.PuzzleAuditoriumStageAlignment &&
            state.PreviewValues is { Count: 7 } values)
        {
            ApplyLineValues(values.Take(4).Select(value => int.TryParse(value, out var parsed) ? parsed : 1).ToArray());
            ApplyDiscs(values.Skip(4).Select(value => value switch { "figure" => 1, "bell" => 2, _ => 0 }).ToArray());
        }
        else
        {
            ApplyLineValues([1, 1, 1, 1]);
            ApplyDiscs([0, 0, 0]);
        }
    }

    protected override void BuildRoom()
    {
        var model = GetNode<Node3D>("../AuditoriumModel");
        ConfigureMeshes(model);
        Node3D Required(string name) => model.FindChild(name, true, false) as Node3D
            ?? throw new InvalidOperationException($"Auditorium v2 node missing: {name}");
        _doorHinge = Required("Door_South_Hinge");
        _curtain = Required("MainCurtain");
        _projectionClue = Required("Projection_Clue");
        _lens = Required("Projector_Lens");
        _projectorSolved = Required("Projector_Solved");
        _tapeOrder = Required("Tape_Order");
        _spliceReel = Required("Splice_Reel");
        _masterTape = Required("Master_Tape");
        _masterPlayerLight = Required("Master_Player_Light");
        _lightConsole = Required("Light_Ready");
        _soundConsole = Required("Sound_Ready");
        _broadcastConsole = Required("Broadcast_Ready");
        _leftLeverLight = Required("Left_Lever_Held");
        _leftLeverLight.Visible = false;
        _exitOpen = Required("Exit_Open");
        _exitClosed = Required("StageExitClosed");
        _counterweightPickup = Required("Pickup_Counterweight");
        _lensPickup = Required("Pickup_Lens");
        _installedCounterweight = Required("Installed_Counterweight");
        _unsplicedTape = Required("Tape_Unspliced");
        _leftHandle = Required("Left_Handle");
        _rightHandle = Required("Right_Handle");
        for (var i = 0; i < 4; i++) _lineWeights.Add(Required($"Line_Weight{i}"));
        for (var i = 0; i < 3; i++) _discs.Add(Required($"Projector_Disc{i}"));
        Label("FrontPlateTitle", "DREAM_V6_TARGET_OBSERVE_TARGET_AUDITORIUM_LINE_SET_NOTCHES", new(0, .79f, -3.944f), 0, 19);
        Label("SidePlateTitle", "DREAM_V6_TARGET_OBSERVE_TARGET_AUDITORIUM_PROJECTION_ORDER", new(-7.251f, 1.51f, -7.1f), -90, 16);
    }

    private void ApplyDiscs(IReadOnlyList<int> values)
    {
        for (var i = 0; i < _discs.Count; i++) _discs[i].RotationDegrees = new(values[i] * 120, 0, 0);
    }

    private void Label(string name, string key, Vector3 p, float yaw, int size) => Geometry.AddChild(new Label3D
    {
        Name = name, Text = UiText.Get(key), Position = p, RotationDegrees = new(0, yaw, 0),
        Font = UiStyles.CinematicFont, FontSize = size, PixelSize = .002f, Modulate = new Color("303a32"),
        OutlineSize = 0, CastShadow = GeometryInstance3D.ShadowCastingSetting.Off
    });

    private static void ConfigureMeshes(Node node)
    {
        if (node is MeshInstance3D mesh)
        {
            for (var i = 0; i < mesh.GetSurfaceOverrideMaterialCount(); i++)
                if (mesh.GetActiveMaterial(i) is BaseMaterial3D material)
                    material.TextureFilter = BaseMaterial3D.TextureFilterEnum.LinearWithMipmapsAnisotropic;
            if (mesh.Name.ToString().Contains("Glass", StringComparison.Ordinal))
                mesh.CastShadow = GeometryInstance3D.ShadowCastingSetting.Off;
        }
        foreach (var child in node.GetChildren()) ConfigureMeshes(child);
    }

    private void ApplyLineValues(IReadOnlyList<int> values)
    {
        _lineValues = values.Select(value => Mathf.Clamp(value, 1, 4)).ToArray();
        for (var index = 0; index < _lineWeights.Count; index++)
        {
            var position = _lineWeights[index].Position;
            position.Y = AuditoriumLayout3D.WeightMinimumHeight + (_lineValues[index] - 1) * AuditoriumLayout3D.WeightStep;
            _lineWeights[index].Position = position;
        }
    }
}
