using Godot;
using System.Text.Json;
using TownRpg.Application;
using TownRpg.Core.Domain;
using TownRpg.Core.Services;
using TownRpg.Presentation;

namespace TownRpg.Testing;

/// <summary>Actual rendered lookdev and uninterrupted input. No formal save writes.</summary>
public partial class VillageAnimeProbe : Node
{
    private VillagePreviewController _host=null!;
    private string _output="";
    private readonly List<string> _checks=[];
    private readonly List<object> _views=[];
    private readonly bool _baseline=OS.GetCmdlineUserArgs().Contains("--anime-baseline");
    private readonly bool _quick=OS.GetCmdlineUserArgs().Contains("--anime-quick");
    private Camera3D? _review;
    private int _videoFrame;
    public override void _Ready()=>CallDeferred(MethodName.Run);
    private void Require(bool ok,string message) { if(!ok)throw new InvalidOperationException(message);_checks.Add(message); }
    private async Task Frames(int n) {for(var i=0;i<n;i++)await ToSignal(GetTree(),SceneTree.SignalName.ProcessFrame);}
    private async Task Settle()
    {
        await Frames(8);
        for(var i=0;i<1200 && _host.Exterior.Streaming?.PendingLoads>0;i++)await Frames(1);
        Require(_host.Exterior.Streaming is null || _host.Exterior.Streaming.LoadFailures==0,"no failed detail loads");
        Require(_host.Exterior.Streaming is null || _host.Exterior.Streaming.PendingLoads==0,"detail ready");await Frames(8);
    }
    private async Task Place(float x,float z,float yaw=0)
    {
        _host.ActivePlayer.GlobalPosition=_host.Exterior.SurfacePoint(x,z,.04f);_host.ActivePlayer.Velocity=Vector3.Zero;
        _host.ActiveCamera.SetYawImmediate(yaw);_host.ActiveCamera.SnapToTarget();await Settle();
    }
    private async Task Capture(string path)
    {
        await ToSignal(RenderingServer.Singleton,RenderingServer.SignalName.FramePostDraw);
        using var im=GetViewport().GetTexture().GetImage();im.SavePng(path);
    }
    private async Task View(string name,Vector3 at,Vector3 target,Vector2I size)
    {
        await Place(at.X,at.Z);
        _review!.GlobalPosition=at;_review.LookAt(target);_review.MakeCurrent();await Settle();
        await Capture(Path.Combine(_output,$"{size.X}-{name}.png"));
        _views.Add(new {name,width=size.X,height=size.Y,at=new[]{at.X,at.Y,at.Z},target=new[]{target.X,target.Y,target.Z},stream=_host.Exterior.Streaming?.Snapshot()});
    }
    private void Release() {foreach(var a in new[]{"move_up","move_down","move_left","move_right","move_slow"})Input.ActionRelease(a);}
    private async Task Walk(float x,float z,bool video=false)
    {
        _host.SetHostLocked(false);
        for(var f=0;f<1400;f++)
        {
            var p=_host.ActivePlayer.GlobalPosition;var d=new Vector3(x-p.X,0,z-p.Z);
            if(d.Length()<.20f){Release();await Frames(4);Require(true,$"walk reached {x},{z}");return;}
            var strength=Mathf.Clamp(d.Length()*1.5f,.25f,1);d=d.Normalized();
            var forward=_host.ActiveCamera.HorizontalForward;var right=forward.Cross(Vector3.Up);
            var fx=d.Dot(forward)*strength;var rx=d.Dot(right)*strength;
            Input.ActionPress("move_up",Math.Max(0,fx));Input.ActionPress("move_down",Math.Max(0,-fx));
            Input.ActionPress("move_right",Math.Max(0,rx));Input.ActionPress("move_left",Math.Max(0,-rx));
            await Frames(1);
            if(video && f%4==0)await Capture(Path.Combine(_output,"walk-frames",$"{_videoFrame++:D5}.png"));
        }
        throw new InvalidOperationException($"Walk blocked {_host.ActivePlayer.GlobalPosition} => {x},{z}");
    }
    private async Task Interact(bool interior)
    {
        Input.ParseInputEvent(new InputEventKey {PhysicalKeycode=Key.F,Pressed=true});await Frames(2);
        Input.ParseInputEvent(new InputEventKey {PhysicalKeycode=Key.F,Pressed=false});
        for(var i=0;i<600;i++){await Frames(1);if(!_host.IsTransitioning && _host.IsInterior==interior)return;}
        throw new InvalidOperationException("F transition failed");
    }
    private async void Run()
    {
        try
        {
            _output=ProjectSettings.GlobalizePath("res://outputs/village-v8/"+DateTime.Now.ToString("yyyyMMdd-HHmmss")+(_baseline?"-art-baseline":"-art"));Directory.CreateDirectory(_output);
            var session=GetNode<GameSession>("/root/GameSession");var before=SaveGameSerializer.Serialize(session.State);
            var savePath=ProjectSettings.GlobalizePath(GameSession.SavePath);var save=File.Exists(savePath)?File.ReadAllBytes(savePath):null;
            _host=ResourceLoader.Load<PackedScene>(_baseline?"res://scenes/testing/VillagePreview.tscn":"res://scenes/testing/VillageAnimePreview.tscn").Instantiate<VillagePreviewController>();AddChild(_host);
            _host.SetHostLocked(true);_host.ActivePlayer.Visible=false;
            foreach(var c in UiUxCapture.Find<CanvasLayer>(_host))c.Visible=false;
            await Settle();Require(_host.Exterior.VisualProfile.AnimeLighting!=_baseline,"sample selection");
            Require(_host.Exterior.Layout.Geometry.Buildings.Length==24,"24 building collisions retained");Require(_host.Exterior.Surface.Ready,"native terrain ready");
            _review=new Camera3D {Fov=58,Near=.1f,Far=420};_host.Exterior.AddChild(_review);
            foreach(var size in _quick?new[]{new Vector2I(1280,720)}:new[]{new Vector2I(1280,720),new Vector2I(1600,900),new Vector2I(1920,1080)})
            {
                DisplayServer.WindowSetMode(DisplayServer.WindowMode.Windowed);DisplayServer.WindowSetFlag(DisplayServer.WindowFlags.Borderless,true);GetWindow().Size=size;
                if(size.X==1920)DisplayServer.WindowSetMode(DisplayServer.WindowMode.Fullscreen);await Frames(12);
                using(var im=GetViewport().GetTexture().GetImage())Require(im.GetSize()==size,"actual framebuffer "+size);
                foreach(var (name,x,z,yaw) in new[]{("home-player",-56f,54f,0f),("plaza-player",-24f,30f,0f),("workshop-player",-23f,7f,0f)})
                {await Place(x,z,yaw);_host.ActiveCamera.Camera.MakeCurrent();await Settle();await Capture(Path.Combine(_output,$"{size.X}-{name}.png"));}
                await View("home-front",new(-49,4.2f,57),new(-56,2,43),size);
                await View("home-porch",new(-52,2.4f,49),new(-58,1.8f,44.8f),size);
                await View("shop-front",new(-11,6.3f,31),new(-20,4.4f,20),size);
                await View("shop-awning",new(-24,4.2f,24),new(-20,5.15f,21),size);
                await View("workshop-front",new(-12,6.8f,11),new(-23,5.0f,.7f),size);
                await View("plaza-tree",new(-20,4.4f,38),new(-28,6,29),size);
                await View("plaza-wide",new(-8,9,40),new(-25,4,23),size);
                foreach(var (name,x,z,h) in new[]{("tree-broad",-62.2426f,23.7574f,.9129f),("tree-upright",-40.3f,28.5f,1.1191f),("tree-open",5.9f,44.5f,-.1826f)})
                    await View(name,new(x+6,h+3,z+8),new(x,h+4,z),size);
                await View("tree-under",new(-29,3.1f,30),new(-28,8,29),size);
            }
            _review.QueueFree();_review=null;_host.ActiveCamera.Camera.MakeCurrent();await Frames(4);
            if(!_baseline && !_quick)
            {
                DisplayServer.WindowSetMode(DisplayServer.WindowMode.Windowed);GetWindow().Size=new(1280,720);await Frames(10);
                Directory.CreateDirectory(Path.Combine(_output,"walk-frames"));
                await Place(-56,49);_host.ActiveCamera.SetYawImmediate(0);_host.ActiveCamera.SnapToTarget();
                foreach(var p in _host.Exterior.Layout.Geometry.Routes.Single(r=>r.Id=="home_shop").Points.Skip(1))await Walk(p[0],p[1],true);
                foreach(var (x,z) in new[]{(-12f,25f),(-8f,9f),(-23f,7f)})await Walk(x,z,true);
                foreach(var (x,z) in new[]{(-8f,9f),(-12f,25f),(-20f,24f)})await Walk(x,z);
                // Walk back along the same road, through the gate and into the actual F entrance.
                foreach(var p in _host.Exterior.Layout.Geometry.Routes.Single(r=>r.Id=="home_shop").Points.Reverse().Skip(1))await Walk(p[0],p[1]);
                await Walk(-56,45.85f);await Interact(true);Require(_host.Interior!.ExteriorModelPath==VillageVisualProfile.AnimeV8.HomeModel,"matching home exterior source");
                _host.ActivePlayer.Visible=false;
                var state=_host.Capture();await Walk(0,3.05f);await Interact(false);await Settle();_host.ActivePlayer.Visible=false;
                await Walk(-56,55);await Walk(-56,61);await Walk(-56,55);Require(_host.ActivePlayer.IsOnFloor(),"repaired slope roundtrip grounded");
                await _host.RestoreAsync(VillageExplorationState.HomeLocation,state);await Frames(20);
                Require(_host.IsInterior,"home restore");
                var restored=_host.Capture();Require(restored.Doors.OrderBy(x=>x.Key).SequenceEqual(state.Doors.OrderBy(x=>x.Key)),"home doors retained");
                Require(restored.DoorOpenSides.OrderBy(x=>x.Key).SequenceEqual(state.DoorOpenSides.OrderBy(x=>x.Key)),"door sides retained");
                Require(restored.Lights.OrderBy(x=>x.Key).SequenceEqual(state.Lights.OrderBy(x=>x.Key)),"home lights retained");
                // Review the same exterior from the residential scene with its original lighting.
                var homePosition=_host.ActivePlayer.GlobalPosition;
                _host.ActivePlayer.GlobalPosition=new(0,.04f,8);_host.ActivePlayer.Visible=false;
                var homeReview=new Camera3D {Fov=58,Near=.1f,Far=100};_host.Interior!.AddChild(homeReview);
                homeReview.GlobalPosition=new(7,4.2f,17);homeReview.LookAt(new Vector3(0,2,3));homeReview.MakeCurrent();
                await Frames(20);await Capture(Path.Combine(_output,"1280-residential-exterior.png"));
                homeReview.QueueFree();_host.ActiveCamera.Camera.MakeCurrent();_host.ActivePlayer.GlobalPosition=homePosition;
            }
            Require(SaveGameSerializer.Serialize(session.State)==before,"formal state unchanged");
            Require(save is null?!File.Exists(savePath):save.SequenceEqual(File.ReadAllBytes(savePath)),"formal save bytes unchanged");
            File.WriteAllText(Path.Combine(_output,"results.json"),JsonSerializer.Serialize(new{baseline=_baseline,checks=_checks,views=_views,videoFrames=_videoFrame,humanAcceptance="pending",targetHardwareAcceptance="pending"},new JsonSerializerOptions {WriteIndented=true}));
            GD.Print("V8_ART_PASS output="+_output+" checks="+_checks.Count);_host.QueueFree();await Frames(3);GetTree().Quit();
        }
        catch(Exception e){Release();GD.PushError(e.ToString());if(_output!="")File.WriteAllText(Path.Combine(_output,"failure.txt"),e.ToString());GetTree().Quit(1);}
    }
}
