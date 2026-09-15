using Godot;
using System.Text.Json;
using TownRpg.Presentation;
using TownRpg.Application;
using TownRpg.Core.Services;

namespace TownRpg.Testing;

public partial class VillageV1Probe
{
    private bool HouseArtCheck=>OS.GetCmdlineUserArgs().Contains("--village-v5");
    private bool HouseBaseline=>OS.GetCmdlineUserArgs().Contains("--house-baseline");
    private string HouseOutput=>ProjectSettings.GlobalizePath($"res://outputs/village-v5/{_compactRun}-{(HouseBaseline?"v4":"v5")}");
    private int _houseFrame;
    private async Task HouseWalk(float x,float z,bool slow=false,string? frames=null)
    {
        var previous=_village.ActivePlayer.GlobalPosition;var stagnant=0d;
        for(var i=0;i<2400;i++)
        {
            var p=_village.ActivePlayer.GlobalPosition;var d=new Vector2(x-p.X,z-p.Z);
            if(d.Length()<.15f){ReleaseMovement();await Frames(7);Require(true,$"house continuous walk {x},{z} slow={slow}");return;}
            d=d.Normalized().Rotated(Mathf.DegToRad(_village.ActiveCamera.YawDegrees))*(slow?1:Math.Clamp(d.Length()*2,.65f,1));
            Input.ActionPress("move_left",Math.Max(0,-d.X));Input.ActionPress("move_right",Math.Max(0,d.X));Input.ActionPress("move_up",Math.Max(0,-d.Y));Input.ActionPress("move_down",Math.Max(0,d.Y));if(slow)Input.ActionPress("move_slow");
            await Frames(1);var now=_village.ActivePlayer.GlobalPosition;stagnant=new Vector2(now.X-previous.X,now.Z-previous.Z).Length()<.002?stagnant+GetPhysicsProcessDeltaTime():0;previous=now;
            if(stagnant>1.5)throw new Exception($"House route blocked {now} -> {x},{z}: "+string.Join(" | ",Enumerable.Range(0,_village.ActivePlayer.GetSlideCollisionCount()).Select(j=>{var c=_village.ActivePlayer.GetSlideCollision(j);return c.GetCollider()+" "+c.GetNormal();})));
            if(frames is not null && i%4==0){await ToSignal(RenderingServer.Singleton,RenderingServer.SignalName.FramePostDraw);using var im=GetViewport().GetTexture().GetImage();im.SaveJpg(Path.Combine(frames,$"{_houseFrame++:D5}.jpg"),.87f);}
        }
        throw new Exception("House route timeout");
    }
    private async Task RunHouseArt(double loadMs,string state,byte[]? save,string path)
    {
        Engine.TimeScale=1;Require(_village.Exterior.UseHouseArtSample==!HouseBaseline,"sample isolated");Require(_village.Exterior.Layout.Compact && _village.Exterior.Surface.Ready,"adopted compact ground ready");Require(_village.ActivePlayer.Speed==4.2f,"speed unchanged");
        if(OS.GetCmdlineUserArgs().Contains("--house-load"))
        {
            await Place(-71,51);var times=new List<double>();
            for(var i=0;i<180;i++){var t=Time.GetTicksUsec();_village.ActiveCamera.SetYawImmediate(i*.5f);await ToSignal(RenderingServer.Singleton,RenderingServer.SignalName.FramePostDraw);times.Add((Time.GetTicksUsec()-t)/1000d);}
            var result=new{loadMs,meanMs=times.Average(),p95Ms=times.Order().ElementAt(171),workingSet=System.Diagnostics.Process.GetCurrentProcess().WorkingSet64,videoMemory=Performance.GetMonitor(Performance.Monitor.RenderVideoMemUsed),textureMemory=Performance.GetMonitor(Performance.Monitor.RenderTextureMemUsed),renderer=RenderingServer.GetVideoAdapterName()};
            await ClosePreview();File.WriteAllText(Path.Combine(Output,"load.json"),JsonSerializer.Serialize(result));GD.Print("V5_LOAD_PASS "+loadMs);GetTree().Quit();return;
        }
        foreach(var size in new[]{new Vector2I(1280,720),new Vector2I(1600,900),new Vector2I(1920,1080)})
        {
            DisplayServer.WindowSetMode(DisplayServer.WindowMode.Windowed);DisplayServer.WindowSetFlag(DisplayServer.WindowFlags.Borderless,true);GetWindow().Size=size;if(size.X==1920)DisplayServer.WindowSetMode(DisplayServer.WindowMode.Fullscreen);await Frames(15);
            var dir=Path.Combine(Output,$"{size.X}x{size.Y}");Directory.CreateDirectory(dir);
            foreach(var house in VillageHouseArtLayout3D.Layout.Houses)
            {
                var b=_village.Exterior.Layout.Geometry.Buildings.Single(b=>b.Id==house.Id);var end=b.LocalPad.Z+b.LocalPad.Depth;
                var front=b.ToWorld(new(0,end+1.4f));await Place(front.X,front.Y);_village.ActiveCamera.SetYawImmediate(b.Yaw);_village.ActiveCamera.SnapToTarget();await SaveCapture(dir,house.Id+"-player",size);
                // Explicit presentation cameras for facade/side review, never used as traversal evidence.
                var camera=new Camera3D{Fov=65,Near=.10f,Far=350};_village.Exterior.AddChild(camera);
                foreach(var side in new[]{false,true})
                {
                    var at=b.ToWorld(new(side?b.Size[0]*.72f:0,side?end+.8f:end+2));var target=b.ToWorld(new(0,b.Size[2]/2+1));
                    camera.GlobalPosition=new(at.X,b.Elevation+(side?4.8f:3.6f),at.Y);camera.LookAt(new Vector3(target.X,b.Elevation+1.8f,target.Y));camera.MakeCurrent();await SaveCapture(dir,house.Id+(side?"-side":"-front"),size);
                }
                camera.QueueFree();_village.ActiveCamera.Camera.MakeCurrent();await Frames(3);
                if(!OS.GetCmdlineUserArgs().Contains("--house-views-only"))
                {
                    var video=Path.Combine(dir,house.Id+"-frames");Directory.CreateDirectory(video);_houseFrame=0;
                    foreach(var (x,z,slow) in new[]{(0f,end,false),(0f,4.95f,false),(0f,6f,true),(3.05f,7.2f,true),(0f,7.2f,false),(-.75f,end-.5f,true),(-.75f,end+1.2f,true),(0f,end+1.4f,false)})
                    {var p=b.ToWorld(new(x,z));await HouseWalk(p.X,p.Y,slow,video);}
                    Require(_village.ActivePlayer.Velocity.Length()<.15,"stop outside "+house.Id);
                    _village.SetPaused(true);var before=_village.ActivePlayer.GlobalPosition;Input.ActionPress("move_up");await Frames(8);Require(before.DistanceTo(_village.ActivePlayer.GlobalPosition)<.01,"pause blocks motion");ReleaseMovement();_village.SetPaused(false);
                }
            }
        }
        if(!HouseBaseline)
        {
            foreach(var p in VillageHouseArtLayout3D.Layout.Props.Where(p=>p.Solid))
            {
                var b=_village.Exterior.Layout.Geometry.Buildings.Single(b=>b.Id==p.House);var pos=b.ToWorld(new(p.Position[0],p.Position[2]));
                using var ray=PhysicsRayQueryParameters3D.Create(new(pos.X,b.Elevation+p.Position[1]+p.Size[1]+.2f,pos.Y),new(pos.X,b.Elevation+p.Position[1]+.05f,pos.Y),1);
                var hit=_village.Exterior.GetWorld3D().DirectSpaceState.IntersectRay(ray);Require(hit.Count>0 && hit["collider"].AsGodotObject() is Node n && n.Name=="V5_"+p.Id,"shared prop collision "+p.House+"/"+p.Id);
            }
        }
        Require(SaveGameSerializer.Serialize(GetNode<GameSession>("/root/GameSession").State)==state,"formal state unchanged");Require(save is null?!File.Exists(path):File.ReadAllBytes(path).SequenceEqual(save),"formal save unchanged");
        var report=new{loadMs,checks=_checks,screens=_captures,baseline=HouseBaseline,humanAcceptance="pending"};await ClosePreview();File.WriteAllText(Path.Combine(Output,"houses.json"),JsonSerializer.Serialize(report,new JsonSerializerOptions{WriteIndented=true}));GD.Print("VILLAGE_V5_HOUSES_PASS checks="+_checks.Count);GetTree().Quit();
    }
}
