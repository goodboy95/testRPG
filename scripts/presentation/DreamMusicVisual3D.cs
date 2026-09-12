using Godot;
using TownRpg.Core.Domain;

namespace TownRpg.Presentation;

public partial class DreamMusicVisual3D : Node3D
{
    private Node3D _model = null!;
    private Node3D _geometry = null!;
    private Node3D _cageDoor = null!;
    private Node3D _scoreRevealed = null!;
    private Node3D _metronomeWeight = null!;
    private Node3D _metronomeSolved = null!;
    private Node3D _forksSolved = null!;
    private Node3D _pianoRollInstalled = null!;
    private Node3D _acousticSolved = null!;
    private Node3D _attendanceOpen = null!;
    private readonly Dictionary<string, Node3D> _pickups = new(StringComparer.Ordinal);
    private readonly Dictionary<string, StaticBody3D> _bodies = new(StringComparer.Ordinal);
    private GroundWaypointNavigator3D _navigator = null!;
    private Node3D? _debugHost;
    private bool _stateApplied;
    private bool _openTarget;
    private bool _doorPassable;
    private float _doorProgress;
    private float _closedYaw;

    public Rect2 WalkableBounds => MusicRoomLayout3D.WalkableBounds;
    public int CollisionBodyCount => MusicRoomLayout3D.Collisions.Length;
    public int FurnitureCollisionCount => MusicRoomLayout3D.Collisions.Count(spec => spec.Furniture);
    public Node3D DoorHinge { get; private set; } = null!;
    public Node3D ModelRoot => _model;
    public bool ScoreRevealed => _scoreRevealed.Visible;
    public bool CageOpen => _openTarget;
    public bool EquipmentPassable => _doorPassable;
    public bool EquipmentDoorMoving => _openTarget && _doorProgress < 1f;
    public bool MetronomeWeightInstalled => _metronomeWeight.Visible;
    public bool MetronomeSolved => _metronomeSolved.Visible;
    public bool TuningForksSolved => _forksSolved.Visible;
    public bool PianoRollInstalled => _pianoRollInstalled.Visible;
    public bool AcousticWallSolved => _acousticSolved.Visible;
    public bool AttendanceReaderOpen => _attendanceOpen.Visible;
    public bool PianoBenchOpen => false;
    public int VisiblePickupCount => _pickups.Values.Count(node => node.Visible);

    public override void _Ready()
    {
        _model = GetNode<Node3D>("../MusicModel");
        _geometry = new Node3D { Name = "MusicRoomGeometry" };
        AddChild(_geometry);
        DoorHinge = Required("Door_West_Hinge");
        _cageDoor = Required("EquipmentDoorHinge");
        _closedYaw = _cageDoor.Rotation.Y;
        _scoreRevealed = Required("Score_Revealed");
        _metronomeWeight = Required("Metronome_Weight");
        _metronomeSolved = Required("Metronome_Solved");
        _forksSolved = Required("Calibration_Solved");
        _pianoRollInstalled = Required("Roll_Installed");
        _acousticSolved = Required("Acoustic_Solved");
        _attendanceOpen = Required("Photo_Solved");
        foreach (var (id, name) in new[]
        {
            (DreamEscapeIds.ItemRubberMallet,"Pickup_Mallet"),
            (DreamEscapeIds.ItemListeningCone,"Pickup_Cone"),
            (DreamEscapeIds.ItemMetronomeWeight,"Pickup_Weight"),
            (DreamEscapeIds.ItemTornPianoRoll,"Pickup_Roll"),
            (DreamEscapeIds.ItemWaxPatch,"Pickup_Wax")
        }) _pickups[id] = Required(name);
        ConfigureMeshes(_model);
        BuildLabels();
        BuildCollisionBodies();
        ResetNavigation();
    }

    public override void _Process(double delta)
    {
        if (!EquipmentDoorMoving) return;
        _doorProgress = Mathf.Min(1f, _doorProgress + (float)delta / .4f);
        SetDoorAngle(Mathf.SmoothStep(0f, 1f, _doorProgress));
        if (_doorProgress >= 1f) SetDoorPassable(true);
    }

    public void ApplyState(DreamEscapeState state)
    {
        var open = state.SolvedPuzzles.Contains(DreamEscapeIds.PuzzleMusicCage);
        if (!_stateApplied || !open)
        {
            _doorProgress = open ? 1f : 0f;
            SetDoorAngle(_doorProgress);
            SetDoorPassable(open);
        }
        else if (open != _openTarget) _doorProgress = 0f;
        _openTarget = open;
        _stateApplied = true;
        _scoreRevealed.Visible = state.SolvedPuzzles.Contains(DreamEscapeIds.PuzzleMusicScore);
        SetPickup(state, DreamEscapeIds.ItemRubberMallet, DreamEscapeIds.PickupRubberMallet);
        SetPickup(state, DreamEscapeIds.ItemListeningCone, DreamEscapeIds.PickupListeningCone);
        SetPickup(state, DreamEscapeIds.ItemMetronomeWeight, DreamEscapeIds.PickupMetronomeWeight);
        SetPickup(state, DreamEscapeIds.ItemTornPianoRoll, DreamEscapeIds.PickupTornPianoRoll);
        SetPickup(state, DreamEscapeIds.ItemWaxPatch, DreamEscapeIds.PickupWaxPatch);
        _metronomeWeight.Visible = state.InstalledParts.Contains(DreamEscapeIds.SlotMusicMetronomeWeight) ||
                                   state.SolvedPuzzles.Contains(DreamEscapeIds.ProgressMetronomeReady);
        _metronomeSolved.Visible = state.SolvedPuzzles.Contains(DreamEscapeIds.ProgressMetronomeReady);
        _forksSolved.Visible = state.SolvedPuzzles.Contains(DreamEscapeIds.PuzzleMusicChannelCalibration);
        _pianoRollInstalled.Visible = state.InstalledParts.Contains(DreamEscapeIds.SlotMusicPianoRoll) ||
                                      state.SolvedPuzzles.Contains(DreamEscapeIds.PuzzleMusicPianoRoll);
        _acousticSolved.Visible = state.SolvedPuzzles.Contains(DreamEscapeIds.PuzzleMusicAcousticWall);
        _attendanceOpen.Visible = state.SolvedPuzzles.Contains(DreamEscapeIds.PuzzleMusicBackstagePhoto);
        _geometry.GetNode<Label3D>("ScoreSequence").Visible = _scoreRevealed.Visible;
        _geometry.GetNode<Label3D>("BeatGlyphs").Visible = _metronomeSolved.Visible;
        _geometry.GetNode<Label3D>("ForkMap").Visible = _forksSolved.Visible;
        _geometry.GetNode<Label3D>("PitchInstrumentMap").Visible = _pianoRollInstalled.Visible;
    }

    public void ApplySettings(PlayerSettings settings) => _ = settings;
    public bool IsBlocked(Vector3 position) => MusicRoomLayout3D.IsBlocked(position, _doorPassable);
    public Vector3 ResolveSpawn(Vector3 position) => MusicRoomLayout3D.ResolveSpawn(position, _doorPassable);
    public Vector3 AdvanceCompanion(Vector3 current, Vector3 desired, float distance) =>
        _navigator.Advance(current, desired, distance);

    public void SetCollisionDebug(bool visible)
    {
        _debugHost?.QueueFree();
        _debugHost = null;
        if (!visible) return;
        _debugHost = new Node3D { Name = "CollisionDebug" };
        AddChild(_debugHost);
        foreach (var spec in MusicRoomLayout3D.Collisions.Where(s => MusicRoomLayout3D.IsActive(s, _doorPassable)))
            _debugHost.AddChild(new MeshInstance3D
            {
                Name = spec.Name, Position = spec.Position, Mesh = new BoxMesh { Size = spec.Size },
                MaterialOverride = new StandardMaterial3D
                {
                    AlbedoColor = spec.Furniture ? new Color("ffad3b55") : new Color("48a8ff3c"),
                    ShadingMode = BaseMaterial3D.ShadingModeEnum.Unshaded,
                    Transparency = BaseMaterial3D.TransparencyEnum.Alpha, NoDepthTest = true
                },
                CastShadow = GeometryInstance3D.ShadowCastingSetting.Off
            });
    }

    private void SetDoorAngle(float amount) => _cageDoor.Rotation =
        new Vector3(_cageDoor.Rotation.X, _closedYaw - Mathf.Pi / 2 * amount, _cageDoor.Rotation.Z);

    private void SetDoorPassable(bool open)
    {
        var changed = open != _doorPassable;
        _doorPassable = open;
        foreach (var spec in MusicRoomLayout3D.Collisions)
        {
            var body = _bodies[spec.Name];
            body.CollisionLayer = MusicRoomLayout3D.IsActive(spec, open) ? 1u : 0u;
            body.CollisionMask = body.CollisionLayer;
        }
        if (changed || _navigator is null) ResetNavigation();
        if (_debugHost is not null) SetCollisionDebug(true);
    }

    private void ResetNavigation() => _navigator = new GroundWaypointNavigator3D(
        MusicRoomLayout3D.Waypoints(_doorPassable), IsBlocked, ResolveSpawn);

    private void SetPickup(DreamEscapeState state, string itemId, string progressId) =>
        _pickups[itemId].Visible = state.XiaoyuJoined && !state.SolvedPuzzles.Contains(progressId);

    private Node3D Required(string name) => _model.FindChild(name, true, false) as Node3D
        ?? throw new InvalidOperationException($"Music v2 model is missing '{name}'.");

    private void BuildCollisionBodies()
    {
        var host = new Node3D { Name = "CollisionBodies" };
        AddChild(host);
        foreach (var spec in MusicRoomLayout3D.Collisions)
        {
            var body = new StaticBody3D { Name = spec.Name, Position = spec.Position };
            body.AddChild(new CollisionShape3D { Shape = new BoxShape3D { Size = spec.Size } });
            host.AddChild(body);
            _bodies[spec.Name] = body;
        }
    }

    private static void ConfigureMeshes(Node parent)
    {
        if (parent is MeshInstance3D mesh)
        {
            for (var surface = 0; surface < mesh.GetSurfaceOverrideMaterialCount(); surface++)
                if (mesh.GetActiveMaterial(surface) is BaseMaterial3D material)
                    material.TextureFilter = BaseMaterial3D.TextureFilterEnum.LinearWithMipmapsAnisotropic;
            if (mesh.Name.ToString().Contains("Glass", StringComparison.Ordinal))
                mesh.CastShadow = GeometryInstance3D.ShadowCastingSetting.Off;
        }
        foreach (var child in parent.GetChildren()) ConfigureMeshes(child);
    }

    private void BuildLabels()
    {
        Label("ScoreSequence", DreamPrologueText.Get("DREAM_V6_WORLD_SCORE"), new(-4.35f,1.07f,-4.222f), 0, 28);
        Label("BeatGlyphs", "○   ◇   □   △", new(-.48f,1.37f,4.289f), 180, 10);
        Label("ForkMap", DreamPrologueText.Get("DREAM_V6_WORLD_CHANNELS"), new(1.45f,1.1f,-3.57f), 0, 20);
        Label("PitchInstrumentMap", DreamPrologueText.Get("DREAM_V6_WORLD_SCORE_END"), new(3.15f,1.1f,4.15f), 180, 16);
        Label("TempoNotice", DreamPrologueText.Get("DREAM_V6_WORLD_METRONOME"), new(-5.865f,1.65f,2), 90, 22);
        Label("RollTemplate", DreamPrologueText.Get("DREAM_V6_WORLD_SEAMS"), new(-5.865f,1.7f,-2.15f), 90, 20);
        Label("ForkReference", DreamPrologueText.Get("DREAM_V6_WORLD_MUSIC_WAVES"), new(4.25f,2.00f,-4.85f), 0, 22);
        Label("ChannelLegend", DreamPrologueText.Get("DREAM_V6_WORLD_MUSIC_CHANNEL_LEGEND"), new(4.72f,1.59f,-4.85f), 0, 18);
        for (var i=0;i<3;i++) Label("FrontWaveGlyph"+i,new[]{"○","◇","□"}[i],new(3.57f,1.79f-i*.2f,-4.85f),0,23);
        Label("SideObservation", DreamPrologueText.Get("DREAM_V6_WORLD_MUSIC_SIDE_PORT"), new(2.947f,1.65f,-3.85f), 90, 18);
        Label("EquipmentName", DreamPrologueText.Get("DREAM_V6_WORLD_MUSIC_EQUIPMENT_SIGN"), new(4.35f,2.92f,-1.635f), 0, 32);
        Label("RoomName", DreamPrologueText.Get("DREAM_V6_WORLD_MUSIC_ROOM_SIGN"), new(-5.905f,2.86f,3.55f), 90, 28);
        Required("DoorNumberPlate").AddChild(new Label3D
        {
            Name="InnerNumbers",Text=DreamPrologueText.Get("DREAM_V6_WORLD_MUSIC_INNER_NUMBERS"),
            Position=new(0,0,-.015f),RotationDegrees=new(0,180,0),FontSize=24,PixelSize=.0025f,
            Modulate=new Color("3d3b32"),OutlineSize=0,CastShadow=GeometryInstance3D.ShadowCastingSetting.Off
        });
    }

    private void Label(string name, string text, Vector3 position, float yaw, int size)
    {
        _geometry.AddChild(new Label3D
        {
            Name = name, Text = text, Position = position, RotationDegrees = new Vector3(0,yaw,0),
            FontSize = size, PixelSize = .0025f, Modulate = new Color("3d3b32"), OutlineSize = 0,
            CastShadow = GeometryInstance3D.ShadowCastingSetting.Off
        });
    }
}
