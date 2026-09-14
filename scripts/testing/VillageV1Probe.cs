using System.Text.Json;
using Godot;
using TownRpg.Application;
using TownRpg.Core.Services;
using TownRpg.Presentation;

namespace TownRpg.Testing;

public partial class VillageV1Probe : Node
{
    private VillagePreviewController _village=null!;
    private readonly List<string> _checks=[];
    private readonly List<object> _routes=[];
    private readonly List<string> _captures=[];
    private readonly bool _capture=OS.GetCmdlineUserArgs().Contains("--village-capture");
    private readonly bool _gray=OS.GetCmdlineUserArgs().Contains("--village-graybox");
    private readonly bool _lookdev=OS.GetCmdlineUserArgs().Contains("--village-lookdev");
    private bool BatchOnly => OS.GetCmdlineUserArgs().Contains("--village-batch-only");
    private bool UseV1 => OS.GetCmdlineUserArgs().Contains("--village-v1");
    private bool TerrainAdoption=>OS.GetCmdlineUserArgs().Contains("--terrain-adoption");
    private string Output=>TerrainAdoption ? ProjectSettings.GlobalizePath($"res://outputs/village-terrain-adoption/{_travelRun}-{(UseV1?"v1":TerrainBaseline?"original":"default")}") : TerrainCheck ? TerrainOutput : TravelCheck || TravelVideo ? TravelOutput : ProjectSettings.GlobalizePath($"res://outputs/{(UseV1?"village-v1-regression":"village-v2")}/{(_lookdev?"lookdev":_gray?"graybox":"validation")}");
    public override void _Ready()=>CallDeferred(MethodName.Run);
    private async void Run()
    {
        try
        {
            Directory.CreateDirectory(Output);
            var session=GetNode<GameSession>("/root/GameSession");var state=SaveGameSerializer.Serialize(session.State);
            var path=ProjectSettings.GlobalizePath(GameSession.SavePath);var save=File.Exists(path)?File.ReadAllBytes(path):null;
            var start=Time.GetTicksUsec();
            _village=ResourceLoader.Load<PackedScene>(TerrainCheck && !TerrainBaseline && !TerrainAdoption ? "res://scenes/testing/VillageTerrainPreview.tscn" : "res://scenes/testing/VillagePreview.tscn").Instantiate<VillagePreviewController>();
            AddChild(_village);await Frames(6);var loadMs=(Time.GetTicksUsec()-start)/1000d;
            Require(Math.Abs(_village.ActivePlayer.Speed-(UseV1?3.1f:4.2f))<.001f,"area default speed");
            Require(_village.Exterior.UseTerrainSample==(!UseV1 && !_gray && !TerrainBaseline && !OS.GetCmdlineUserArgs().Contains("--village-original-terrain")),"preview surface selection");
            if(TerrainCheck)
            {
                if(!TerrainBaseline) await TerrainContract();
                if(OS.GetCmdlineUserArgs().Contains("--terrain-views"))
                {
                    await CaptureTerrainMatrix();await ClosePreview();GD.Print("TERRAIN_VIEWS_PASS");GetTree().Quit();return;
                }
            }
            if(TravelVideo)
            {
                await CaptureTravelMotion();await ClosePreview();
                File.WriteAllText(Path.Combine(Output,"video-checks.json"),JsonSerializer.Serialize(_checks));
                GD.Print("VILLAGE_TRAVEL_VIDEO_PASS");GetTree().Quit();return;
            }
            if(OS.GetCmdlineUserArgs().Contains("--village-refresh-capture"))
            {
                await CaptureMatrix();
                File.WriteAllText(Path.Combine(Output,"capture-refresh.json"),JsonSerializer.Serialize(new { loadMs,captureCount=_captures.Count,checks=_checks,files=_captures },new JsonSerializerOptions { WriteIndented=true }));
                GD.Print($"VILLAGE_CAPTURE_REFRESH_PASS count={_captures.Count}");await ClosePreview();GetTree().Quit();return;
            }
            if(_lookdev)
            {
                await Lookdev();GD.Print("VILLAGE_LOOKDEV_CAPTURE_PASS");await ClosePreview();GetTree().Quit();return;
            }
            Require(!_village.IsInterior && !_village.IsPaused,"starts outside");
            Require(_village.Exterior.InteractionMarkerCount==1,"only home entrance is interactive");
            Require(_village.ActivePlayer.TerrainMovement && _village.ActivePlayer.IsOnFloor(),$"terrain grounded at spawn {_village.ActivePlayer.Position}");
            await Inputs();
            if(TravelCheck && !UseV1 && !TravelVerificationOnly) await CompareTravel();
            Engine.TimeScale=3;
            if(!BatchOnly && (!(TravelCheck && !UseV1) || TravelVerificationOnly)) foreach(var route in VillageLayout3D.Layout.Routes) await WalkRoute(route);
            if(!UseV1)
            {
                foreach(var route in VillageExteriorLayout3D.Layout.Routes) await WalkRoute(route);
                await ExteriorClearances();
            }
            if(!BatchOnly) await BridgeApproaches();
            Engine.TimeScale=1;
            if(TravelCheck && !UseV1) await TravelStops();
            await BoundariesAndCamera();
            await HomeTransitions();
            Require(SaveGameSerializer.Serialize(session.State)==state,"formal session unchanged");
            Require(save is null ? !File.Exists(path) : File.ReadAllBytes(path).SequenceEqual(save),"formal save unchanged");
            if(_capture) { if(TerrainCheck) await CaptureTerrainMatrix(); else await CaptureMatrix(); }
            await _village.ResetPreview();await Frames(5);
            Require(!_village.IsInterior && _village.Interior is null && !_village.IsPaused,"reset clears cached interior");
            _village.ActivePlayer.ConfigureDefaultCharacterVisual(DreamCharacterFrames.Create(ResourceLoader.Load<Texture2D>(
                "res://assets/generated/dream_escape/pixel/protagonist-walk-4x4-v1.png")),"res://tests/village-missing-character.glb");
            Require(!_village.ActivePlayer.UsesDefault3DModel && _village.ActivePlayer.CharacterSprite is not null,"safe billboard fallback");
            if(TravelCheck) await Inputs();
            var result=JsonSerializer.Serialize(new {
                status="pass",graybox=_gray,loadMs,checks=_checks,routes=_routes,transitionTimesMs=_village.TransitionTimesMs,
                renderer=RenderingServer.GetVideoAdapterName(),captured=_capture,captureCount=_captures.Count,captureFiles=_captures,humanAcceptance="pending"
            },new JsonSerializerOptions { WriteIndented=true });
            await ClosePreview();
            File.WriteAllText(Path.Combine(Output,"results.json"),result);
            GD.Print($"VILLAGE_PREVIEW_PROBE_PASS checks={_checks.Count} graybox={_gray} loadMs={loadMs:F1}");
            GetTree().Quit();
        }
        catch(Exception e) { Engine.TimeScale=1;ReleaseMovement();GD.PushError("VILLAGE_V1_PROBE_FAIL "+e);GetTree().Quit(1); }
    }
    private async Task ClosePreview()
    {
        _village.QueueFree();await Frames(3);_village=null!;
        GC.Collect();GC.WaitForPendingFinalizers();await Frames(3);
        GC.Collect();GC.WaitForPendingFinalizers();await Frames(3);
    }
    private async Task Inputs()
    {
        await Place(-56,48.5f);
        _village.ActiveCamera.SetYawImmediate(0);_village.ActivePlayer.SetFacingImmediate(Vector3.Back);
        var start=_village.ActivePlayer.Position;
        Input.ActionPress("move_down");await Frames(30);ReleaseMovement();await Frames(6);
        var jog=_village.ActivePlayer.Position.DistanceTo(start);Require(jog>.7f,"jog input");
        await Place(-56,48.5f);_village.ActivePlayer.SetFacingImmediate(Vector3.Back);start=_village.ActivePlayer.Position;
        Input.ActionPress("move_slow");Input.ActionPress("move_down");await Frames(30);ReleaseMovement();await Frames(6);
        Require(_village.ActivePlayer.Position.DistanceTo(start)<jog,"Shift slow walk");
        await KeyInput(Key.Escape);Require(_village.IsPaused,"Esc pauses");start=_village.ActivePlayer.Position;
        Input.ActionPress("move_down");await Frames(8);ReleaseMovement();
        Require(_village.ActivePlayer.Position.IsEqualApprox(start)&&Input.MouseMode==Input.MouseModeEnum.Visible,"pause blocks movement and releases cursor");
        await PadInput(JoyButton.B);Require(!_village.IsPaused,"B resumes");
        _village.ActivePlayer._Notification((int)NotificationWMWindowFocusOut);start=_village.ActivePlayer.Position;
        Input.ActionPress("move_down");await Frames(8);ReleaseMovement();
        Require(_village.ActivePlayer.Position.IsEqualApprox(start),"focus loss clears motion");
        _village.ActivePlayer._Notification((int)NotificationWMWindowFocusIn);
        var yaw=_village.ActiveCamera.YawDegrees;
        Require(_village.ActiveCamera.TryApplyMouseMotion(new(50,0),true)&&Math.Abs(_village.ActiveCamera.YawDegrees-yaw)>1,"mouse orbit input");
    }
    private async Task<double> WalkRoute(VillageLayout3D.Route route)
    {
        var first=route.Points[0];await Place(first[0],first[1]);
        _village.ActiveCamera.SetYawImmediate(0);_village.ActiveCamera.SnapToTarget();
        _village.ActivePlayer.SetFacingImmediate(Vector3.Forward);
        var started=Time.GetTicksUsec();
        var seconds=0d;var minY=float.MaxValue;var maxY=float.MinValue;
        for(var i=1;i<route.Points.Length;i++)
        {
            var destination=new Vector3(route.Points[i][0],0,route.Points[i][1]);var done=false;
            for(var frame=0;frame<2400;frame++)
            {
                var p=_village.ActivePlayer.GlobalPosition;var direction=new Vector3(destination.X-p.X,0,destination.Z-p.Z);
                minY=Math.Min(minY,p.Y);maxY=Math.Max(maxY,p.Y);
                if(direction.Length()<.22f) { done=true;break; }
                var strength=Mathf.Clamp(direction.Length()*1.5f,.25f,1);direction=direction.Normalized();
                Input.ActionPress("move_left",Math.Max(0,-direction.X)*strength);Input.ActionPress("move_right",Math.Max(0,direction.X)*strength);
                Input.ActionPress("move_up",Math.Max(0,-direction.Z)*strength);Input.ActionPress("move_down",Math.Max(0,direction.Z)*strength);
                await Frames(1);seconds+=GetPhysicsProcessDeltaTime();
                if(frame%30==0) Require(Math.Abs(p.Y-_village.Exterior.WalkHeight(p.X,p.Z))<.4f,$"{route.Id} on surface segment {i} frame {frame}");
            }
            if(!done) throw new InvalidOperationException($"Route {route.Id} blocked {_village.ActivePlayer.Position} -> {destination}");
        }
        ReleaseMovement();await Frames(5);
        Require(true,"continuous input route "+route.Id);
        _routes.Add(new { id=route.Id,speed=_village.ActivePlayer.Speed,simulatedSeconds=seconds,
            wallSeconds=(Time.GetTicksUsec()-started)/1000000d,timeScale=Engine.TimeScale,
            idealJogSeconds=VillageLayout3D.RouteLength(route)/_village.ActivePlayer.Speed,minY,maxY });
        GD.Print($"VILLAGE_ROUTE_PASS {route.Id} seconds={seconds:F2} height={minY:F2}..{maxY:F2}");
        return seconds;
    }
    private async Task ExteriorClearances()
    {
        foreach(var zone in VillageExteriorLayout3D.Layout.Zones)
        {
            var r=zone.Rect;
            await WalkRoute(new VillageLayout3D.Route("clear_"+zone.Id,new[]{
                new[]{r[0]+.35f,r[1]+.35f},new[]{r[0]+r[2]-.35f,r[1]+.35f},
                new[]{r[0]+r[2]-.35f,r[1]+r[3]-.35f},new[]{r[0]+.35f,r[1]+r[3]-.35f}}));
        }
        foreach(var p in VillageExteriorLayout3D.Layout.Props.Where(p=>p.Solid && p.Kind!="overhead"))
        {
            var from=new Vector3(p.Position[0],p.Position[1]+p.Size[1]*.5f,p.Position[2]+p.Size[2]/2+.2f);
            using var query=PhysicsRayQueryParameters3D.Create(from,from+Vector3.Forward*(p.Size[2]+.4f),1);
            query.Exclude=new Godot.Collections.Array<Rid>{_village.ActivePlayer.GetRid()};
            Require(_village.Exterior.GetWorld3D().DirectSpaceState.IntersectRay(query).Count>0,"fixture collision "+p.Id);
        }
        foreach(var (x,z) in new[]{(-7.5f,3.35f),(-19.3f,-13.3f),(-19f,8.6f)})
        {
            await Place(x,z);
            foreach(var yaw in new[]{0f,90f,180f,270f})
            {
                _village.ActiveCamera.SetYawImmediate(yaw);_village.ActiveCamera.SnapToTarget();await Frames(6);
                Require(float.IsFinite(_village.ActiveCamera.SpringArm.GetHitLength()),"finite exterior camera");
                Require(_village.ActivePlayer.IsOnFloor(),"standing under canopy or at desk");
            }
        }
    }
    private async Task BoundariesAndCamera()
    {
        foreach(var bridge in VillageLayout3D.Layout.Bridges)
        {
            await Place(bridge.CenterX,bridge.Z);
            Require(_village.ActivePlayer.TestMove(_village.ActivePlayer.GlobalTransform,new(0,0,3)),"bridge rail "+bridge.Id);
            Require(_village.ActivePlayer.IsOnFloor(),"bridge floor "+bridge.Id);
        }
        await Place(VillageLayout3D.StreamX(0)-10,0);
        Require(_village.ActivePlayer.TestMove(_village.ActivePlayer.GlobalTransform,new(4,0,0)),"water boundary");
        await Place(-102,85);
        Require(_village.ActivePlayer.TestMove(_village.ActivePlayer.GlobalTransform,new(-5,0,0)),"village outer boundary");
        await Place(-56,45.9f);
        _village.ActiveCamera.SetYawImmediate(180);_village.ActiveCamera.SnapToTarget();await Frames(10);
        Require(_village.ActiveCamera.SpringArm.GetHitLength()<2,"house facade compresses camera");
        await Place(-56,49);
        _village.ActiveCamera.SetYawImmediate(90);_village.ActiveCamera.SnapToTarget();await Frames(10);
        Require(_village.ActiveCamera.SpringArm.GetHitLength()>2,"camera recovers in courtyard");
        await Place(-56,45.6f);
        _village.ActivePlayer.SetPhysicsProcess(false);_village.ActivePlayer.Position+=Vector3.Up*3;
        Require(!_village.Exterior.CanReachHome(),"vertical separation excludes entrance");
        _village.ActivePlayer.SetPhysicsProcess(true);
    }
    private async Task BridgeApproaches()
    {
        foreach(var bridge in VillageLayout3D.Layout.Bridges)
        foreach(var offset in new[]{-.65f,0f,.65f})
        foreach(var side in new[]{-1,1})
        {
            var from=bridge.CenterX+side*(bridge.HalfSpan+3);var to=bridge.CenterX-side*(bridge.HalfSpan+3);
            await WalkRoute(new VillageLayout3D.Route($"bridge_{bridge.Id}_{side}_{offset}",new[]{new[]{from,bridge.Z+offset},new[]{to,bridge.Z+offset}}));
        }
    }
    private async Task HomeTransitions()
    {
        await Place(-56,45.7f);await Frames(3);
        Require(_village.Exterior.CanReachHome(),"home entrance reachable");
        await KeyInput(Key.F);await AwaitTransition();
        Require(_village.IsInterior,"F enters home through input event");
        Require(_village.GetNode("AreaHost").GetChildCount()==1 && !_village.Exterior.IsInsideTree(),"one live area indoors");
        Require(!_village.ActivePlayer.TerrainMovement,"interior retains flat movement");
        Require(Math.Abs(_village.ActivePlayer.Speed-3.1f)<.001f,"home retains original speed");
        _village.ActivePlayer.SetPhysicsProcess(false);_village.ActivePlayer.Position=new(1.1f,0,3.6f);await Frames(4);
        await KeyInput(Key.F);Require(_village.Interior!.LightOn("living"),"interior light event");
        _village.ActivePlayer.Position=new(0,0,3.1f);await Frames(4);
        await PadInput(JoyButton.A);await AwaitTransition();
        Require(!_village.IsInterior,"A exits home through input event");
        Require(Math.Abs(_village.ActivePlayer.Speed-(UseV1?3.1f:4.2f))<.001f,"outdoor speed restored after home");
        Require(_village.GetNode("AreaHost").GetChildCount()==1 && !_village.Interior.IsInsideTree(),"one live area outdoors");
        for(var i=0;i<2;i++)
        {
            GC.Collect();GC.WaitForPendingFinalizers();await Frames(3);
            await Place(-56,45.7f);
            var transition=_village.SwitchArea(true);
            Require(!await _village.SwitchArea(true),"duplicate transition rejected "+i);
            await transition;
            Require(_village.Interior.LightOn("living"),"light retained in preview "+i);
            _village.ActivePlayer.SetPhysicsProcess(false);_village.ActivePlayer.Position=new(1.1f,0,3.6f);await Frames(4);
            await KeyInput(Key.F);Require(!_village.Interior.LightOn("living"),"subscriptions operate once after return "+i);
            await PadInput(JoyButton.A);Require(_village.Interior.LightOn("living"),"A operates once after return "+i);
            _village.ActivePlayer.Position=new(0,0,3.1f);await Frames(4);
            await KeyInput(Key.F);await AwaitTransition();Require(!_village.IsInterior,"door exit still subscribed "+i);
        }
    }
    private async Task AwaitTransition()
    {
        for(var i=0;i<600 && _village.IsTransitioning;i++) await ProcessFrames(1);
        Require(!_village.IsTransitioning,"transition completes");
    }
    private async Task CaptureMatrix()
    {
        foreach(var resolution in new[]{new Vector2I(1280,720),new Vector2I(1600,900),new Vector2I(1920,1080)})
        {
            DisplayServer.WindowSetMode(DisplayServer.WindowMode.Windowed);DisplayServer.WindowSetFlag(DisplayServer.WindowFlags.Borderless,true);
            GetWindow().Size=resolution;await Frames(20);
            var folder=Path.Combine(Output,$"{resolution.X}x{resolution.Y}");Directory.CreateDirectory(folder);
            foreach(var view in new[]{
                ("01-home-facade",-56f,50f,0f),("02-home-gate",-56f,56.5f,0f),("03-residential-lane",-70f,55f,-90f),
                ("04-approach-square",-20f,37f,0f),("05-shop",-4f,6.6f,0f),("06-plaza-tree",-8f,14f,35f),
                ("07-community",-18f,15f,90f),("08-clinic",18f,23f,0f),("09-workshop",-23f,-12f,0f),
                ("25-desk",-19f,9.4f,0f),("26-board",-15.5f,9.8f,0f),("27-shop-side",-7f,6.5f,-30f),
                ("28-delivery-close",-7.5f,3.4f,0f),("29-workshop-side",-29.3f,-10f,-45f),("30-workbench-close",-19.3f,-13.2f,0f),
                ("10-south-bridge",38f,28f,-90f),("11-stream-south",VillageLayout3D.StreamX(28),28f,0f),
                ("12-stream-north",VillageLayout3D.StreamX(-31),-31f,180f),("13-east-lane",72f,8f,0f),
                ("14-station",-28f,-79f,0f),("15-railway",-35f,-98f,-90f),("16-valley-return",-36f,-64f,180f)})
            {
                await Place(view.Item2,view.Item3);_village.ActiveCamera.SetYawImmediate(view.Item4);_village.ActiveCamera.SnapToTarget();
                _village.ActivePlayer.SetFacingImmediate(_village.ActiveCamera.HorizontalForward);
                await SaveCapture(folder,view.Item1,resolution);
            }
            var review=new Camera3D { Name="ReviewCamera",Fov=58,Near=1f,Far=850 };
            _village.Exterior.AddChild(review);_village.ActivePlayer.Visible=false;review.MakeCurrent();
            foreach(var (name,position,target) in new[]{
                ("17-overview",new Vector3(-150,170,170),new Vector3(0,1,0)),
                ("18-east-overview",new Vector3(152,105,90),new Vector3(5,1,-5)),
                ("19-village-center",new Vector3(-33,32,40),new Vector3(-1,2,5)),
                ("20-station-overview",new Vector3(-58,27,-52),new Vector3(-22,5.8f,-90))})
            {
                review.GlobalPosition=position;review.LookAt(target);await SaveCapture(folder,name,resolution);
            }
            review.QueueFree();_village.ActivePlayer.Visible=true;_village.ActiveCamera.Camera.MakeCurrent();await Frames(3);
            await _village.SwitchArea(true);
            foreach(var (name,p,yaw) in new[]{
                ("21-home-living",new Vector3(.7f,0,2.1f),0f),("22-home-bedroom",new Vector3(-3.1f,0,2.8f),0f),
                ("23-home-family",new Vector3(-2.6f,0,-3.55f),0f),("24-home-kitchen",new Vector3(3.9f,0,1.8f),-15f)})
            {
                _village.ActivePlayer.SetPhysicsProcess(true);_village.ActivePlayer._Notification((int)NotificationWMWindowFocusIn);
                _village.ActivePlayer.Position=p;_village.ActivePlayer.Velocity=Vector3.Zero;
                _village.ActiveCamera.SetYawImmediate(yaw);_village.ActiveCamera.SnapToTarget();_village.ActivePlayer.SetFacingImmediate(_village.ActiveCamera.HorizontalForward);
                await SaveCapture(folder,name,resolution);
            }
            await _village.SwitchArea(false);
            await Place(0,8);await Frames(30);
            var orbitTimes=new List<double>();
            for(var frame=0;frame<180;frame++)
            {
                _village.ActiveCamera.SetYawImmediate(frame*2);var before=Time.GetTicksUsec();
                await ToSignal(RenderingServer.Singleton,RenderingServer.SignalName.FramePostDraw);orbitTimes.Add((Time.GetTicksUsec()-before)/1000d);
            }
            await Place(-56,55);_village.ActiveCamera.SetYawImmediate(-90);_village.ActiveCamera.SnapToTarget();_village.ActivePlayer.SetFacingImmediate(Vector3.Right);await Frames(12);
            var walkTimes=new List<double>();Input.ActionPress("move_up");
            for(var frame=0;frame<360;frame++)
            {
                var before=Time.GetTicksUsec();await ToSignal(RenderingServer.Singleton,RenderingServer.SignalName.FramePostDraw);walkTimes.Add((Time.GetTicksUsec()-before)/1000d);
            }
            ReleaseMovement();await Frames(5);
            object Summary(List<double> values)=>new {frames=values.Count,meanMs=values.Average(),p95Ms=values.Order().ToArray()[(int)(values.Count*.95)],maxMs=values.Max()};
            File.WriteAllText(Path.Combine(folder,"frame-times.json"),JsonSerializer.Serialize(new {
                orbit=Summary(orbitTimes),walking=Summary(walkTimes),renderer=RenderingServer.GetVideoAdapterName(),
                note="Local rendered wall-clock frames including frame pacing. Target-hardware and physical-input certification are pending."
            },new JsonSerializerOptions { WriteIndented=true }));
        }
    }
    private async Task SaveCapture(string folder,string name,Vector2I resolution)
    {
        await Frames(12);await ToSignal(RenderingServer.Singleton,RenderingServer.SignalName.FramePostDraw);
        var image=GetViewport().GetTexture().GetImage();Require(image.GetWidth()==resolution.X&&image.GetHeight()==resolution.Y,"capture resolution "+name);
        var path=Path.Combine(folder,name+".png");image.SavePng(path);_captures.Add(path);
    }
    private async Task Lookdev()
    {
        var resolution=new Vector2I(1280,720);GetWindow().Size=resolution;await Frames(15);
        foreach(var (name,x,z,yaw) in new[]{("shop",-4f,6.6f,0f),("plaza",-8f,14f,35f),("workshop",-23f,-12f,0f),("workbench",-19.3f,-13.2f,0f),("desk",-19f,9.4f,0f),("delivery",-7.5f,3.4f,0f)})
        {
            await Place(x,z);_village.ActiveCamera.SetYawImmediate(yaw);_village.ActiveCamera.SnapToTarget();
            _village.ActivePlayer.SetFacingImmediate(_village.ActiveCamera.HorizontalForward);await SaveCapture(Output,name,resolution);
        }
        var camera=new Camera3D { Fov=58,Near=1,Far=850 };_village.Exterior.AddChild(camera);_village.ActivePlayer.Visible=false;
        camera.GlobalPosition=new(-90,120,120);camera.LookAt(new(0,1,0));camera.MakeCurrent();await SaveCapture(Output,"overview",resolution);
        camera.QueueFree();_village.ActivePlayer.Visible=true;_village.ActiveCamera.Camera.MakeCurrent();
    }
    private async Task Place(float x,float z)
    {
        ReleaseMovement();var player=_village.ActivePlayer;player.SetPhysicsProcess(true);player._Notification((int)NotificationWMWindowFocusIn);
        player.GlobalPosition=_village.Exterior.SurfacePoint(x,z,.06f);player.Velocity=Vector3.Zero;await Frames(8);
    }
    private static void ReleaseMovement() { foreach(var a in new[]{"move_up","move_down","move_left","move_right","move_slow"}) Input.ActionRelease(a); }
    private void Require(bool condition,string name) { if(!condition) throw new InvalidOperationException(name);_checks.Add(name); }
    private async Task Frames(int count) { for(var i=0;i<count;i++) await ToSignal(GetTree(),SceneTree.SignalName.PhysicsFrame); }
    private async Task ProcessFrames(int count) { for(var i=0;i<count;i++) await ToSignal(GetTree(),SceneTree.SignalName.ProcessFrame); }
    private async Task KeyInput(Key key)
    {
        Input.ParseInputEvent(new InputEventKey { Keycode=key,PhysicalKeycode=key,Pressed=true });await ProcessFrames(3);
        Input.ParseInputEvent(new InputEventKey { Keycode=key,PhysicalKeycode=key,Pressed=false });await ProcessFrames(3);
    }
    private async Task PadInput(JoyButton button)
    {
        Input.ParseInputEvent(new InputEventJoypadButton { ButtonIndex=button,Pressed=true });await ProcessFrames(3);
        Input.ParseInputEvent(new InputEventJoypadButton { ButtonIndex=button,Pressed=false });await ProcessFrames(3);
    }
}
