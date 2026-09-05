using Godot;
using TownRpg.Core.Domain;

namespace TownRpg.Presentation;

public partial class DreamAuditoriumVisual3D : DreamLateRoomVisual3D
{
    private static readonly CollisionSpec[] Specs =
    [
        new("Floor", new Vector3(0f, -0.09f, 0f), new Vector3(18f, 0.18f, 13f), false),
        new("Ceiling", new Vector3(0f, 7.41f, 0f), new Vector3(18f, 0.18f, 13f), false),
        new("WallNorth", new Vector3(0f, 3.75f, -6.5f), new Vector3(18.2f, 7.5f, 0.20f), false),
        new("WallEast", new Vector3(9f, 3.75f, 0f), new Vector3(0.20f, 7.5f, 13f), false),
        new("WallWest", new Vector3(-9f, 3.75f, 0f), new Vector3(0.20f, 7.5f, 13f), false),
        new("WallSouthWest", new Vector3(-4.9f, 3.75f, 6.5f), new Vector3(8.2f, 7.5f, 0.20f), false),
        new("WallSouthEast", new Vector3(4.9f, 3.75f, 6.5f), new Vector3(8.2f, 7.5f, 0.20f), false),
        new("DoorTop", new Vector3(0f, 5.15f, 6.5f), new Vector3(1.8f, 4.7f, 0.20f), false),
        new("DoorBarrier", new Vector3(0f, 1.25f, 6.36f), new Vector3(1.8f, 2.5f, 0.24f), false),
        new("StageBack", new Vector3(0f, 1.0f, -5.75f), new Vector3(12f, 2.0f, 1.2f), true),
        new("SeatBankWest", new Vector3(-4.6f, 0.55f, 1.45f), new Vector3(3.2f, 1.1f, 3.3f), true),
        new("SeatBankEast", new Vector3(4.6f, 0.55f, 1.45f), new Vector3(3.2f, 1.1f, 3.3f), true),
        new("Projector", new Vector3(-7.65f, 1.25f, -3.9f), new Vector3(1.6f, 2.5f, 2.0f), true),
        new("FlyRail", new Vector3(7.65f, 1.2f, -3.9f), new Vector3(1.4f, 2.4f, 3.2f), true),
        new("LightConsole", new Vector3(-6.6f, 0.75f, 4.65f), new Vector3(2.2f, 1.5f, 1.2f), true),
        new("SoundConsole", new Vector3(0f, 0.75f, 4.4f), new Vector3(2.4f, 1.5f, 0.8f), true),
        new("BroadcastConsole", new Vector3(6.6f, 0.75f, 4.65f), new Vector3(2.2f, 1.5f, 1.2f), true),
        new("SpliceBench", new Vector3(5.7f, 0.72f, -0.2f), new Vector3(2.2f, 1.44f, 1.4f), true),
        new("MasterPlayer", new Vector3(6.9f, 1.1f, 1.6f), new Vector3(1.4f, 2.2f, 1.1f), true),
        new("LeftLever", new Vector3(-7.4f, 0.85f, -1.0f), new Vector3(1.0f, 1.7f, 1.0f), true),
        new("RightSwitch", new Vector3(7.4f, 0.85f, -1.0f), new Vector3(1.0f, 1.7f, 1.0f), true)
    ];

    private static readonly Vector3[] Waypoints =
    [
        new(0f, 0f, 5.55f), new(-2f, 0f, 5.2f), new(2f, 0f, 5.2f),
        new(-2.2f, 0f, 3.1f), new(2.2f, 0f, 3.1f), new(0f, 0f, 1.4f),
        new(-2.3f, 0f, -0.8f), new(2.3f, 0f, -0.8f), new(-5.8f, 0f, -2.2f),
        new(5.8f, 0f, -2.2f), new(0f, 0f, -3.8f)
    ];

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
    public override Rect2 WalkableBounds => new(-8.5f, -5.5f, 17f, 11f);
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
        if (_masterTape.Visible)
            _masterTape.Rotation = new Vector3(_masterTape.Rotation.X,
                _masterTape.Rotation.Y + (float)delta * 2.2f, _masterTape.Rotation.Z);
        if (_projectorSolved.Visible)
            _projectorSolved.Rotation = new Vector3(_projectorSolved.Rotation.X,
                _projectorSolved.Rotation.Y + (float)delta * 0.8f, _projectorSolved.Rotation.Z);
    }

    public override void ApplyState(DreamEscapeState state)
    {
        _lineSetSolved = state.SolvedPuzzles.Contains(DreamEscapeIds.PuzzleAuditoriumLineSetBalance);
        ApplyLineValues(_lineSetSolved ? [3, 1, 4, 2] : [1, 1, 1, 1]);
        _curtain.Position = new Vector3(0f, _lineSetSolved ? 5.25f : 2.65f, -5.68f);
        _projectionClue.Visible = _lineSetSolved;
        _lens.Visible = state.InstalledParts.Contains(DreamEscapeIds.SlotAuditoriumProjectorLens) ||
                        state.SolvedPuzzles.Contains(DreamEscapeIds.PuzzleAuditoriumShadowProjector);
        _projectorSolved.Visible = state.SolvedPuzzles.Contains(DreamEscapeIds.PuzzleAuditoriumShadowProjector);
        _tapeOrder.Visible = _projectorSolved.Visible;
        _spliceReel.Visible = state.SolvedPuzzles.Contains(DreamEscapeIds.PuzzleAuditoriumMasterTapeSplice);
        _masterTape.Visible = state.InstalledParts.Contains(DreamEscapeIds.SlotAuditoriumMasterTape);
        _masterPlayerLight.Visible = _masterTape.Visible;
        _lightConsole.Visible = state.SolvedPuzzles.Contains(DreamEscapeIds.PuzzleAuditoriumLightConsole);
        _soundConsole.Visible = state.SolvedPuzzles.Contains(DreamEscapeIds.PuzzleAuditoriumSoundConsole);
        _broadcastConsole.Visible = state.SolvedPuzzles.Contains(DreamEscapeIds.PuzzleAuditoriumBroadcastConsole);
        _exitOpen.Visible = state.Status == DreamEscapeStatus.ExitOpened;
        _counterweightPickup.Visible = !state.SolvedPuzzles.Contains(DreamEscapeIds.PickupCurtainCounterweight);
        _lensPickup.Visible = !state.SolvedPuzzles.Contains(DreamEscapeIds.PickupProjectorLens);
    }

    public override void ApplyTransientState(DreamAreaTransientState state)
    {
        _leftLeverLight.Visible = state.XiaoyuHoldingExitLever;
        if (_lineSetSolved) return;
        if (state.PreviewPuzzleId == DreamEscapeIds.PuzzleAuditoriumLineSetBalance &&
            state.PreviewValues is { Count: 4 } values)
        {
            ApplyLineValues(values.Select(value => int.TryParse(value, out var parsed) ? parsed : 1).ToArray());
        }
        else
        {
            ApplyLineValues([1, 1, 1, 1]);
        }
    }

    protected override void BuildRoom()
    {
        _doorHinge = new Node3D { Name = "Door_South_Hinge", Position = new Vector3(-0.9f, 0f, 6.38f) };
        Geometry.AddChild(_doorHinge);
        AddBox(_doorHinge, "DoorLeaf", new Vector3(0.9f, 1.25f, 0f),
            new Vector3(1.8f, 2.5f, 0.12f), "oak");

        _curtain = AddBox(Geometry, "MainCurtain", new Vector3(0f, 2.65f, -5.68f),
            new Vector3(11.5f, 5.1f, 0.18f), "curtain");
        for (var index = 0; index < 4; index++)
        {
            AddCylinder(Geometry, $"LineSetCable{index}", new Vector3(6.9f + index * 0.35f, 4.6f, -4.7f),
                0.025f, 4.8f, "metal");
            _lineWeights.Add(AddBox(Geometry, $"LineSetWeight{index}",
                new Vector3(6.9f + index * 0.35f, 2.2f, -4.7f),
                new Vector3(0.22f, 0.44f, 0.30f), "brass"));
        }

        _projectionClue = AddLabel("ProjectionSilhouettes", "▦      人      🔔",
            new Vector3(0f, 3.45f, -5.52f), Vector3.Zero, 84, new Color("e8d9b4"));
        _lensPickup = AddCylinder(Geometry, "ProjectorLensPickup", new Vector3(-7.0f, 0.25f, -4.4f),
            0.25f, 0.18f, "amber_glow", new Vector3(90f, 0f, 0f));
        _lens = AddCylinder(Geometry, "InstalledProjectorLens", new Vector3(-6.85f, 1.45f, -3.55f),
            0.32f, 0.22f, "glass", new Vector3(90f, 0f, 0f));
        _projectorSolved = AddCylinder(Geometry, "ProjectorDiscsAligned",
            new Vector3(-7.35f, 1.55f, -3.95f), 0.62f, 0.14f, "green_glow",
            new Vector3(90f, 0f, 0f));
        _tapeOrder = AddLabel("MasterTapeOrder", "教室  →  音乐  →  档案",
            new Vector3(-7.20f, 2.25f, -3.20f), new Vector3(0f, 35f, 0f), 28,
            new Color("e8d9b4"));

        _counterweightPickup = AddBox(Geometry, "CurtainCounterweightPickup",
            new Vector3(-6.2f, 0.30f, -1.8f), new Vector3(0.42f, 0.60f, 0.36f), "amber_glow");
        _spliceReel = AddCylinder(Geometry, "SplicedMasterReel", new Vector3(5.7f, 1.18f, -0.55f),
            0.48f, 0.12f, "green_glow", new Vector3(90f, 0f, 0f));
        _masterTape = AddCylinder(Geometry, "InstalledMasterTape", new Vector3(6.65f, 1.55f, 1.0f),
            0.44f, 0.13f, "brass", new Vector3(90f, 0f, 0f));
        _masterPlayerLight = AddBox(Geometry, "MasterPlayerWhiteNoise", new Vector3(6.45f, 1.0f, 1.0f),
            new Vector3(0.08f, 0.45f, 0.80f), "green_glow");

        _lightConsole = AddBox(Geometry, "LightConsoleSolved", new Vector3(-6.6f, 1.28f, 4.05f),
            new Vector3(1.55f, 0.10f, 0.28f), "green_glow");
        _soundConsole = AddBox(Geometry, "SoundConsoleSolved", new Vector3(0f, 1.28f, 4.05f),
            new Vector3(1.65f, 0.10f, 0.28f), "green_glow");
        _broadcastConsole = AddBox(Geometry, "BroadcastConsoleSolved", new Vector3(6.6f, 1.28f, 4.05f),
            new Vector3(1.55f, 0.10f, 0.28f), "green_glow");
        _leftLeverLight = AddBox(Geometry, "LeftLeverHeld", new Vector3(-7.0f, 1.25f, -1.0f),
            new Vector3(0.12f, 0.85f, 0.55f), "amber_glow", new Vector3(0f, 0f, -26f));
        _exitOpen = AddBox(Geometry, "ExitOpenGlow", new Vector3(0f, 2.5f, -6.30f),
            new Vector3(3.2f, 4.8f, 0.08f), "green_glow");
    }

    private void ApplyLineValues(IReadOnlyList<int> values)
    {
        _lineValues = values.Select(value => Mathf.Clamp(value, 1, 4)).ToArray();
        for (var index = 0; index < _lineWeights.Count; index++)
        {
            var position = _lineWeights[index].Position;
            position.Y = 2.65f - _lineValues[index] * 0.48f;
            _lineWeights[index].Position = position;
        }
    }
}
