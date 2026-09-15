using Godot;
using System.Text.Json;
using TownRpg.Application;
using TownRpg.Core.Domain;
using TownRpg.Core.Services;
using TownRpg.Presentation;

namespace TownRpg.Testing;

/// <summary>Continuous input from an isolated bedroom save through the yard gate. No actor teleport during the journey.</summary>
public partial class HomeAccessProbe : Node
{
    private MainController _main=null!;
    private GameSession Session=>GetNode<GameSession>("/root/GameSession");
    private VillageAreaHost Host=>_main.VillageRuntime!;
    private string _output="",_save="";
    private readonly List<string> _checks=[];
    private readonly List<object> _trace=[];
    private int Fps=>OS.GetCmdlineUserArgs().Contains("--access-30")?30:OS.GetCmdlineUserArgs().Contains("--access-120")?120:60;
    private bool Baseline=>OS.GetCmdlineUserArgs().Contains("--access-baseline");
    private void Check(bool ok,string text){if(!ok)throw new InvalidOperationException(text);_checks.Add(text);GD.Print("ACCESS_CHECK "+text);}
    private bool _record;
    private int _frame,_videoFrame;
    private async Task Frames(int n=1)
    {
        for(var i=0;i<n;i++)
        {
            await ToSignal(GetTree(),SceneTree.SignalName.ProcessFrame);
            if(_record && ++_frame % (Fps/15)==0)
            {
                await ToSignal(RenderingServer.Singleton,RenderingServer.SignalName.FramePostDraw);
                using var image=GetViewport().GetTexture().GetImage();
                image.SaveJpg(Path.Combine(_output,"video",$"{_videoFrame++:D5}.jpg"),.85f);
            }
        }
    }
    private async Task Key(Key key){Input.ParseInputEvent(new InputEventKey{PhysicalKeycode=key,Keycode=key,Pressed=true});await Frames(2);Input.ParseInputEvent(new InputEventKey{PhysicalKeycode=key,Keycode=key});await Frames(3);}
    private async Task Click(string label){UiUxCapture.Find<Button>(_main).First(b=>b.Text==label&&b.IsVisibleInTree()).GrabFocus();await Key(Godot.Key.Enter);}
    private static void Release(){foreach(var a in new[]{"move_up","move_down","move_left","move_right","move_slow"})Input.ActionRelease(a);}
    private async Task Settle(){for(var i=0;i<1200;i++){await Frames();if(_main.VillageRuntime is not null&&!Session.Modal.IsBlocked&&!Host.IsTransitioning){await Frames(5);return;}}throw new Exception("load/transition timeout");}
    private object Contact(string label)
    {
        var p=Host.ActivePlayer;var pos=p.GlobalPosition;
        using var ray=PhysicsRayQueryParameters3D.Create(pos+Vector3.Up*3,pos-Vector3.Up*4,1);ray.Exclude=new Godot.Collections.Array<Rid>{p.GetRid()};
        var hit=p.GetWorld3D().DirectSpaceState.IntersectRay(ray);
        return new{label,position=pos.ToString(),velocity=p.Velocity.ToString(),floor=p.IsOnFloor(),ground=Host.IsInterior?0:Host.Exterior.WalkHeight(pos.X,pos.Z),ray=hit.ToString(),contacts=Enumerable.Range(0,p.GetSlideCollisionCount()).Select(i=>{var c=p.GetSlideCollision(i);return new{body=c.GetCollider()?.ToString(),point=c.GetPosition().ToString(),normal=c.GetNormal().ToString()};}).ToArray()};
    }
    private async Task Walk(float x,float z,bool slow=false)
    {
        Host.ActiveCamera.SetYawImmediate(0);Host.ActiveCamera.SnapToTarget();
        var previous=Host.ActivePlayer.GlobalPosition;var stagnant=0d;
        for(var i=0;i<2400;i++)
        {
            var p=Host.ActivePlayer.GlobalPosition;var d=new Vector2(x-p.X,z-p.Z);
            if(d.Length()<.13f){Release();await Frames(8);Check(true,$"walk {x},{z}");return;}
            // Shift walks at its normal speed; do not reduce input into the joystick dead zone at waypoints.
            var strength=slow?1:Math.Clamp(d.Length()*2,.65f,1);d=d.Normalized()*strength;
            Input.ActionPress("move_left",Math.Max(0,-d.X));Input.ActionPress("move_right",Math.Max(0,d.X));Input.ActionPress("move_up",Math.Max(0,-d.Y));Input.ActionPress("move_down",Math.Max(0,d.Y));if(slow)Input.ActionPress("move_slow");
            await Frames();var now=Host.ActivePlayer.GlobalPosition;
            stagnant=new Vector2(now.X-previous.X,now.Z-previous.Z).Length()<.002f?stagnant+GetProcessDeltaTime():0;previous=now;
            if(i%15==0)_trace.Add(Contact($"to {x},{z}"));
            if(stagnant>1.5){_trace.Add(Contact("BLOCKED"));throw new Exception($"continuous walk blocked {now} -> {x},{z}");}
        }
        throw new Exception("walk timeout");
    }
    private async Task Reload(){await Key(Godot.Key.Escape);await Click("返回标题");await Frames(8);await Click("继续");await Settle();}
    private async Task WaitDoor(string id)
    {
        for(var i=0;i<Fps*3 && Host.Interior!.DoorMoving(id);i++)await Frames();
        Check(!Host.Interior!.DoorMoving(id),"door completed "+id);
    }
    private async Task DoorRoundtrip(string id,float x,float z,int insideSign)
    {
        await Walk(.55f,z);await Walk(x-insideSign*.65f,z);
        if(Host.Interior!.DoorOpen(id)){await Key(Godot.Key.F);await Walk(.1f,z);await WaitDoor(id);await Walk(x-insideSign*.65f,z);}
        await Shot(id+"-approach");await Key(Godot.Key.F);
        if(id=="family")
        {
            await Key(Godot.Key.Escape);
            var hinge=UiUxCapture.Find<Node3D>(Host.Interior).Single(n=>n.Name=="Door_family_Hinge0");var angle=hinge.Rotation.Y;
            await Frames(Fps/2);Check(Math.Abs(angle-hinge.Rotation.Y)<.0001f,"pause freezes moving door");await Click("继续");
        }
        await WaitDoor(id);Check(Host.Interior.DoorOpen(id)&&Host.Interior.DoorOpenSide(id)==insideSign,"push away from hall "+id);
        await Walk(x+insideSign*.65f,z);await Key(Godot.Key.F);await Frames(Fps/2);
        Check(Host.Interior.DoorMoving(id),"closing pauses before player "+id);
        if(id=="family")await Walk(x+insideSign*.65f,z+1.2f);
        else await Walk(x+insideSign*1.75f,z);
        await WaitDoor(id);Check(!Host.Interior.DoorOpen(id),"closing resumes after moving aside "+id);
        await Walk(x+insideSign*.65f,z);await Key(Godot.Key.F);await WaitDoor(id);
        Check(Host.Interior.DoorOpenSide(id)==-insideSign,"push away from room "+id);
        await Shot(id+"-reverse");await Reload();Check(Host.Interior!.DoorOpenSide(id)==-insideSign && Host.Interior.DoorOpen(id),"direction survives room save "+id);
        await Walk(x-insideSign*.65f,z);await Walk(.55f,z);
    }
    private async Task AllRooms()
    {
        await Walk(-2.55f,2.1f);await Key(Godot.Key.F);await WaitDoor("bedroom");
        await Key(Godot.Key.F);await WaitDoor("bedroom");await Walk(.55f,2.1f);
        await DoorRoundtrip("bedroom",-1.9f,2.1f,-1);
        await Walk(.55f,1.5f);await Walk(.55f,-2.5f);await DoorRoundtrip("family",-1.9f,-2.5f,-1);
        await DoorRoundtrip("bathroom",1.9f,-2.5f,1);
        await Walk(.55f,1.5f);await DoorRoundtrip("kitchen",1.9f,2,1);
    }
    private async Task Shot(string name){using var image=GetViewport().GetTexture().GetImage();image.SavePng(Path.Combine(_output,name+".png"));}
    public override void _Ready()=>CallDeferred(MethodName.Run);
    private async void Run()
    {
        IDisposable? lease=null;var passed=false;
        try
        {
            Check(OS.GetCmdlineUserArgs().Contains("--prologue-e2e"),"isolated save required");
            _output=ProjectSettings.GlobalizePath((OS.GetCmdlineUserArgs().Contains("--home-trees-regression")?"res://outputs/village-v6/home-access/":OS.GetCmdlineUserArgs().Contains("--house-art-regression")?"res://outputs/village-v5/home-access/":"res://outputs/home-access/")+DateTime.Now.ToString("yyyyMMdd-HHmmss")+(Baseline?"-before":"-after"));Directory.CreateDirectory(_output);_save=Path.Combine(_output,"save.json");lease=Session.UseTestSavePath(_save);
            var fixture=SaveGameSerializer.Deserialize(File.ReadAllText(ProjectSettings.GlobalizePath("res://outputs/home-access/user-save-original.json")));
            fixture.Player.LocationId=VillageExplorationState.HomeLocation;fixture.VillageExploration!.X=-3.1f;fixture.VillageExploration.Y=0;fixture.VillageExploration.Z=2.8f;fixture.VillageExploration.Doors["bedroom"]=Baseline;
            fixture.Settings.WindowWidth=Fps==30?1280:Fps==60?1600:1920;fixture.Settings.WindowHeight=fixture.Settings.WindowWidth*9/16;fixture.Settings.BorderlessFullscreen=Fps==120;
            File.WriteAllText(_save,SaveGameSerializer.Serialize(fixture));
            _main=ResourceLoader.Load<PackedScene>("res://scenes/app/Main.tscn").Instantiate<MainController>();AddChild(_main);await Frames(10);await Click("继续");await Settle();
            Check(Host.Exterior.UseHomeTreesSample && Host.Exterior.UseHouseArtSample,"formal route adopts village v6 and four houses");
            Check(Host.Interior!.UseIdentitySample,"formal bedroom adopts matching home exterior");
            Directory.CreateDirectory(Path.Combine(_output,"video"));_record=!Baseline;
            await Walk(-2.55f,2.1f);if(!Baseline){await Shot("door-before");await Key(Godot.Key.F);await Frames(Fps);Check(Host.Interior!.DoorOpen("bedroom"),"bedroom opens at natural approach");}
            await Walk(-.6f,2.1f);await Walk(0,3.05f);await Key(Godot.Key.F);await Settle();Check(!Host.IsInterior,"front door transition");
            await Walk(-56,49);await Shot("yard");await Walk(-56,51.9f);await Walk(-56,55);await Shot("outside-gate");
            await Walk(-56,61);await Walk(-56,55);_record=false;await Walk(-70,55);await Walk(-56,55);await Walk(-42,54);await Walk(-30,53);await Shot("village-road");await Walk(-42,54);await Walk(-56,55);
            await Reload();await Walk(-56,51.9f);await Walk(-56,49);await Walk(-56,45.85f);await Key(Godot.Key.F);await Settle();Check(Host.IsInterior,"return indoors");
            await Walk(0,2.1f);await Walk(-3.1f,2.1f);await Walk(-3.1f,2.8f);await Reload();Check(Host.IsInterior,"bedroom reload after continuous roundtrip");
            if(!Baseline)await AllRooms();
            await Walk(.55f,2.9f);await Walk(0,3.05f);await Key(Godot.Key.F);await Settle();
            foreach(var offset in new[]{-.85f,0f,.85f})
            {
                await Walk(-56,49,true);await Walk(-56+offset,49,true);await Walk(-56+offset,55,true);await Walk(-56+offset,60,true);await Walk(-56+offset,55,true);await Walk(-56+offset,49,true);
            }
            passed=true;
        }
        catch(Exception e){GD.PushError("HOME_ACCESS_FAIL "+e);if(_main?.VillageRuntime is not null){_trace.Add(Contact("failure"));await Shot("failure");}}
        finally
        {
            _record=false;Release();if(_main is not null){_main.QueueFree();await Frames(5);_main=null!;}GC.Collect();GC.WaitForPendingFinalizers();await Frames(5);lease?.Dispose();
            if(_output!="")File.WriteAllText(Path.Combine(_output,"report.json"),JsonSerializer.Serialize(new{passed,checks=_checks,trace=_trace},new JsonSerializerOptions{WriteIndented=true}));GD.Print("HOME_ACCESS_"+(passed?"PASS":"FAIL"));GetTree().Quit(passed?0:1);
        }
    }
}
