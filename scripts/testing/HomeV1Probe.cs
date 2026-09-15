using System.Text.Json;
using Godot;
using TownRpg.Application;
using TownRpg.Core.Services;
using TownRpg.Presentation;

namespace TownRpg.Testing;

public partial class HomeV1Probe : Node
{
    private HomePreviewController3D _home = null!;
    private readonly bool _capture = OS.GetCmdlineUserArgs().Contains("--home-capture");
    private readonly bool _gray = OS.GetCmdlineUserArgs().Contains("--home-graybox");
    private readonly List<string> _checks = [];
    private string Output => ProjectSettings.GlobalizePath(OS.GetCmdlineUserArgs().Contains("--home-access") ? "res://outputs/home-access/home-regression" : _gray ? "res://outputs/home-v1/graybox" : "res://outputs/home-v1/validation");
    public override void _Ready() => CallDeferred(MethodName.Run);

    private async void Run()
    {
        try
        {
            Directory.CreateDirectory(Output);
            var session = GetNode<GameSession>("/root/GameSession");
            var stateBefore = SaveGameSerializer.Serialize(session.State);
            var savePath = ProjectSettings.GlobalizePath(GameSession.SavePath);
            var saveBefore = File.Exists(savePath) ? File.ReadAllBytes(savePath) : null;
            var started = Time.GetTicksUsec();
            _home = ResourceLoader.Load<PackedScene>("res://scenes/testing/HomePreview.tscn").Instantiate<HomePreviewController3D>();
            AddChild(_home); await Frames(5);
            var loadMs = (Time.GetTicksUsec() - started) / 1000d;
            Require(_home.InteractionMarkerCount == 11 && _home.AllDoorsClosed && _home.AllLightsOff, "initial state");
            _home.PlayerActor.SetPhysicsProcess(false);
            await Doors();
            await Lights();
            await PhysicalRoutes();
            await ValidateInput();
            await ValidateCamera();
            await WalkHome();
            Require(SaveGameSerializer.Serialize(session.State) == stateBefore, "session unchanged");
            Require(saveBefore is null ? !File.Exists(savePath) : File.ReadAllBytes(savePath).SequenceEqual(saveBefore), "formal save unchanged");
            if (_capture) await CaptureMatrix();
            _home.ResetPreview(); await Frames(3);
            Require(_home.AllDoorsClosed && _home.AllLightsOff && _home.PlayerActor.Position.IsEqualApprox(new(-3.1f, 0, 2.8f)), "reset");
            _home.QueueFree(); await Frames(3);
            _home = ResourceLoader.Load<PackedScene>("res://scenes/testing/HomePreview.tscn").Instantiate<HomePreviewController3D>();
            AddChild(_home); await Frames(3);
            Require(_home.AllDoorsClosed && _home.AllLightsOff, "instance reload");
            _home.PlayerActor.ConfigureDefaultCharacterVisual(DreamCharacterFrames.Create(ResourceLoader.Load<Texture2D>(
                "res://assets/generated/dream_escape/pixel/protagonist-walk-4x4-v1.png")), "res://tests/home-missing-model.glb");
            Require(!_home.PlayerActor.UsesDefault3DModel && _home.PlayerActor.CharacterSprite is not null, "billboard fallback");
            File.WriteAllText(Path.Combine(Output, "results.json"), JsonSerializer.Serialize(new {
                status = "pass", graybox = _gray, loadMs, checks = _checks, captured = _capture,
                renderer = RenderingServer.GetVideoAdapterName(), humanAcceptance = "pending"
            }, new JsonSerializerOptions { WriteIndented = true }));
            GD.Print($"HOME_V1_PROBE_PASS checks={_checks.Count} graybox={_gray} loadMs={loadMs:F1}");
            GetTree().Quit();
        }
        catch (Exception e)
        {
            GD.PushError("HOME_V1_PROBE_FAIL " + e);
            GetTree().Quit(1);
        }
    }

    private async Task Doors()
    {
        foreach (var d in HomeLayout3D.Layout.Doors)
        {
            var p = V(d.Position);
            var near = d.Axis == "x" ? p + Vector3.Back * 1.2f : p + Vector3.Right * (-d.Swing * 1.2f);
            var across = d.Axis == "x" ? p - Vector3.Back * 1.2f : p + Vector3.Right * (d.Swing * 1.2f);
            await Place(near);
            Require(_home.PlayerActor.TestMove(_home.PlayerActor.GlobalTransform, across - near), "closed collision " + d.Id);
            Require(_home.HasLineOfSight("home.door." + d.Id), "door focus " + d.Id);
            Require(_home.TryOperate("home.door." + d.Id), "open " + d.Id);
            Require(!_home.TryOperate("home.door." + d.Id), "repeat rejected " + d.Id);
            await Frames(36);
            Require(_home.DoorOpen(d.Id), "open complete " + d.Id);
            Require(!_home.PlayerActor.TestMove(_home.PlayerActor.GlobalTransform, across - near), "open passage " + d.Id);
            await Place(p);
            Require(_home.TryOperate("home.door." + d.Id), "begin closing occupied doorway " + d.Id);
            await Frames(36);
            Require(_home.DoorMoving(d.Id), "closing waits for occupied doorway " + d.Id);
            await Place(near);
            await Frames(36);
            Require(!_home.DoorOpen(d.Id), "closed again " + d.Id);
            _home.TryOperate("home.door." + d.Id); await Frames(36);
        }
        await Place(new(-3.4f, 0, -1.1f));
        Require(!_home.HasLineOfSight("home.door.kitchen"), "occluded opposite room");
    }

    private async Task Lights()
    {
        foreach (var l in HomeLayout3D.Layout.Lights)
        {
            var p = V(l.Switch); p.Y = 0;
            p += l.Id is "living" ? Vector3.Forward * .7f : l.Id is "porch" ? Vector3.Back * .7f : Vector3.Right * (p.X < 0 ? -.7f : .7f);
            await Place(p);
            Require(_home.TryOperate("home.light." + l.Id) && _home.LightOn(l.Id), "light on " + l.Id);
            Require(_home.TryOperate("home.light." + l.Id) && !_home.LightOn(l.Id), "light off " + l.Id);
        }
        // The two entry switches occupy opposite faces of the same wall.
        await Place(new(1.1f, 0, 3.8f));
        Require(_home.HasLineOfSight("home.light.living") && !_home.HasLineOfSight("home.light.porch"), "switch wall occlusion");
    }

    private async Task PhysicalRoutes()
    {
        // Real swept capsule queries, both directions, including every room and all four house sides.
        var routes = new (Vector3, Vector3)[] {
            (new(-3.1f,0,2.8f),new(-3.1f,0,2.1f)),
            (new(-3.1f,0,2.1f),new(.65f,0,2.1f)),
            (new(.65f,0,2.1f),new(.65f,0,-2.5f)),
            (new(-.65f,0,-2.5f),new(-3.1f,0,-2.5f)),
            (new(.65f,0,2),new(4.15f,0,2)),
            (new(.65f,0,-2.5f),new(4.1f,0,-2.5f)),
            (new(0,0,2.9f),new(0,0,10.7f)),
            (new(-6.7f,0,5.8f),new(-6.7f,0,-5.1f)),
            (new(6.7f,0,5.8f),new(6.7f,0,-5.1f)),
            (new(-6.7f,0,-5.1f),new(6.7f,0,-5.1f))
        };
        foreach (var (a, b) in routes)
        foreach (var (from, to) in new[] { (a, b), (b, a) })
        {
            await Place(from);
            Require(!_home.PlayerActor.TestMove(_home.PlayerActor.GlobalTransform, to - from), $"route {from}->{to}");
        }
        await Place(new(0,0,10.8f));
        Require(_home.PlayerActor.TestMove(_home.PlayerActor.GlobalTransform,new(0,0,2)), "closed yard boundary");
    }

    private async Task ValidateInput()
    {
        await Place(new(0,0,7.8f));
        _home.PlayerActor._Notification((int)NotificationWMWindowFocusIn);
        _home.CameraController.SetYawImmediate(0); _home.CameraController.SnapToTarget();
        _home.PlayerActor.SetFacingImmediate(Vector3.Forward);
        _home.PlayerActor.SetPhysicsProcess(true);
        var start = _home.PlayerActor.Position;
        Input.ActionPress("move_up"); await Frames(25); Input.ActionRelease("move_up"); await Frames(4);
        var jogDistance = start.DistanceTo(_home.PlayerActor.Position);
        Require(jogDistance > .5f, $"actual move input distance={jogDistance:F3}");
        await Place(new(0,0,7.8f)); _home.PlayerActor.SetFacingImmediate(Vector3.Forward); start = _home.PlayerActor.Position;
        Input.ActionPress("move_slow"); Input.ActionPress("move_up"); await Frames(25);
        Input.ActionRelease("move_up"); Input.ActionRelease("move_slow"); await Frames(4);
        Require(start.DistanceTo(_home.PlayerActor.Position) < jogDistance, "shift slower than jog");
        _home.SetPaused(true); start = _home.PlayerActor.Position;
        Input.ActionPress("move_up"); await Frames(12); Input.ActionRelease("move_up");
        Require(start.IsEqualApprox(_home.PlayerActor.Position) && Input.MouseMode == Input.MouseModeEnum.Visible, "pause input gate");
        _home.SetPaused(false); await Frames(3);
        _home.PlayerActor._Notification((int)NotificationWMWindowFocusOut);
        Input.ActionPress("move_up"); await Frames(12); Input.ActionRelease("move_up");
        Require(start.IsEqualApprox(_home.PlayerActor.Position), "focus loss movement reset");
        _home.PlayerActor._Notification((int)NotificationWMWindowFocusIn);
        var yaw = _home.CameraController.YawDegrees;
        Require(_home.CameraController.TryApplyMouseMotion(new(40,0),true) && _home.CameraController.YawDegrees != yaw, "orbit mouse delta");
        _home.PlayerActor.SetPhysicsProcess(false);
        // Dispatch through Godot's event route, not directly to the handler.
        await Place(new(1.1f,0,3.6f)); await Frames(3);
        var lit = _home.LightOn("living");
        Input.ParseInputEvent(new InputEventKey { Keycode = Key.F, PhysicalKeycode = Key.F, Pressed = true }); await ProcessFrames(3);
        Input.ParseInputEvent(new InputEventKey { Keycode = Key.F, PhysicalKeycode = Key.F, Pressed = false });
        await ProcessFrames(3);
        Require(_home.LightOn("living") != lit, "keyboard F dispatch");
        Input.ParseInputEvent(new InputEventJoypadButton { ButtonIndex = JoyButton.A, Pressed = true }); await ProcessFrames(3);
        Input.ParseInputEvent(new InputEventJoypadButton { ButtonIndex = JoyButton.A, Pressed = false });
        await ProcessFrames(3);
        Require(_home.LightOn("living") == lit, $"gamepad A dispatch target={_home.CurrentInteractionTargetId} paused={_home.IsPaused}");
        await KeyInput(Key.Escape);
        Require(_home.IsPaused && !_home.PlayerActor.InputEnabled, "Esc opens pause through input route");
        await JoyInput(JoyButton.B);
        Require(!_home.IsPaused && GetViewport().GuiGetFocusOwner() is null, "B resumes and releases menu focus");
        await KeyInput(Key.Escape); await KeyInput(Key.Down);
        Require(GetViewport().GuiGetFocusOwner() is Button button && button.Text == UiText.Get("HOME_RESET"), "pause menu keyboard focus");
        await KeyInput(Key.Enter);
        Require(!_home.IsPaused && _home.AllDoorsClosed && _home.AllLightsOff, "reset button input resets preview");
    }

    private async Task CaptureMatrix()
    {
        _home.PlayerActor.SetPhysicsProcess(true);
        var views = new (string, Vector3, float)[] {
            ("bedroom",new(-3.1f,0,2.3f),0), ("bedroom-door",new(-2.85f,0,3.25f),-80),
            ("family",new(-2.6f,0,-3.55f),110), ("living",new(.55f,0,2.95f),0),
            ("dining",new(.3f,0,-2.45f),0), ("kitchen",new(3.9f,0,1.8f),-15),
            ("bathroom",new(3.9f,0,-2.55f),-15), ("porch",new(0,0,6.3f),0),
            ("yard",new(0,0,10.2f),0), ("west",new(-6.7f,0,1),-90),
            ("east",new(6.7f,0,1),90), ("rear",new(0,0,-5.2f),180)
        };
        foreach (var resolution in new[] { new Vector2I(1280,720),new Vector2I(1600,900),new Vector2I(1920,1080) })
        {
            DisplayServer.WindowSetMode(DisplayServer.WindowMode.Windowed);
            DisplayServer.WindowSetFlag(DisplayServer.WindowFlags.Borderless,true);
            DisplayServer.WindowSetSize(resolution); await Frames(12);
            var folder = Path.Combine(Output,$"{resolution.X}x{resolution.Y}"); Directory.CreateDirectory(folder);
            foreach (var lights in new[] {false,true})
            {
                foreach (var l in HomeLayout3D.Layout.Lights)
                {
                    if (_home.LightOn(l.Id)==lights) continue;
                    var p=V(l.Switch);p.Y=0;
                    p+=l.Id=="living"?Vector3.Forward*.7f:l.Id=="porch"?Vector3.Back*.7f:Vector3.Right*(p.X<0?-.7f:.7f);
                    await Place(p);_home.TryOperate("home.light."+l.Id);
                }
                foreach (var (name,p,yaw) in views)
                {
                    await Place(p);_home.CameraController.SetYawImmediate(yaw);_home.CameraController.SnapToTarget();
                    _home.PlayerActor.SetFacingImmediate(_home.CameraController.HorizontalForward);
                    await Frames(12); await ToSignal(RenderingServer.Singleton,RenderingServer.SignalName.FramePostDraw);
                    var image=GetViewport().GetTexture().GetImage();
                    Require(image.GetWidth()==resolution.X && image.GetHeight()==resolution.Y,"capture resolution");
                    image.SavePng(Path.Combine(folder,name+(lights?"-lit":"-day")+".png"));
                }
            }
            // Architectural views use a separate camera, so the complete roof and all facades can be reviewed.
            var reviewCamera=new Camera3D { Fov=55, Near=.05f, Far=100 };
            _home.AddChild(reviewCamera);_home.PlayerActor.Visible=false;reviewCamera.Current=true;
            foreach(var (name,position) in new[] {("exterior-front",new Vector3(10,7,17)),("exterior-west",new Vector3(-15,7,8)),
                ("exterior-east",new Vector3(15,7,8)),("exterior-rear",new Vector3(-10,7,-13))})
            {
                reviewCamera.Position=position;reviewCamera.LookAt(new Vector3(0,1.8f,1));
                await Frames(10);await ToSignal(RenderingServer.Singleton,RenderingServer.SignalName.FramePostDraw);
                GetViewport().GetTexture().GetImage().SavePng(Path.Combine(folder,name+".png"));
            }
            reviewCamera.QueueFree();_home.PlayerActor.Visible=true;_home.CameraController.Camera.Current=true;
            await Place(new(.6f,0,2.1f));_home.CameraController.SnapToTarget();await Frames(30);
            var times=new List<double>();
            for(var frame=0;frame<180;frame++)
            {
                _home.CameraController.SetYawImmediate(frame*2);
                var before=Time.GetTicksUsec();await ToSignal(RenderingServer.Singleton,RenderingServer.SignalName.FramePostDraw);
                times.Add((Time.GetTicksUsec()-before)/1000d);
            }
            var sorted=times.Order().ToArray();
            File.WriteAllText(Path.Combine(folder,"frame-times.json"),JsonSerializer.Serialize(new {
                frames=times.Count,meanMs=times.Average(),p95Ms=sorted[(int)(sorted.Length*.95)],maxMs=times.Max(),
                note="Local rendered wall-clock samples including frame pacing; not target-hardware certification."
            },new JsonSerializerOptions {WriteIndented=true}));
        }
    }

    private async Task WalkHome()
    {
        _home.ResetPreview();
        _home.PlayerActor._Notification((int)NotificationWMWindowFocusIn);
        _home.PlayerActor.SetPhysicsProcess(true);
        _home.CameraController.SetYawImmediate(0);_home.CameraController.SnapToTarget();
        await WalkTo(new(-3.1f,0,3.25f));await WalkTo(new(-2.9f,0,3.25f));
        Require(_home.TryOperate("home.door.bedroom"),"walkthrough bedroom exit");await Frames(36);
        await WalkTo(new(-2.9f,0,2.1f));await WalkTo(new(.65f,0,2.1f));
        await WalkTo(new(.65f,0,-2.5f));await WalkTo(new(-.65f,0,-2.5f));
        Require(_home.TryOperate("home.door.family"),"walkthrough family entrance");await Frames(36);
        await WalkTo(new(-2.8f,0,-2.5f));await WalkTo(new(.65f,0,-2.5f));
        Require(_home.TryOperate("home.door.bathroom"),"walkthrough bathroom entrance");await Frames(36);
        await WalkTo(new(3.9f,0,-2.5f));await WalkTo(new(.65f,0,-2.5f));
        await WalkTo(new(.65f,0,2));
        Require(_home.TryOperate("home.door.kitchen"),"walkthrough kitchen entrance");await Frames(36);
        await WalkTo(new(3.9f,0,2));await WalkTo(new(.65f,0,2));await WalkTo(new(0,0,3.1f));
        await InputDoor();await Frames(36);
        Require(_home.DoorOpen("entry") && _home.DoorOpenSide("entry")==1,"input pushes double entry outside");
        await WalkTo(new(0,0,9.8f));await WalkTo(new(-6.7f,0,9.8f));
        await WalkTo(new(-6.7f,0,-5.1f));await WalkTo(new(6.7f,0,-5.1f));
        await WalkTo(new(6.7f,0,6.1f));await WalkTo(new(6.3f,0,6.1f));
        await WalkTo(new(6.3f,0,9.8f));await WalkTo(new(0,0,9.8f));
        await WalkTo(new(0,0,5.2f));await InputDoor();await Frames(36);
        Require(!_home.DoorOpen("entry"),"double entry closes along same direction");
        await WalkTo(new(0,0,5.1f));await InputDoor();await Frames(36);
        Require(_home.DoorOpen("entry") && _home.DoorOpenSide("entry")==-1,"input pushes double entry inside");
        await WalkTo(new(0,0,3.1f));
        _home.PlayerActor.SetPhysicsProcess(false);
        Require(true,"continuous input walkthrough all rooms and courtyard");
    }

    private async Task InputDoor()
    {
        Input.ParseInputEvent(new InputEventKey{PhysicalKeycode=Key.F,Keycode=Key.F,Pressed=true});await Frames(2);
        Input.ParseInputEvent(new InputEventKey{PhysicalKeycode=Key.F,Keycode=Key.F});await Frames(3);
    }

    private async Task ValidateCamera()
    {
        _home.PlayerActor.SetPhysicsProcess(true);
        await Place(new(-3.7f,0,3.85f));
        _home.CameraController.SetYawImmediate(0);_home.CameraController.SnapToTarget();await Frames(12);
        Require(_home.CameraController.SpringArm.GetHitLength()<.65f && _home.CameraController.Camera.GlobalPosition.Z<4.5f,
            "window glazing stops orbit camera leaving bedroom");
        if(_home.PlayerActor.CharacterModel is not null)Require(!_home.PlayerActor.CharacterModel.Visible,"near-window character hidden");
        await Place(new(-3.1f,0,2.8f));_home.CameraController.SnapToTarget();await Frames(12);
        Require(_home.CameraController.SpringArm.GetHitLength()>.8f,"camera recovers away from window");
        if(_home.PlayerActor.CharacterModel is not null)Require(_home.PlayerActor.CharacterModel.Visible,"character visible again");
        _home.PlayerActor.SetPhysicsProcess(false);
    }

    private async Task WalkTo(Vector3 destination)
    {
        var actions=new[] {"move_left","move_right","move_up","move_down"};
        for(var frame=0;frame<900;frame++)
        {
            var delta=destination-_home.PlayerActor.Position;delta.Y=0;
            if(delta.Length()<.11f)
            {
                foreach(var a in actions)Input.ActionRelease(a);
                await Frames(10);return;
            }
            var direction=delta.Normalized();
            var strength=Mathf.Clamp(delta.Length()*1.8f,.22f,1);
            Input.ActionPress("move_left",Math.Max(0,-direction.X)*strength);
            Input.ActionPress("move_right",Math.Max(0,direction.X)*strength);
            Input.ActionPress("move_up",Math.Max(0,-direction.Z)*strength);
            Input.ActionPress("move_down",Math.Max(0,direction.Z)*strength);
            await Frames(1);
        }
        foreach(var a in actions)Input.ActionRelease(a);
        throw new InvalidOperationException($"Walk blocked at {_home.PlayerActor.Position}, destination {destination}");
    }

    private static Vector3 V(float[] a) => new(a[0],a[1],a[2]);
    private async Task Place(Vector3 p)
    {
        _home.PlayerActor.Position=p;
        _home.PlayerActor.Velocity=Vector3.Zero;
        await Frames(3);
    }
    private void Require(bool condition,string name)
    {
        if(!condition)throw new InvalidOperationException(name);
        _checks.Add(name);
    }
    private async Task Frames(int count)
    {
        for(var i=0;i<count;i++)await ToSignal(GetTree(),SceneTree.SignalName.PhysicsFrame);
    }
    private async Task ProcessFrames(int count)
    {
        for(var i=0;i<count;i++)await ToSignal(GetTree(),SceneTree.SignalName.ProcessFrame);
    }
    private async Task KeyInput(Key key)
    {
        Input.ParseInputEvent(new InputEventKey {Keycode=key,PhysicalKeycode=key,Pressed=true});await ProcessFrames(3);
        Input.ParseInputEvent(new InputEventKey {Keycode=key,PhysicalKeycode=key,Pressed=false});await ProcessFrames(3);
    }
    private async Task JoyInput(JoyButton button)
    {
        Input.ParseInputEvent(new InputEventJoypadButton {ButtonIndex=button,Pressed=true});await ProcessFrames(3);
        Input.ParseInputEvent(new InputEventJoypadButton {ButtonIndex=button,Pressed=false});await ProcessFrames(3);
    }
}
