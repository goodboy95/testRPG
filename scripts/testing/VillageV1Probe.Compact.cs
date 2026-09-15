using Godot;
using System.Text.Json;
using TownRpg.Presentation;
using TownRpg.Core.Domain;
using TownRpg.Core.Services;
using TownRpg.Application;

namespace TownRpg.Testing;

public partial class VillageV1Probe
{
    private bool CompactCheck=>OS.GetCmdlineUserArgs().Contains("--village-v4");
    private bool CompactBaseline=>OS.GetCmdlineUserArgs().Contains("--v4-baseline");
    private readonly string _compactRun=DateTime.Now.ToString("yyyyMMdd-HHmmss");
    private string CompactOutput=>ProjectSettings.GlobalizePath($"res://outputs/{(OS.GetCmdlineUserArgs().Contains("--home-access")?"home-access/routes":"village-v4")}/{_compactRun}-{(CompactBaseline?"baseline":_gray?"graybox":"v4")}");
    private async Task RunCompact(double loadMs,string state,byte[]? save,string savePath)
    {
        Engine.TimeScale=1;
        var layout=_village.Exterior.Layout;
        Require(layout.Compact!=CompactBaseline,"instance layout selection");
        Require(layout.Geometry.Buildings.Length==24,"24 buildings");
        Require(_village.ActivePlayer.Speed==4.2f,"outdoor speed unchanged");
        if(OS.GetCmdlineUserArgs().Contains("--v4-load"))
        {
            await Place(-56,55);var times=new List<double>();
            for(var i=0;i<180;i++){var start=Time.GetTicksUsec();_village.ActiveCamera.SetYawImmediate(i*.5f);await ToSignal(RenderingServer.Singleton,RenderingServer.SignalName.FramePostDraw);times.Add((Time.GetTicksUsec()-start)/1000d);}
            var result=new {loadMs,meanMs=times.Average(),p95Ms=times.Order().ElementAt(171),workingSet=System.Diagnostics.Process.GetCurrentProcess().WorkingSet64,videoMemory=Performance.GetMonitor(Performance.Monitor.RenderVideoMemUsed),textureMemory=Performance.GetMonitor(Performance.Monitor.RenderTextureMemUsed),renderer=RenderingServer.GetVideoAdapterName()};
            await ClosePreview();File.WriteAllText(Path.Combine(Output,"load.json"),JsonSerializer.Serialize(result));GD.Print("V4_LOAD_PASS "+loadMs);GetTree().Quit();return;
        }
        if(!OS.GetCmdlineUserArgs().Contains("--v4-views"))
        {
            await Inputs();
            foreach(var r in layout.Geometry.Routes)await WalkRoute(r);
            if(!CompactBaseline)
            {
                foreach(var zone in layout.Fixtures.Zones.Concat(layout.PlanningZones))
                {var q=zone.Rect;await WalkRoute(new("clear_"+zone.Id,[ [q[0]+.4f,q[1]+.4f],[q[0]+q[2]-.4f,q[1]+.4f],[q[0]+q[2]-.4f,q[1]+q[3]-.4f],[q[0]+.4f,q[1]+q[3]-.4f] ]));}
                foreach(var b in layout.Geometry.Buildings.Where(b=>b.Kind=="house"))
                {
                    var pad=b.LocalPad;var gate=b.ToWorld(new(0,pad.Z+pad.Depth));var near=b.ToWorld(new(0,pad.Z+pad.Depth+2));var inside=b.ToWorld(new(0,pad.Z+pad.Depth-2));
                    await WalkRoute(new("gate_"+b.Id,[[near.X,near.Y],[gate.X,gate.Y],[inside.X,inside.Y]]));
                }
                foreach(var fixture in layout.Fixtures.Props.Where(p=>p.Solid && p.Kind!="overhead"))
                {using var ray=PhysicsRayQueryParameters3D.Create(new(fixture.Position[0],fixture.Position[1]+fixture.Size[1]/2,fixture.Position[2]+fixture.Size[2]/2+.3f),new(fixture.Position[0],fixture.Position[1]+fixture.Size[1]/2,fixture.Position[2]-fixture.Size[2]/2-.3f),1);Require(_village.Exterior.GetWorld3D().DirectSpaceState.IntersectRay(ray).Count>0,"fixture collider "+fixture.Id);}
                await CompactRestore();
            }
            if(!CompactBaseline)await BridgeApproaches();await BoundariesAndCamera();await HomeTransitions();
            Require(SaveGameSerializer.Serialize(GetNode<GameSession>("/root/GameSession").State)==state,"formal state untouched");
            Require(save is null ? !File.Exists(savePath) : File.ReadAllBytes(savePath).SequenceEqual(save),"formal file untouched");
            _village.ActivePlayer.ConfigureDefaultCharacterVisual(DreamCharacterFrames.Create(ResourceLoader.Load<Texture2D>("res://assets/generated/dream_escape/pixel/protagonist-walk-4x4-v1.png")),"res://tests/v4-missing.glb");
            Require(!_village.ActivePlayer.UsesDefault3DModel,"billboard fallback");
            _village.ActivePlayer.ConfigureDefaultCharacterVisual(DreamCharacterFrames.Create(ResourceLoader.Load<Texture2D>("res://assets/generated/dream_escape/pixel/protagonist-walk-4x4-v1.png")));
        }
        if(_capture)await CaptureCompact();
        var report=new {status="pass",loadMs,checks=_checks,routes=_routes,screens=_captures,humanAcceptance="pending"};
        await ClosePreview();File.WriteAllText(Path.Combine(Output,"results.json"),JsonSerializer.Serialize(report,new JsonSerializerOptions{WriteIndented=true}));GD.Print("VILLAGE_V4_PROBE_PASS checks="+_checks.Count);GetTree().Quit();
    }
    private async Task CompactRestore()
    {
        foreach(var p in new[]{new Vector3(-56,0,49),new Vector3(-20,1.935f,16),new Vector3(500,0,500),new Vector3(-4,1.935f,-3),new Vector3(VillageLayout3D.StreamX(28),VillageLayout3D.Layout.Bridges[0].DeckY,28)})
        {
            var snapshot=new VillageExplorationState {X=p.X,Y=p.Y,Z=p.Z,WakeIntroductionComplete=true,CameraYaw=90};
            await _village.RestoreAsync(VillageExplorationState.VillageLocation,snapshot);_village.SetHostLocked(false);await Frames(4);
            Require(_village.SafePose(_village.ActivePlayer.GlobalPosition),"restore valid "+p);
            var once=_village.Capture();await _village.RestoreAsync(VillageExplorationState.VillageLocation,once);_village.SetHostLocked(false);await Frames(4);
            Require(_village.ActivePlayer.Position.DistanceTo(new(once.X,once.Y,once.Z))<.15f,"repeat restore stable");
        }
    }
    private async Task CaptureCompact()
    {
        foreach(var size in new[]{new Vector2I(1280,720),new Vector2I(1600,900),new Vector2I(1920,1080)})
        {
            DisplayServer.WindowSetMode(DisplayServer.WindowMode.Windowed);DisplayServer.WindowSetFlag(DisplayServer.WindowFlags.Borderless,true);GetWindow().Size=size;if(size.X==1920)DisplayServer.WindowSetMode(DisplayServer.WindowMode.Fullscreen);await Frames(15);
            var dir=Path.Combine(Output,$"{size.X}x{size.Y}");Directory.CreateDirectory(dir);
            foreach(var (name,x,z,yaw) in new[]{("home",-56f,55f,0f),("lane",-70f,55f,-90f),("center",-28f,34f,0f),("shop",-20f,26.5f,0f),("workshop",-23f,7f,0f),("east",68f,12f,-90f),("station",-28f,-36f,0f),("bridge",38f,28f,-90f)})
            {
                var px=x;var pz=z;if(CompactBaseline){if(name is "center" or "shop"){px+=16;pz-=19;}if(name=="workshop")pz-=16;if(name=="station")pz-=45;if(name=="east")px+=4;}
                await Place(px,pz);_village.ActiveCamera.SetYawImmediate(yaw);_village.ActiveCamera.SnapToTarget();await SaveCapture(dir,name,size);
            }
            var camera=new Camera3D{Fov=58,Near=1,Far=850};_village.Exterior.AddChild(camera);camera.GlobalPosition=new(-125,140,145);camera.LookAt(new Vector3(-10,1,8));camera.MakeCurrent();await SaveCapture(dir,"overview",size);camera.QueueFree();_village.ActiveCamera.Camera.MakeCurrent();await Frames(3);
            if(OS.GetCmdlineUserArgs().Contains("--v4-video"))
            {
                var frames=Path.Combine(dir,"frames");Directory.CreateDirectory(frames);await Place(-56,55);_village.ActiveCamera.SetYawImmediate(-90);_village.ActiveCamera.SnapToTarget();
                for(var i=0;i<180;i++){ReleaseMovement();if(i<110)Input.ActionPress("move_up");else if(i<145)Input.ActionPress("move_right");if(i>=145)Input.ActionPress("move_slow");await ToSignal(RenderingServer.Singleton,RenderingServer.SignalName.FramePostDraw);using var img=GetViewport().GetTexture().GetImage();img.SavePng(Path.Combine(frames,$"{i:0000}.png"));}
                ReleaseMovement();
            }
        }
    }
}
