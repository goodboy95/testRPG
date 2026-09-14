using Godot;
using NVector2 = System.Numerics.Vector2;

namespace TownRpg.Presentation;

/// <summary>Standalone home inspection. Deliberately has no GameSession mutations or persistence.</summary>
public partial class HomePreviewController3D : WorldArea3D
{
    /// <summary>Only set before Ready by the independent village host. Defaults preserve HomePreview v1.</summary>
    public bool EmbeddedInVillage { get; init; }
    public event Action? VillageExitRequested;
    public event Action? FixturesChanged;
    private bool _hostLocked;
    private sealed class DoorRuntime(HomeLayout3D.Door spec)
    {
        public HomeLayout3D.Door Spec { get; } = spec;
        public HomeDoorMotion Motion { get; } = new(spec);
        public List<Node3D> Hinges { get; } = [];
        public List<StaticBody3D> Leaves { get; } = [];
        public StaticBody3D Sweep { get; set; } = null!;
    }
    private readonly Dictionary<string, DoorRuntime> _doors = new(StringComparer.Ordinal);
    private readonly Dictionary<string, OmniLight3D> _lights = new(StringComparer.Ordinal);
    private readonly Dictionary<string, Vector3> _sightPoints = new(StringComparer.Ordinal);
    private Node3D _model = null!;
    private Label _prompt = null!;
    private Label _instructions = null!;
    private Control _pause = null!;
    private Button _resume = null!;
    private bool _paused;
    private float _feedbackTime;
    private string _feedback = string.Empty;
    private string _promptText = string.Empty;
    public bool Graybox { get; } = OS.GetCmdlineUserArgs().Contains("--home-graybox");
    public PlayerController3D PlayerActor => Player;
    public ThirdPersonCameraRig CameraController => CameraRig;
    public bool IsPaused => _paused;
    public bool AllLightsOff => _lights.Values.All(l => !l.Visible);
    public bool AllDoorsClosed => _doors.Values.All(d => d.Motion.Progress == 0 && !d.Motion.IsMoving);
    public Node3D ModelRoot => _model;
    protected override bool InputBlocked => _paused || _hostLocked;

    public void SetHostLocked(bool value) { _hostLocked = value; SetRuntimeInputLocked(value || _paused); }

    public override void _EnterTree()
    {
        InteractionRequested += OnInteraction;
        PromptChanged += OnPrompt;
    }

    public override void _Ready()
    {
        BuildModel();
        BuildGeometry();
        BuildActorAndCamera();
        BuildInteractions();
        BuildLighting();
        BuildUi();
        base._Ready();
        Player.ConfigureDefaultCharacterVisual(DreamCharacterFrames.Create(ResourceLoader.Load<Texture2D>(
            "res://assets/generated/dream_escape/pixel/protagonist-walk-4x4-v1.png")));
        ApplySettings(new TownRpg.Core.Domain.PlayerSettings());
        Player.GlobalPosition = V(HomeLayout3D.Layout.Spawn);
        Player.SetFacingImmediate(Vector3.Right);
        CameraRig.SetYawImmediate(-90);
        CameraRig.SnapToTarget();
    }

    public override void _ExitTree()
    {
        InteractionRequested -= OnInteraction;
        PromptChanged -= OnPrompt;
        base._ExitTree();
    }

    public override void _Process(double delta)
    {
        base._Process(delta);
        _feedbackTime = Math.Max(0, _feedbackTime - (float)delta);
        _prompt.Text = _feedbackTime > 0 ? _feedback : _promptText;
    }

    public override void _PhysicsProcess(double delta)
    {
        if (_paused) return;
        foreach (var door in _doors.Values)
        {
            if (!door.Motion.IsMoving) continue;
            door.Motion.Step((float)delta);
            ApplyDoor(door);
            if (!door.Motion.IsMoving) FixturesChanged?.Invoke();
        }
    }

    public override void _UnhandledInput(InputEvent e)
    {
        if (e is InputEventKey { Echo: true }) return;
        if (e is InputEventJoypadButton or InputEventJoypadMotion) SetGamepadPrompts(true);
        else if (e is InputEventKey or InputEventMouseButton) SetGamepadPrompts(false);
        if (!EmbeddedInVillage && e.IsActionPressed("ui_cancel"))
        {
            SetPaused(!_paused);
            GetViewport().SetInputAsHandled();
            return;
        }
        base._UnhandledInput(e);
    }

    public override void SetGamepadPrompts(bool value)
    {
        base.SetGamepadPrompts(value);
        if (_instructions is not null) _instructions.Text = UiText.Get(value ? "HOME_CONTROLS_PAD" : "HOME_CONTROLS_KEYBOARD");
    }

    protected override string BuildPrompt(InteractionMarker3D marker)
    {
        var id = marker.TargetId;
        if (EmbeddedInVillage && id == "home.door.entry")
            return $"[{(UseGamepadPrompts ? "A" : "F")}] {UiText.Get("VILLAGE_EXIT_HOME")}";
        var open = id.StartsWith("home.door.", StringComparison.Ordinal)
            ? _doors[id[10..]].Motion.IsOpen : _lights[id[11..]].Visible;
        return $"[{(UseGamepadPrompts ? "A" : "F")}] {UiText.Get(open ? "HOME_ACTION_CLOSE" : "HOME_ACTION_OPEN")} · {UiText.Get(marker.Prompt)}";
    }

    protected override bool IsMarkerActive(InteractionMarker3D marker)
    {
        if (Player is null) return false;
        if (marker.TargetId.StartsWith("home.door.", StringComparison.Ordinal) && _doors[marker.TargetId[10..]].Motion.IsMoving) return false;
        return HasLineOfSight(marker.TargetId);
    }

    public bool HasLineOfSight(string id)
    {
        var from = Player.GlobalPosition + Vector3.Up * 1.2f;
        var to = _sightPoints[id];
        var query = PhysicsRayQueryParameters3D.Create(from, to, 1);
        var excluded = new Godot.Collections.Array<Rid> { Player.GetRid() };
        if (id.StartsWith("home.door.", StringComparison.Ordinal))
        {
            var door = _doors[id[10..]];
            foreach (var leaf in door.Leaves) excluded.Add(leaf.GetRid());
            excluded.Add(door.Sweep.GetRid());
        }
        query.Exclude = excluded;
        return GetWorld3D().DirectSpaceState.IntersectRay(query).Count == 0;
    }

    private void OnPrompt(string value) => _promptText = value;
    private void OnInteraction(string id) => TryOperate(id);

    /// <summary>Shared input path used by the interaction signal and the engine probe.</summary>
    public bool TryOperate(string id)
    {
        if (_paused || _hostLocked || !_sightPoints.TryGetValue(id, out var sight) ||
            new Vector2(Player.Position.X - sight.X, Player.Position.Z - sight.Z).Length() > 1.65f || !HasLineOfSight(id)) return false;
        if (EmbeddedInVillage && id == "home.door.entry") { VillageExitRequested?.Invoke(); return true; }
        if (id.StartsWith("home.door.", StringComparison.Ordinal))
        {
            var door = _doors[id[10..]];
            if (!door.Motion.Request(new NVector2(Player.Position.X, Player.Position.Z)))
            {
                _feedback = UiText.Get("HOME_DOOR_CLEARANCE"); _feedbackTime = 2;
                return false;
            }
            ApplyDoor(door);
        }
        else
        {
            var light = _lights[id[11..]];
            light.Visible = !light.Visible;
            FixturesChanged?.Invoke();
        }
        return true;
    }

    public bool DoorOpen(string id) => _doors[id].Motion.IsOpen;
    public bool DoorMoving(string id) => _doors[id].Motion.IsMoving;
    public bool LightOn(string id) => _lights[id].Visible;

    public Dictionary<string, bool> CaptureDoors() => _doors.ToDictionary(pair => pair.Key, pair => pair.Value.Motion.StableOpen);
    public Dictionary<string, bool> CaptureLights() => _lights.ToDictionary(pair => pair.Key, pair => pair.Value.Visible);
    public void RestoreFixtures(IReadOnlyDictionary<string, bool> doors, IReadOnlyDictionary<string, bool> lights)
    {
        foreach (var (id, door) in _doors) { door.Motion.Restore(doors.GetValueOrDefault(id)); ApplyDoor(door); }
        foreach (var (id, light) in _lights) light.Visible = lights.GetValueOrDefault(id);
    }

    public void SetPaused(bool value)
    {
        if (!value) GetViewport().GuiGetFocusOwner()?.ReleaseFocus();
        _paused = value; _pause.Visible = value;
        SetRuntimeInputLocked(value);
        if (value) _resume.GrabFocus();
    }

    public void ResetPreview()
    {
        foreach (var d in _doors.Values) { d.Motion.Reset(); ApplyDoor(d); }
        foreach (var l in _lights.Values) l.Visible = false;
        _feedbackTime = 0;
        Player.GlobalPosition = V(HomeLayout3D.Layout.Spawn);
        Player.SetFacingImmediate(Vector3.Right);
        CameraRig.SetYawImmediate(-90); CameraRig.SnapToTarget();
        SetPaused(false);
    }

    private void ApplyDoor(DoorRuntime d)
    {
        var t = Mathf.SmoothStep(0, 1, d.Motion.Progress);
        for (var i = 0; i < d.Hinges.Count; i++)
        {
            var sign = d.Spec.Double ? (i == 0 ? 1 : -1) : d.Spec.Swing;
            d.Hinges[i].Rotation = new Vector3(0, sign * Mathf.Pi / 2 * t, 0);
            d.Leaves[i].CollisionLayer = d.Motion.IsMoving ? 0u : 1u;
        }
        // A temporary solid sweep prevents the actor entering a moving leaf's path.
        d.Sweep.CollisionLayer = d.Motion.IsMoving ? 1u : 0u;
    }

    private static Vector3 V(float[] a) => new(a[0], a[1], a[2]);
    private static Vector3 V(System.Numerics.Vector3 a) => new(a.X, a.Y, a.Z);

    private void BuildModel()
    {
        _model = Graybox ? new Node3D() : ResourceLoader.Load<PackedScene>("res://assets/generated/home/3d/home-v1.glb").Instantiate<Node3D>();
        _model.Name = "HomeModel"; AddChild(_model);
        foreach (var mesh in Descendants(_model).OfType<MeshInstance3D>())
            if (mesh.Name.ToString().StartsWith("Glass_", StringComparison.Ordinal)) mesh.CastShadow = GeometryInstance3D.ShadowCastingSetting.Off;
    }

    private static IEnumerable<Node> Descendants(Node root)
    {
        foreach (var c in root.GetChildren()) { yield return c; foreach (var n in Descendants(c)) yield return n; }
    }

    private void BuildGeometry()
    {
        foreach (var b in HomeLayout3D.WallBoxes()) AddBox(this, b.Id, V(b.Center), V(b.Size), Graybox);
        // Real window openings admit light, while glazing keeps the orbit camera indoors.
        foreach (var w in HomeLayout3D.Layout.Walls)
        foreach (var o in w.Openings.Where(o => o[2] > 0))
        {
            var position = w.Axis == "x" ? new Vector3(o[0], (o[2] + o[3]) / 2, w.At) : new Vector3(w.At, (o[2] + o[3]) / 2, o[0]);
            var size = w.Axis == "x" ? new Vector3(o[1], o[3] - o[2], .03f) : new Vector3(.03f, o[3] - o[2], o[1]);
            AddBox(this, "WindowCameraBarrier_" + w.Id, position, size, false).CollisionLayer = 2;
        }
        foreach (var f in HomeLayout3D.Layout.Furniture)
            AddBox(this, f.Id + "Collision", V(f.Position) + Vector3.Up * (f.Size[1] / 2), V(f.Size), Graybox);
        foreach (var b in HomeLayout3D.Layout.Structure)
            AddBox(this, b.Id + "Collision", V(b.Position), V(b.Size), Graybox);
    }

    private StaticBody3D AddBox(Node parent, string name, Vector3 position, Vector3 size, bool visible)
    {
        var body = new StaticBody3D { Name = name, Position = position, CollisionLayer = 1, CollisionMask = 0 };
        body.AddChild(new CollisionShape3D { Shape = new BoxShape3D { Size = size } });
        if (visible) body.AddChild(new MeshInstance3D { Mesh = new BoxMesh { Size = size }, MaterialOverride =
            new StandardMaterial3D { AlbedoColor = new Color(.52f, .57f, .55f), Roughness = .9f } });
        parent.AddChild(body); return body;
    }

    private void BuildActorAndCamera()
    {
        var player = new PlayerController3D { Name = "Player", MotionMode = CharacterBody3D.MotionModeEnum.Floating };
        player.AddChild(new CollisionShape3D { Position = new(0, .8f, 0), Shape = new CapsuleShape3D { Radius = .28f, Height = 1.6f } });
        AddChild(player);
        var rig = new ThirdPersonCameraRig { Name = "ThirdPersonCameraRig", FixedPitchDegrees = -21, FollowTargetHeight = 1.3f };
        var arm = new SpringArm3D { Name = "SpringArm3D", SpringLength = 2.6f, Margin = .08f, CollisionMask = 3,
            Shape = new SphereShape3D { Radius = .16f } };
        var socket = new Node3D { Name = "CameraSocket" };
        socket.AddChild(new Camera3D { Name = "Camera3D", Fov = 60, Near = .08f, Far = 90, Current = true });
        arm.AddChild(socket); rig.AddChild(arm); AddChild(rig);
    }

    private void BuildInteractions()
    {
        var host = new Node3D { Name = "InteractionMarkers" }; AddChild(host);
        foreach (var d in HomeLayout3D.Layout.Doors)
        {
            var runtime = new DoorRuntime(d); _doors.Add(d.Id, runtime);
            for (var i = 0; i < (d.Double ? 2 : 1); i++)
            {
                var name = $"Door_{d.Id}_Hinge{i}";
                Node3D hinge;
                if (Graybox)
                {
                    var offset = (i == 0 ? -1 : 1) * d.Width / 2;
                    hinge = new Node3D { Name = name, Position = V(d.Position) + (d.Axis == "x" ? Vector3.Right : Vector3.Back) * offset };
                    _model.AddChild(hinge);
                }
                else hinge = Descendants(_model).OfType<Node3D>().Single(n => n.Name == name);
                var len = d.Double ? d.Width / 2 : d.Width;
                var side = i == 0 ? 1 : -1;
                var center = d.Axis == "x" ? new Vector3(side * len / 2, d.Height / 2, 0) : new Vector3(0, d.Height / 2, len / 2);
                var size = d.Axis == "x" ? new Vector3(len, d.Height, .065f) : new Vector3(.065f, d.Height, len);
                runtime.Hinges.Add(hinge);
                runtime.Leaves.Add(AddBox(hinge, "DoorLeafCollision", center, size, Graybox));
            }
            var sweep = d.SweepBounds(0);
            runtime.Sweep = AddBox(this, "Sweep_" + d.Id, new(sweep.X + sweep.Width / 2, d.Height / 2, sweep.Z + sweep.Depth / 2),
                new(sweep.Width + .08f, d.Height, sweep.Depth + .08f), false);
            runtime.Sweep.CollisionLayer = 0;
            AddMarker(host, "home.door." + d.Id, "HOME_ROOM_" + d.Id.ToUpperInvariant(), V(d.Position) + Vector3.Up * 1.2f);
        }
        foreach (var l in HomeLayout3D.Layout.Lights)
            AddMarker(host, "home.light." + l.Id, "HOME_LIGHT_" + l.Id.ToUpperInvariant(), V(l.Switch));
    }

    private void AddMarker(Node3D host, string id, string key, Vector3 sight)
    {
        _sightPoints.Add(id, sight);
        host.AddChild(new InteractionMarker3D { Name = id.Replace('.', '_'), TargetId = id, Prompt = key,
            Position = new(sight.X, 0, sight.Z), InteractionRange = 1.65f });
    }

    private void BuildLighting()
    {
        AddChild(new MeshInstance3D { Name = "ScenicGround", Position = new(0, -.18f, 3), Mesh = new BoxMesh { Size = new(80, .08f, 80) },
            MaterialOverride = new StandardMaterial3D { AlbedoColor = new Color("737e6d"), Roughness = .97f } });
        AddChild(new WorldEnvironment { Environment = new Godot.Environment {
            BackgroundMode = Godot.Environment.BGMode.Color, BackgroundColor = new Color("bacdd7"),
            AmbientLightSource = Godot.Environment.AmbientSource.Color, AmbientLightColor = new Color("809199"), AmbientLightEnergy = .45f,
            AmbientLightSkyContribution = 0, ReflectedLightSource = Godot.Environment.ReflectionSource.Disabled,
            TonemapMode = Godot.Environment.ToneMapper.Filmic, TonemapExposure = .95f } });
        AddChild(new DirectionalLight3D { Name = "MorningSun", RotationDegrees = new(-32, -32, 0), LightColor = new Color("fff0d1"),
            LightEnergy = .75f, ShadowEnabled = true, DirectionalShadowMaxDistance = 45 });
        foreach (var l in HomeLayout3D.Layout.Lights)
        {
            var light = new OmniLight3D { Name = "Light_" + l.Id, Position = V(l.Position) - Vector3.Up * .13f,
                LightColor = new Color("ffe7bf"), LightEnergy = .22f, OmniRange = l.Id == "living" ? 5.5f : 4.8f,
                ShadowEnabled = true, Visible = false };
            AddChild(light); _lights.Add(l.Id, light);
        }
        foreach (var (x, z, sx, sz) in new[] { (-3.9f, 0f, 3.6f, 8.4f), (0f, 0f, 3.5f, 8.4f), (3.9f, 0f, 3.6f, 8.4f) })
            AddChild(new ReflectionProbe { Position = new(x, 1.5f, z), Size = new(sx, 3, sz), BoxProjection = true, Interior = true });
    }

    private void BuildUi()
    {
        var layer = new CanvasLayer(); AddChild(layer);
        var root = new Control { Name = "HomeUi", MouseFilter = Control.MouseFilterEnum.Ignore, Theme = UiStyles.CreateTheme(1, cinematic: true) };
        root.SetAnchorsAndOffsetsPreset(Control.LayoutPreset.FullRect); layer.AddChild(root);
        var title = new Label { Text = UiText.Get("HOME_TITLE"), Position = new(18, 13), MouseFilter = Control.MouseFilterEnum.Ignore, Visible = !EmbeddedInVillage };
        title.AddThemeFontSizeOverride("font_size", 12); root.AddChild(title);
        title.AddThemeColorOverride("font_shadow_color", new Color(0,0,0,.8f)); title.AddThemeConstantOverride("shadow_outline_size", 2);
        _instructions = new Label { Text = UiText.Get("HOME_CONTROLS_KEYBOARD"), Position = new(18, 333), MouseFilter = Control.MouseFilterEnum.Ignore, Visible = !EmbeddedInVillage };
        root.AddChild(_instructions);
        _instructions.AddThemeColorOverride("font_shadow_color", new Color(0,0,0,.8f)); _instructions.AddThemeConstantOverride("shadow_outline_size", 2);
        _prompt = new Label { Position = new(180, 300), Size = new(280, 22), HorizontalAlignment = HorizontalAlignment.Center,
            MouseFilter = Control.MouseFilterEnum.Ignore };
        _prompt.AddThemeColorOverride("font_shadow_color", Colors.Black); _prompt.AddThemeConstantOverride("shadow_outline_size", 3);
        root.AddChild(_prompt);
        _pause = new ColorRect { Color = new Color(0.035f, .05f, .055f, .83f), Visible = false, MouseFilter = Control.MouseFilterEnum.Stop };
        root.AddChild(_pause); _pause.SetAnchorsAndOffsetsPreset(Control.LayoutPreset.FullRect);
        var menu = new VBoxContainer { Position = new(210, 102), Size = new(220, 155) }; _pause.AddChild(menu);
        menu.AddChild(new Label { Text = UiText.Get("HOME_PAUSE"), HorizontalAlignment = HorizontalAlignment.Center });
        _resume = AddButton(menu, "HOME_RESUME", () => SetPaused(false));
        AddButton(menu, "HOME_RESET", ResetPreview);
        AddButton(menu, "HOME_EXIT", () => GetTree().Quit());
    }

    private static Button AddButton(Node host, string key, Action pressed)
    {
        var button = new Button { Text = UiText.Get(key), CustomMinimumSize = new(220, 28) };
        button.Pressed += pressed; host.AddChild(button); return button;
    }
}
