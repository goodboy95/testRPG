using Godot;
using TownRpg.Core.Domain;

namespace TownRpg.Presentation;

public partial class DreamArchiveVisual3D : DreamLateRoomVisual3D
{
    private static readonly CollisionSpec[] Specs = ArchiveRoomLayout3D.Collisions
        .Select(s => new CollisionSpec(s.Name, s.Position, s.Size, s.Furniture)).ToArray();
    private static readonly Vector3[] Waypoints = ArchiveRoomLayout3D.Waypoints();
    private GroundWaypointNavigator3D? _navigator;
    private Node3D _fuseRoutePaper = null!;
    private Node3D _drawerClosed = null!;
    private Node3D _timelineUnsorted = null!;
    private Node3D _recorderReelRight = null!;
    public Vector3 AdvanceCompanion(Vector3 current, Vector3 desired, float distance) =>
        (_navigator ??= new GroundWaypointNavigator3D(Waypoints, IsBlocked, ResolveSpawn)).Advance(current, desired, distance);

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
    public override Rect2 WalkableBounds => ArchiveRoomLayout3D.WalkableBounds;
    public override bool IsBlocked(Vector3 position) => ArchiveRoomLayout3D.IsBlocked(position);
    public override Vector3 ResolveSpawn(Vector3 requested) => ArchiveRoomLayout3D.ResolveSpawn(requested);
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
        if (!_recorderRunning.Visible) return;
        _recorderReel.RotateX((float)delta * 2.6f);
        _recorderReelRight.RotateX((float)delta * 2.6f);
    }

    public override void ApplyState(DreamEscapeState state)
    {
        _registryOpen.Visible = state.SolvedPuzzles.Contains(DreamEscapeIds.PuzzleArchiveRetrieval);
        _drawerOpen.Visible = state.SolvedPuzzles.Contains(DreamEscapeIds.PuzzleArchiveRetrieval);
        _fuseRepaired.Visible = state.SolvedPuzzles.Contains(DreamEscapeIds.ProgressFuseAssembly);
        _recorderReel.Visible = state.InstalledParts.Contains(DreamEscapeIds.SlotArchiveRecorderReel) ||
                                state.SolvedPuzzles.Contains(DreamEscapeIds.PuzzleArchiveRecorderRebuild);
        _recorderCrank.Visible = state.InstalledParts.Contains(DreamEscapeIds.SlotArchiveRecorderCrank) ||
                                 state.SolvedPuzzles.Contains(DreamEscapeIds.PuzzleArchiveRecorderRebuild);
        _recorderFuse.Visible = state.InstalledParts.Contains(DreamEscapeIds.SlotArchiveRecorderFuse) ||
                                state.SolvedPuzzles.Contains(DreamEscapeIds.PuzzleArchiveRecorderRebuild);
        _recorderRunning.Visible = state.SolvedPuzzles.Contains(DreamEscapeIds.PuzzleArchiveRecorderRebuild);
        _counterOpen.Visible = state.SolvedPuzzles.Contains(DreamEscapeIds.PuzzleArchiveWorkTimeline);
        _vaultOpen.Visible = false;
        _drawerClosed.Visible = !_drawerOpen.Visible;
        _fuseRoutePaper.Visible = _registryOpen.Visible;
        _timelineUnsorted.Visible = !_counterOpen.Visible;
        _recorderReelRight.Visible = _recorderReel.Visible;
        _indexStrip.Visible = !state.SolvedPuzzles.Contains(DreamEscapeIds.PickupArchiveIndexStrip);
        for (var index = 0; index < _registryPreviewLights.Count; index++)
            _registryPreviewLights[index].Visible = _registryOpen.Visible;
    }

    public override void ApplyTransientState(DreamAreaTransientState state)
    {
        if (_registryOpen.Visible) return;
        var count = state.PreviewPuzzleId == DreamEscapeIds.PuzzleArchiveRetrieval
            ? state.PreviewValues?.Count ?? 0
            : 0;
        for (var index = 0; index < _registryPreviewLights.Count; index++)
            _registryPreviewLights[index].Visible = index < count;
    }

    protected override void BuildRoom()
    {
        var model = GetNode<Node3D>("../ArchiveModel");
        ConfigureMeshes(model);
        Node3D Required(string name) => model.FindChild(name, true, false) as Node3D
            ?? throw new InvalidOperationException($"Archive v2 node missing: {name}");
        _doorHinge = Required("Door_East_Hinge");
        _registryOpen = Required("Registry_Solved");
        _drawerOpen = Required("Drawer_Open");
        _drawerClosed = Required("Drawer_Closed");
        _fuseRepaired = Required("Fuse_Repaired");
        _fuseRoutePaper = Required("FuseRoutePaper");
        _recorderReel = Required("Recorder_Reel");
        _recorderReelRight = Required("Recorder_Reel_Right");
        _recorderCrank = Required("Recorder_Crank");
        _recorderFuse = Required("Recorder_Fuse");
        _recorderRunning = Required("Recorder_Running");
        _counterOpen = Required("Timeline_Sorted");
        _timelineUnsorted = Required("Timeline_Unsorted");
        _vaultOpen = Required("UnusedVaultState");
        _indexStrip = Required("Pickup_IndexStrip");
        for (var i = 0; i < 4; i++) _registryPreviewLights.Add(Required($"Registry_Preview{i}"));
        PaperTitle("RegistryRuleTitle", "DREAM_V6_TARGET_OBSERVE_TARGET_ARCHIVE_REGISTRY_RULE", new(-4.8f, 1.63f, 4.859f), 180);
        PaperTitle("ChoirTitle", "DREAM_V6_TARGET_OBSERVE_TARGET_ARCHIVE_REGISTRY_CHOIR", new(-6.014f, 1.63f, 1.4f), 90);
        PaperTitle("BroadcastTitle", "DREAM_V6_TARGET_OBSERVE_TARGET_ARCHIVE_REGISTRY_BROADCAST", new(-6.014f, 1.63f, -1f), 90);
        PaperTitle("ArtTitle", "DREAM_V6_TARGET_OBSERVE_TARGET_ARCHIVE_REGISTRY_ART", new(-6.014f, 1.63f, -3.8f), 90);
        PaperTitle("TrackTitle", "DREAM_V6_TARGET_OBSERVE_TARGET_ARCHIVE_REGISTRY_TRACK", new(-.7f, 1.63f, -5.034f), 0);
    }

    private void PaperTitle(string name, string key, Vector3 position, float yaw) => Geometry.AddChild(new Label3D
    {
        Name = name, Text = UiText.Get(key), Position = position, RotationDegrees = new(0, yaw, 0),
        Font = UiStyles.CinematicFont, FontSize = 20, PixelSize = .002f,
        Modulate = new Color("303a32"), OutlineSize = 0, CastShadow = GeometryInstance3D.ShadowCastingSetting.Off
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
}
