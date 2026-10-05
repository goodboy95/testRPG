using Godot;
using System.Security.Cryptography;
using System.Text.Json;
using TownRpg.Application;
using TownRpg.Presentation;

namespace TownRpg.Scripts.Testing;

public partial class EnvironmentRefinementProbe : Node
{
    private EnvironmentRefinementPreview _preview=null!;
    private Camera3D _camera=null!;
    private readonly List<string> _checks=[];
    private string _directory="";
    private void Require(bool condition,string detail) {if(!condition)throw new InvalidOperationException(detail);_checks.Add(detail);}
    private async Task Frames(int n=8){for(var i=0;i<n;i++)await ToSignal(GetTree(),SceneTree.SignalName.ProcessFrame);}
    private static Dictionary<string,string> Saves()
    {
        var path=ProjectSettings.GlobalizePath("user://");
        return Directory.Exists(path)?Directory.GetFiles(path,"*",SearchOption.AllDirectories)
            .Where(p=>p.EndsWith(".json",StringComparison.OrdinalIgnoreCase))
            .ToDictionary(p=>p,p=>Convert.ToHexString(SHA256.HashData(File.ReadAllBytes(p)))):new();
    }
    public override void _Ready()=>CallDeferred(MethodName.Run);
    private async Task View(string name,Vector3 at,Vector3 target,float fov=60)
    {
        _preview.Actor.Visible=false;
        foreach(var layer in RefinementArt.Nodes(_preview).OfType<CanvasLayer>())layer.Visible=false;
        _camera.Position=at;_camera.Fov=fov;_camera.LookAt(target);_camera.MakeCurrent();
        await Frames(10);await ToSignal(RenderingServer.Singleton,RenderingServer.SignalName.FramePostDraw);
        using var image=GetViewport().GetTexture().GetImage();
        Require(image.SavePng(Path.Combine(_directory,name+".png"))==Error.Ok,"capture "+name);
    }
    private async Task Walk(Vector3 destination)
    {
        var actor=_preview.Actor;actor.Notification((int)NotificationWMWindowFocusIn);
        // Keep synthetic input independent of desktop focus after checking the real controller path.
        for(var tick=0;tick<1800;tick++)
        {
            var d=destination-actor.GlobalPosition;d.Y=0;
            if(d.Length()<.16f){Release();Require(true,"walk reached "+destination);return;}
            var strength=Mathf.Clamp(d.Length()*1.7f,.22f,1);d=d.Normalized();
            var forward=_preview.Rig.HorizontalForward;var right=forward.Cross(Vector3.Up);
            Input.ActionPress("move_up",Math.Max(0,d.Dot(forward))*strength);Input.ActionPress("move_down",Math.Max(0,-d.Dot(forward))*strength);
            Input.ActionPress("move_right",Math.Max(0,d.Dot(right))*strength);Input.ActionPress("move_left",Math.Max(0,-d.Dot(right))*strength);
            await ToSignal(GetTree(),SceneTree.SignalName.PhysicsFrame);
        }
        Release();throw new InvalidOperationException("walk blocked "+actor.GlobalPosition+" to "+destination);
    }
    private static void Release(){foreach(var a in new[]{"move_up","move_down","move_left","move_right"})Input.ActionRelease(a);}
    private async void Run()
    {
        var stateBefore=JsonSerializer.Serialize(GetNode<GameSession>("/root/GameSession").State);
        var saves=Saves();
        try
        {
            var baseline=OS.GetCmdlineUserArgs().Contains("--refinement-baseline");
            _directory=ProjectSettings.GlobalizePath("res://outputs/environment-refinement-v1/"+DateTime.Now.ToString("yyyyMMdd-HHmmss")+(baseline?"-baseline":"-sample"));
            Directory.CreateDirectory(_directory);DisplayServer.WindowSetSize(new(1920,1080));
            _preview=new EnvironmentRefinementPreview();AddChild(_preview);
            await Frames(2);for(var i=0;i<1200 && _preview.Switching;i++)await Frames(1);
            Require(!_preview.Switching,"initial sample ready");
            _camera=new Camera3D();AddChild(_camera);
            if(_preview.AreaKind!="home")await _preview.Show("home");await Frames();
            var village=(RefinementVillage)_preview.Area!;var home=village.Interior!;
            Require(home.CaptureDoors().Count==5,"five existing home doors");
            Require(home.CaptureLights().Count==6,"six existing home lights");
            Require(home.ModelRoot.FindChild(baseline?"PlayerBed":"Refined_BlueDuvet",true,false) is not null,"bed source selected");
            await View("home-room",new(-2.25f,1.75f,3.6f),new(-4.2f,1.0f,1.95f));
            await View("home-bed",new(-3.5f,1.35f,2.0f),new(-4.85f,.59f,3.08f),52);
            await View("home-desk",new(-2.7f,1.7f,2.0f),new(-3.62f,.88f,.55f),52);
            _preview.Rig.Camera.MakeCurrent();_preview.Actor.Visible=true;
            _preview.Actor.GlobalPosition=new(-2.6f,0,2.75f);await Frames();
            await Walk(new(-2.6f,0,.9f));await Walk(new(-2.6f,0,2.75f));
            // Cached home -> exterior -> home must preserve door/light state.
            var doors=JsonSerializer.Serialize(home.CaptureDoors());var lights=JsonSerializer.Serialize(home.CaptureLights());
            await village.SwitchArea(false);await Frames();await village.SwitchArea(true);await Frames();
            Require(doors==JsonSerializer.Serialize(home.CaptureDoors()),"cached home doors retained");
            Require(lights==JsonSerializer.Serialize(home.CaptureLights()),"cached home lights retained");
            await _preview.Show("village");await Frames(20);
            village=(RefinementVillage)_preview.Area!;
            Require(village.Exterior.Layout.Geometry.Buildings.Length==24,"24 building positions retained");
            Require(!village.Exterior.UseStreaming,"review model does not claim stream-budget validation");
            await View("village-shop",new(-15,4.1f,30),new(-22,4.05f,20),60);
            await View("village-tree",new(-19,3.8f,35),new(-28,5.2f,29),58);
            await View("village-home",new(-48,3.4f,56),new(-56,2.5f,45),60);
            _preview.Rig.Camera.MakeCurrent();_preview.Actor.Visible=true;
            _preview.Actor.GlobalPosition=village.Exterior.SurfacePoint(-20,28,.04f);await Frames();
            await Walk(village.Exterior.SurfacePoint(-20,25,.04f));await Walk(village.Exterior.SurfacePoint(-20,28,.04f));
            var route=village.Exterior.Layout.Geometry.Routes.Single(r=>r.Id=="home_shop").Points;
            _preview.Actor.GlobalPosition=village.Exterior.SurfacePoint(route[0][0],route[0][1],.04f);await Frames();
            foreach(var p in route.Skip(1))await Walk(village.Exterior.SurfacePoint(p[0],p[1],.04f));
            foreach(var p in route.Reverse().Skip(1))await Walk(village.Exterior.SurfacePoint(p[0],p[1],.04f));
            await _preview.Show("classroom");await Frames();
            var room=(RefinementClassroom)_preview.Area!;
            Require(room.InteractionMarkerCount==11,"11 existing classroom target positions retained");
            Require(room.Visual.CollisionBodyCount==24,"24 existing classroom collision bodies");
            await View("classroom-room",new(4.65f,1.85f,3.25f),new(0,1.38f,-2.0f));
            for(var i=0;i<5;i++)
            {
                room.SetClockState(i);
                var names=new[]{"Missing","HandOnly","KeyOnly","Ready","Restored"};
                Require(names.Count(s=>((Node3D)room.GetNode("ClassroomModel").FindChild("Clock_"+s,true,false)!).Visible)==1,"one clock state visible "+i);
                await View("clock-"+room.ClockState,new(3.55f,2.66f,-2.6f),new(3.55f,2.66f,-4.245f),35);
            }
            await View("paper-maintenance",new(-3.55f,1.45f,2.70f),new(-3.55f,.753f,2.25f),43);
            await View("paper-schedule",new(-3.4f,1.14f,-.60f),new(-3.4f,1.12f,-2.018f),36);
            _preview.Rig.Camera.MakeCurrent();_preview.Actor.Visible=true;
            _preview.Actor.GlobalPosition=new(0,0,3.6f);await Frames();await Walk(new(0,0,-1.8f));await Walk(new(0,0,3.6f));
            var position=_preview.Actor.GlobalPosition;var yaw=_preview.Rig.YawDegrees;
            await _preview.ToggleBaseline();await _preview.ToggleBaseline();
            Require(_preview.Baseline==baseline && _preview.Actor.GlobalPosition.DistanceTo(position)<.01f && Mathf.Abs(_preview.Rig.YawDegrees-yaw)<.01f,"new/old toggle retains viewpoint");
            Require(stateBefore==JsonSerializer.Serialize(GetNode<GameSession>("/root/GameSession").State),"formal session state unchanged");
            var after=Saves();Require(saves.Count==after.Count && saves.All(kv=>after.TryGetValue(kv.Key,out var hash)&&hash==kv.Value),"user JSON saves byte-identical");
            File.WriteAllText(Path.Combine(_directory,"results.json"),JsonSerializer.Serialize(new {status="PASS",baseline,checks=_checks,
                device=RenderingServer.GetVideoAdapterName(),targetHardwareVerified=false},new JsonSerializerOptions {WriteIndented=true}));
            GD.Print($"ENVIRONMENT_REFINEMENT_PASS checks={_checks.Count} output={_directory}");
            _preview.Free();_camera.Free();await Frames(4);GetTree().Quit();
        }
        catch(Exception e){Release();GD.PushError(e.ToString());GetTree().Quit(1);}
    }
}
