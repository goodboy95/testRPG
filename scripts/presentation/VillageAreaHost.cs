using Godot;
using TownRpg.Core.Domain;

namespace TownRpg.Presentation;

/// <summary>Shared area lifetime, movement and transitions. The owner handles session persistence.</summary>
public partial class VillageAreaHost : Node
{
    public bool EmbeddedInGame { get; init; }
    protected virtual bool TerrainSample=>false;
    // Regression switches apply only to independent previews; formal saves always use the adopted surface.
    private bool OriginalTerrain=>OS.GetCmdlineUserArgs().Contains("--village-original-terrain") || OS.GetCmdlineUserArgs().Contains("--terrain-baseline");
    public event Action? CheckpointReached;
    private bool _hostLocked;
    private PlayerSettings _settings = new();
    private Dictionary<string, bool> _savedDoors = new();
    private Dictionary<string, bool> _savedLights = new();
    private bool _restoring;
    public bool Graybox { get; } = OS.GetCmdlineUserArgs().Contains("--village-graybox");
    public bool UseV1 { get; } = OS.GetCmdlineUserArgs().Contains("--village-v1");
    public VillageExterior3D Exterior { get; private set; } = null!;
    public HomePreviewController3D? Interior { get; private set; }
    public bool IsInterior { get; private set; }
    public bool IsTransitioning { get; private set; }
    public bool IsPaused { get; private set; }
    public PlayerController3D ActivePlayer => IsInterior ? Interior!.PlayerActor : Exterior.PlayerActor;
    public ThirdPersonCameraRig ActiveCamera => IsInterior ? Interior!.CameraController : Exterior.CameraController;
    public IReadOnlyList<double> TransitionTimesMs => _transitionTimes;
    private readonly List<double> _transitionTimes=[];
    private Node _areaHost=null!;
    private ColorRect _fade=null!;
    private Control _pause=null!;
    private Label _areaName=null!;
    private Label _instructions=null!;
    private Label _prompt=null!;
    private Button _resume=null!;
    private bool _gamepad;
    private bool _exiting;
    // Keep the shared packed character alive across the cached exterior/interior instances.
    // A local-only ResourceLoader wrapper can otherwise be finalized before the next reload.
    private PackedScene? _characterModelResource;

    public override void _Ready()
    {
        if(ResourceLoader.Exists(PlayerController3D.DefaultCharacterModelPath))
            _characterModelResource=ResourceLoader.Load<PackedScene>(PlayerController3D.DefaultCharacterModelPath);
        _areaHost=new Node { Name="AreaHost" };AddChild(_areaHost);BuildUi();
        Exterior=new VillageExterior3D { Name="VillageExterior",Graybox=!EmbeddedInGame && Graybox,UseV1=!EmbeddedInGame && UseV1,
            UseTerrainSample=EmbeddedInGame || (!UseV1 && !Graybox && !OriginalTerrain) };
        Exterior.HomeRequested+=EnterHome;
        Exterior.PromptChanged+=OnExteriorPrompt;
        _areaHost.AddChild(Exterior);
        UpdateLabels();
    }
    private void EnterHome() => _=SwitchArea(true);
    private void LeaveHome() => _=SwitchArea(false);
    private void OnExteriorPrompt(string text) { if(!IsInterior) _prompt.Text=text; }

    public override void _ExitTree()
    {
        _exiting=true;
        if(Exterior is not null)
        {
            Exterior.HomeRequested-=EnterHome;Exterior.PromptChanged-=OnExteriorPrompt;
            if(Exterior.GetParent() is null) Exterior.Free();
        }
        if(Interior is not null)
        {
            Interior.VillageExitRequested-=LeaveHome;
            if(Interior.GetParent() is null) Interior.Free();
        }
        GC.KeepAlive(_characterModelResource);
        Input.MouseMode=Input.MouseModeEnum.Visible;
    }
    public override void _Input(InputEvent e)
    {
        if(EmbeddedInGame || e is InputEventKey { Echo:true }) return;
        if(e is InputEventJoypadButton or InputEventJoypadMotion) SetGamepadPrompts(true);
        else if(e is InputEventKey or InputEventMouseButton) SetGamepadPrompts(false);
        if(e.IsActionPressed("ui_cancel"))
        {
            if(!IsTransitioning) SetPaused(!IsPaused);
            GetViewport().SetInputAsHandled();
        }
    }
    public void SetGamepadPrompts(bool value)
    {
        if(_gamepad==value) return;
        _gamepad=value;Exterior.SetGamepadPrompts(value);Interior?.SetGamepadPrompts(value);UpdateLabels();
    }
    private void Lock(bool value) { Exterior.SetHostLocked(value || _hostLocked);Interior?.SetHostLocked(value || _hostLocked); }
    public void SetHostLocked(bool value) { _hostLocked=value;Lock(IsPaused || IsTransitioning); if (EmbeddedInGame && _instructions is not null) _instructions.Visible=!value; }
    public void SetPaused(bool value)
    {
        if(IsTransitioning) return;
        IsPaused=value;_pause.Visible=value;Lock(value);
        if(value) _resume.GrabFocus();else GetViewport().GuiGetFocusOwner()?.ReleaseFocus();
    }
    public async Task<bool> SwitchArea(bool enterHome)
    {
        if(_exiting || IsTransitioning || IsPaused || _hostLocked || enterHome==IsInterior) return false;
        IsTransitioning=true;Lock(true);_prompt.Text=string.Empty;
        var start=Time.GetTicksUsec();
        await Fade(1);
        if(_exiting) return false;
        var yaw=ActiveCamera.YawDegrees;
        if(Interior is not null) { _savedDoors=Interior.CaptureDoors();_savedLights=Interior.CaptureLights(); }
        var previous=IsInterior ? (Node)Interior! : Exterior;
        if(!IsInterior) Exterior.SetSurfaceActive(false);
        previous.ProcessMode=ProcessModeEnum.Disabled;_areaHost.RemoveChild(previous);
        if(enterHome)
        {
            if(Interior is null)
            {
                Interior=new HomePreviewController3D { Name="VillageHomeInterior",EmbeddedInVillage=true };
                Interior.VillageExitRequested+=LeaveHome;
                Interior.FixturesChanged+=OnFixturesChanged;
            }
            _areaHost.AddChild(Interior);Interior.SetHostLocked(true);
            Interior.ApplySettings(_settings);
            Interior.RestoreFixtures(_savedDoors,_savedLights);
            var entry=HomeLayout3D.Layout.Doors.Single(d=>d.Id=="entry");
            Interior.PlayerActor.GlobalPosition=new(entry.Position[0],entry.Position[1],entry.Position[2]-1.3f);
            Interior.PlayerActor.SetFacingImmediate(Vector3.Forward);
        }
        else
        {
            _areaHost.AddChild(Exterior);Exterior.SetHostLocked(true);
            Exterior.SetSurfaceActive(true);
            Exterior.PlayerActor.GlobalPosition=Exterior.HomeReturnPoint;Exterior.PlayerActor.SetFacingImmediate(Exterior.HomeForward);
        }
        IsInterior=enterHome;
        var next=IsInterior ? (Node)Interior! : Exterior;
        next.ProcessMode=ProcessModeEnum.Inherit;
        ActiveCamera.SetYawImmediate(yaw+(enterHome?-VillageLayout3D.Home.Yaw:VillageLayout3D.Home.Yaw));ActiveCamera.SnapToTarget();ActiveCamera.Camera.MakeCurrent();
        UpdateLabels();
        await ToSignal(GetTree(),SceneTree.SignalName.PhysicsFrame);
        await Fade(0);
        if(_exiting) return false;
        IsTransitioning=false;Lock(false);
        _transitionTimes.Add((Time.GetTicksUsec()-start)/1000d);
        if (!_restoring) CheckpointReached?.Invoke();
        return true;
    }
    private async Task Fade(float alpha)
    {
        _fade.MouseFilter=Control.MouseFilterEnum.Stop;
        var tween=CreateTween();tween.TweenProperty(_fade,"color:a",alpha,.2);
        await ToSignal(tween,Tween.SignalName.Finished);
        if(!_exiting && alpha==0) _fade.MouseFilter=Control.MouseFilterEnum.Ignore;
    }
    public async Task ResetPreview()
    {
        if(IsTransitioning || _exiting) return;
        SetPaused(false);
        if(IsInterior) await SwitchArea(false);
        if(_exiting) return;
        if(Interior is not null)
        {
            Interior.VillageExitRequested-=LeaveHome;
            Interior.Free();Interior=null;
        }
        Exterior.ResetPosition();Lock(false);UpdateLabels();
    }
    private void UpdateLabels()
    {
        _areaName.Text=EmbeddedInGame ? (IsInterior ? "家中 · 周六早晨" : "村庄 · 周六早晨") : UiText.Get(IsInterior ? "VILLAGE_HOME_TITLE" : "VILLAGE_TITLE");
        if(TerrainSample && !EmbeddedInGame && !IsInterior) _areaName.Text="地形样板 · 夏日上午";
        _instructions.Text=EmbeddedInGame ? (_gamepad ? "左摇杆 移动　右摇杆 镜头　A 互动　Menu 暂停" : "WASD 移动　Shift 慢走　鼠标 镜头　F 互动　Esc 暂停") : UiText.Get(_gamepad?"VILLAGE_CONTROLS_PAD":"VILLAGE_CONTROLS_KEYBOARD");
        if(IsInterior) _prompt.Text=string.Empty;
    }

    private void OnFixturesChanged()
    {
        if (!_restoring && !IsTransitioning) CheckpointReached?.Invoke();
    }

    public void ApplySettings(PlayerSettings settings)
    {
        _settings = settings;
        Exterior.ApplySettings(settings); Interior?.ApplySettings(settings);
        if (_areaName is not null) UiStyles.ApplyScale(_areaName.GetParent<Control>(), settings.FontScale);
    }

    public VillageExplorationState Capture()
    {
        var p = ActivePlayer.GlobalPosition;
        var direction = ActivePlayer.FacingWorld;
        return new() { X=p.X, Y=p.Y, Z=p.Z, FacingRadians=Mathf.Atan2(direction.X,direction.Z),
            CameraYaw=ActiveCamera.YawDegrees, Doors=Interior?.CaptureDoors() ?? new(_savedDoors),
            Lights=Interior?.CaptureLights() ?? new(_savedLights) };
    }

    public async Task RestoreAsync(string location, VillageExplorationState snapshot)
    {
        _restoring=true;
        _savedDoors=new(snapshot.Doors);_savedLights=new(snapshot.Lights);
        _hostLocked=false;
        try
        {
            if ((location == VillageExplorationState.HomeLocation) != IsInterior)
                await SwitchArea(location == VillageExplorationState.HomeLocation);
            if (_exiting) return;
            SetHostLocked(true);
            Interior?.RestoreFixtures(_savedDoors,_savedLights);
            ApplySettings(_settings);
            await ToSignal(GetTree(),SceneTree.SignalName.PhysicsFrame);
            if (_exiting) return;
            var candidate = new Vector3(snapshot.X,snapshot.Y,snapshot.Z);
            // Old v2 saves retain their X/Z on small terrain edits. Large height errors still fall back.
            if (!IsInterior && Exterior.UseTerrainSample && Exterior.Surface.Ready)
            {
                var ground=Exterior.WalkHeight(candidate.X,candidate.Z);
                if (float.IsFinite(ground) && Math.Abs(candidate.Y-ground)<=.5f) candidate.Y=ground+.04f;
            }
            if (SafePose(candidate)) ActivePlayer.GlobalPosition=candidate;
            else MoveToSafePoint();
            ActivePlayer.Velocity=Vector3.Zero;
            ActivePlayer.SetFacingImmediate(new(Mathf.Sin(snapshot.FacingRadians),0,Mathf.Cos(snapshot.FacingRadians)));
            ActiveCamera.SetYawImmediate(snapshot.CameraYaw);ActiveCamera.SnapToTarget();
        }
        finally { _restoring=false; }
    }

    public bool SafePose(Vector3 p)
    {
        if (!float.IsFinite(p.X) || !float.IsFinite(p.Y) || !float.IsFinite(p.Z)) return false;
        if (IsInterior)
        {
            // Embedded home exits through the front door; its duplicated yard is never a saved area.
            if (p.X is < -5.6f or > 5.6f || p.Z is < -4.1f or > 4.1f || Math.Abs(p.Y) > .15f) return false;
        }
        else
        {
            var bounds=VillageLayout3D.Layout.Bounds;
            if (p.X < bounds[0]+1 || p.X > bounds[0]+bounds[2]-1 || p.Z < bounds[1]+1 || p.Z > bounds[1]+bounds[3]-1 ||
                !Exterior.Surface.Ready || !float.IsFinite(Exterior.WalkHeight(p.X,p.Z)) ||
                Math.Abs(p.Y-Exterior.WalkHeight(p.X,p.Z)) > .5f) return false;
        }
        using var shape=new CapsuleShape3D { Radius=.27f,Height=1.6f };
        using var query=new PhysicsShapeQueryParameters3D { Shape=shape, Transform=new(Basis.Identity,p+Vector3.Up*.94f),
            CollisionMask=1, Exclude=new Godot.Collections.Array<Rid> { ActivePlayer.GetRid() } };
        return ActivePlayer.GetWorld3D().DirectSpaceState.IntersectShape(query,1).Count==0;
    }

    public void MoveToSafePoint()
    {
        if(IsInterior)
        {
            var candidates=HomeLayout3D.Layout.SafePoints.Select(p=>new Vector3(p[0],p[1],p[2]));
            ActivePlayer.GlobalPosition=candidates.First(SafePose);
        }
        else Exterior.ResetPosition();
        ActivePlayer.Velocity=Vector3.Zero;ActiveCamera.SnapToTarget();
    }
    private void BuildUi()
    {
        var layer=new CanvasLayer { Name="VillageUi",Layer=EmbeddedInGame ? 5 : 20 };AddChild(layer);
        var root=new Control { MouseFilter=Control.MouseFilterEnum.Ignore,Theme=UiStyles.CreateTheme(1,cinematic:true) };
        root.SetAnchorsAndOffsetsPreset(Control.LayoutPreset.FullRect);layer.AddChild(root);
        _areaName=Label(root,new(18,13),new(300,24),12);
        _instructions=Label(root,new(18,333),new(600,18),8);
        _prompt=Label(root,new(170,300),new(300,22),9);_prompt.HorizontalAlignment=HorizontalAlignment.Center;
        _pause=new ColorRect { Color=new(.035f,.05f,.055f,.9f),Visible=false,MouseFilter=Control.MouseFilterEnum.Stop };
        root.AddChild(_pause);_pause.SetAnchorsAndOffsetsPreset(Control.LayoutPreset.FullRect);
        var menu=new VBoxContainer { Position=new(210,100),Size=new(220,160) };_pause.AddChild(menu);
        menu.AddChild(new Label { Text=UiText.Get("VILLAGE_PAUSE"),HorizontalAlignment=HorizontalAlignment.Center });
        _resume=Button(menu,"HOME_RESUME",()=>SetPaused(false));
        Button(menu,"VILLAGE_RESET",()=>_=ResetPreview());
        Button(menu,"HOME_EXIT",()=>GetTree().Quit());
        _fade=new ColorRect { Color=new(0,0,0,0),MouseFilter=Control.MouseFilterEnum.Ignore };
        root.AddChild(_fade);_fade.SetAnchorsAndOffsetsPreset(Control.LayoutPreset.FullRect);
    }
    private static Label Label(Control parent,Vector2 position,Vector2 size,int font)
    {
        var label=new Label { Position=position,Size=size,MouseFilter=Control.MouseFilterEnum.Ignore };
        label.AddThemeFontSizeOverride("font_size",font);label.AddThemeColorOverride("font_shadow_color",new Color(0,0,0,.85f));
        label.AddThemeConstantOverride("shadow_outline_size",2);parent.AddChild(label);return label;
    }
    private static Button Button(Node parent,string key,Action action)
    {
        var button=new Button { Text=UiText.Get(key),CustomMinimumSize=new(220,28) };button.Pressed+=action;parent.AddChild(button);return button;
    }
}
