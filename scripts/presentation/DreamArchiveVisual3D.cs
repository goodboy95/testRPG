using Godot;
using TownRpg.Core.Domain;

namespace TownRpg.Presentation;

public partial class DreamArchiveVisual3D : DreamLateRoomVisual3D
{
    private static readonly CollisionSpec[] Specs =
    [
        new("Floor", new Vector3(0f, -0.09f, 0f), new Vector3(12f, 0.18f, 9f), false),
        new("Ceiling", new Vector3(0f, 4.11f, 0f), new Vector3(12f, 0.18f, 9f), false),
        new("WallNorth", new Vector3(0f, 2.1f, -4.5f), new Vector3(12.2f, 4.2f, 0.20f), false),
        new("WallSouth", new Vector3(0f, 2.1f, 4.5f), new Vector3(12.2f, 4.2f, 0.20f), false),
        new("WallWest", new Vector3(-6f, 2.1f, 0f), new Vector3(0.20f, 4.2f, 9f), false),
        new("WallEastNorth", new Vector3(6f, 2.1f, -0.75f), new Vector3(0.20f, 4.2f, 7.5f), false),
        new("WallEastSouth", new Vector3(6f, 2.1f, 4.15f), new Vector3(0.20f, 4.2f, 0.70f), false),
        new("DoorTop", new Vector3(6f, 3.35f, 3f), new Vector3(0.20f, 1.70f, 1.60f), false),
        new("DoorBarrier", new Vector3(5.86f, 1.2f, 3f), new Vector3(0.24f, 2.40f, 1.60f), false),
        new("WestArchiveStacks", new Vector3(-5.25f, 1.35f, -0.7f), new Vector3(1.1f, 2.7f, 6.7f), true),
        new("NorthArchiveStacks", new Vector3(-2.8f, 1.35f, -4.0f), new Vector3(4.0f, 2.7f, 0.8f), true),
        new("TrackCabinet", new Vector3(4.65f, 1.25f, -3.75f), new Vector3(1.6f, 2.5f, 1.0f), true),
        new("RegistryTable", new Vector3(0f, 0.72f, -0.25f), new Vector3(2.8f, 1.44f, 1.4f), true),
        new("DrawerMatrix", new Vector3(-3.0f, 0.95f, 1.65f), new Vector3(2.1f, 1.9f, 1.1f), true),
        new("RepairBench", new Vector3(4.55f, 0.72f, -1.8f), new Vector3(1.6f, 1.44f, 2.2f), true),
        new("BroadcastBooth", new Vector3(3.65f, 1.35f, 1.65f), new Vector3(2.8f, 2.7f, 1.9f), true),
        new("EditingCounter", new Vector3(0f, 0.75f, 3.85f), new Vector3(2.2f, 1.5f, 0.9f), true),
        new("UpperVault", new Vector3(-4.75f, 1.75f, 3.75f), new Vector3(1.6f, 3.5f, 1.0f), true)
    ];

    private static readonly Vector3[] Waypoints =
    [
        new(5.25f, 0f, 3f), new(4.2f, 0f, 2.8f), new(2.0f, 0f, 2.5f),
        new(0f, 0f, 2.1f), new(-2.0f, 0f, 2.7f), new(-3.6f, 0f, 2.2f),
        new(-3.6f, 0f, -1.2f), new(-1.8f, 0f, -2.4f), new(1.5f, 0f, -2.3f),
        new(3.1f, 0f, -2.7f), new(2.0f, 0f, 0.7f)
    ];

    private readonly List<Node3D> _registryPreviewLights = [];
    private Node3D _doorHinge = null!;
    private Node3D _registryOpen = null!;
    private Node3D _drawerOpen = null!;
    private Node3D _fuseRepaired = null!;
    private Node3D _recorderReel = null!;
    private Node3D _recorderCrank = null!;
    private Node3D _recorderFuse = null!;
    private Node3D _recorderRunning = null!;
    private Node3D _counterOpen = null!;
    private Node3D _vaultOpen = null!;
    private Node3D _indexStrip = null!;

    protected override IReadOnlyList<CollisionSpec> CollisionSpecs => Specs;
    protected override IReadOnlyList<Vector3> SafeWaypoints => Waypoints;
    public override Rect2 WalkableBounds => new(-5.5f, -4f, 11f, 8f);
    public override Node3D DoorHinge => _doorHinge;

    public bool RegistrySolved => _registryOpen.Visible;
    public bool DrawerRouteSolved => _drawerOpen.Visible;
    public bool FuseRepaired => _fuseRepaired.Visible;
    public bool RecorderRunning => _recorderRunning.Visible;
    public bool CounterOpen => _counterOpen.Visible;
    public bool VaultOpen => _vaultOpen.Visible;
    public bool IndexStripVisible => _indexStrip.Visible;
    public int RegistryPreviewCount => _registryPreviewLights.Count(light => light.Visible);

    public override void _Process(double delta)
    {
        if (_recorderRunning.Visible)
            _recorderReel.Rotation = new Vector3(_recorderReel.Rotation.X,
                _recorderReel.Rotation.Y + (float)delta * 2.6f, _recorderReel.Rotation.Z);
    }

    public override void ApplyState(DreamEscapeState state)
    {
        _registryOpen.Visible = state.SolvedPuzzles.Contains(DreamEscapeIds.PuzzleArchiveSignalRegistry);
        _drawerOpen.Visible = state.SolvedPuzzles.Contains(DreamEscapeIds.PuzzleArchiveDrawerRoute);
        _fuseRepaired.Visible = state.SolvedPuzzles.Contains(DreamEscapeIds.PuzzleArchiveFuseRepair);
        _recorderReel.Visible = state.InstalledParts.Contains(DreamEscapeIds.SlotArchiveRecorderReel) ||
                                state.SolvedPuzzles.Contains(DreamEscapeIds.PuzzleArchiveRecorderRebuild);
        _recorderCrank.Visible = state.InstalledParts.Contains(DreamEscapeIds.SlotArchiveRecorderCrank) ||
                                 state.SolvedPuzzles.Contains(DreamEscapeIds.PuzzleArchiveRecorderRebuild);
        _recorderFuse.Visible = state.InstalledParts.Contains(DreamEscapeIds.SlotArchiveRecorderFuse) ||
                                state.SolvedPuzzles.Contains(DreamEscapeIds.PuzzleArchiveRecorderRebuild);
        _recorderRunning.Visible = state.SolvedPuzzles.Contains(DreamEscapeIds.PuzzleArchiveRecorderRebuild);
        _counterOpen.Visible = state.SolvedPuzzles.Contains(DreamEscapeIds.PuzzleArchiveEditingCounter);
        _vaultOpen.Visible = state.SolvedPuzzles.Contains(DreamEscapeIds.PuzzleArchiveUpperVault);
        _indexStrip.Visible = !state.SolvedPuzzles.Contains(DreamEscapeIds.PickupArchiveIndexStrip);
        for (var index = 0; index < _registryPreviewLights.Count; index++)
            _registryPreviewLights[index].Visible = _registryOpen.Visible;
    }

    public override void ApplyTransientState(DreamAreaTransientState state)
    {
        if (_registryOpen.Visible) return;
        var count = state.PreviewPuzzleId == DreamEscapeIds.PuzzleArchiveSignalRegistry
            ? state.PreviewValues?.Count ?? 0
            : 0;
        for (var index = 0; index < _registryPreviewLights.Count; index++)
            _registryPreviewLights[index].Visible = index < count;
    }

    protected override void BuildRoom()
    {
        _doorHinge = new Node3D { Name = "Door_East_Hinge", Position = new Vector3(5.88f, 0f, 2.20f) };
        Geometry.AddChild(_doorHinge);
        AddBox(_doorHinge, "DoorLeaf", new Vector3(0f, 1.2f, 0.8f),
            new Vector3(0.12f, 2.4f, 1.6f), "oak");

        AddBox(Geometry, "RegistryState", new Vector3(0f, 1.02f, -0.25f),
            new Vector3(2.35f, 0.14f, 1.0f), "oak_light");
        _registryOpen = AddBox(Geometry, "RegistryDrawerOpen", new Vector3(0f, 0.65f, 0.65f),
            new Vector3(1.5f, 0.28f, 0.75f), "green_glow");
        for (var index = 0; index < 4; index++)
        {
            var light = AddBox(Geometry, $"RegistryPreview{index}",
                new Vector3(-0.72f + index * 0.48f, 1.13f, -0.25f),
                new Vector3(0.30f, 0.04f, 0.48f), "amber_glow");
            _registryPreviewLights.Add(light);
        }

        _indexStrip = AddBox(Geometry, "ArchiveIndexStripPickup", new Vector3(-1.8f, 0.92f, 2.8f),
            new Vector3(0.82f, 0.05f, 0.20f), "amber_glow", new Vector3(0f, 15f, 0f));
        _drawerOpen = AddBox(Geometry, "DrawerRouteOpen", new Vector3(-2.8f, 1.0f, 2.25f),
            new Vector3(1.45f, 0.52f, 0.70f), "green_glow");
        _fuseRepaired = AddBox(Geometry, "FuseBridge", new Vector3(4.0f, 1.18f, -1.75f),
            new Vector3(0.08f, 0.40f, 1.10f), "green_glow");

        var recorder = new Node3D { Name = "RecorderState", Position = new Vector3(2.55f, 1.16f, 2.22f) };
        Geometry.AddChild(recorder);
        _recorderReel = AddCylinder(recorder, "CalibrationReel", new Vector3(-0.35f, 0.28f, -0.52f),
            0.32f, 0.10f, "brass", new Vector3(90f, 0f, 0f));
        _recorderCrank = AddBox(recorder, "RecorderCrank", new Vector3(0.58f, 0.05f, -0.55f),
            new Vector3(0.12f, 0.65f, 0.12f), "metal", new Vector3(0f, 0f, 30f));
        _recorderFuse = AddBox(recorder, "RecorderFuse", new Vector3(0.25f, -0.38f, -0.56f),
            new Vector3(0.50f, 0.12f, 0.10f), "amber_glow");
        _recorderRunning = AddBox(recorder, "RecorderRunning", new Vector3(0f, -0.10f, -0.60f),
            new Vector3(1.35f, 0.08f, 0.08f), "green_glow");

        _counterOpen = AddBox(Geometry, "EditingCounterOpen", new Vector3(0f, 1.12f, 3.30f),
            new Vector3(1.55f, 0.48f, 0.12f), "green_glow");
        AddLabel("EditingStops", "12  —  24  —  31  —  45", new Vector3(0f, 1.38f, 3.24f),
            new Vector3(0f, 180f, 0f), 34, new Color("e8d9b4"));
        _vaultOpen = AddBox(Geometry, "UpperVaultOpen", new Vector3(-4.0f, 2.15f, 3.22f),
            new Vector3(0.80f, 0.95f, 0.12f), "green_glow");
    }
}
