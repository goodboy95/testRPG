using Godot;
using System.Text.Json;
using TownRpg.Application;
using TownRpg.Core.Domain;
using TownRpg.Core.Services;
using TownRpg.Presentation;

namespace TownRpg.Testing;

public partial class VillageV1Probe
{
    private bool HomeTreesCheck=>OS.GetCmdlineUserArgs().Contains("--village-v6");
    private bool HomeTreesBaseline=>OS.GetCmdlineUserArgs().Contains("--home-trees-baseline");
    private bool HomeTreesAdoption=>OS.GetCmdlineUserArgs().Contains("--v6-adoption");
    private string HomeTreesOutput=>ProjectSettings.GlobalizePath(OS.GetCmdlineUserArgs().Contains("--village-v8") ? $"res://outputs/village-v8/{_compactRun}-routes" : $"res://outputs/{(HomeTreesAdoption?"village-v6-adoption":"village-v6")}/{_compactRun}-{(HomeTreesBaseline?"v5":"v6")}");
    private async Task HomeTreesTransition(bool interior)
    {
        await KeyInput(Key.F);
        for(var i=0;i<600;i++){await Frames(1);if(!_village.IsTransitioning&&_village.IsInterior==interior){_village.ActiveCamera.SetYawImmediate(0);_village.ActiveCamera.SnapToTarget();return;}}
        throw new Exception("actual F transition failed");
    }
    private async Task HomeTreesDoor(string id)
    {
        await KeyInput(Key.F);for(var i=0;i<180&&_village.Interior!.DoorMoving(id);i++)await Frames(1);
        Require(_village.Interior!.DoorOpen(id),"natural approach opens "+id);
    }
    private async Task RunHomeTrees(double loadMs,string state,byte[]? save,string path)
    {
        Engine.TimeScale=1;Require(_village.Exterior.UseHomeTreesSample==!HomeTreesBaseline,"home-tree sample isolated");
        Require(_village.Exterior.UseHouseArtSample,"v5 four houses retained");Require(_village.Exterior.Surface.Ready,"repaired terrain ready");
        if(OS.GetCmdlineUserArgs().Contains("--home-trees-load"))
        {
            await Place(-56,55);_village.ActiveCamera.SetYawImmediate(-90);var times=new List<double>();
            for(var i=0;i<300;i++)
            {
                if(i==0)Input.ActionPress("move_up");if(i==150){ReleaseMovement();Input.ActionPress("move_down");}
                var tick=Time.GetTicksUsec();await ToSignal(RenderingServer.Singleton,RenderingServer.SignalName.FramePostDraw);times.Add((Time.GetTicksUsec()-tick)/1000d);
            }
            ReleaseMovement();var result=new{loadMs,meanMs=times.Average(),p95Ms=times.Order().ElementAt(285),workingSet=System.Diagnostics.Process.GetCurrentProcess().WorkingSet64,videoMemory=Performance.GetMonitor(Performance.Monitor.RenderVideoMemUsed),textureMemory=Performance.GetMonitor(Performance.Monitor.RenderTextureMemUsed),renderer=RenderingServer.GetVideoAdapterName()};
            await ClosePreview();File.WriteAllText(Path.Combine(Output,"load.json"),JsonSerializer.Serialize(result));GD.Print("V6_LOAD_PASS");GetTree().Quit();return;
        }
        if(!HomeTreesBaseline)
        {
            var trees=UiUxCapture.Find<MeshInstance3D>(_village.Exterior.ModelRoot).Where(n=>n.Name.ToString().StartsWith("Tree_")||n.Name=="PlazaTree").ToArray();Require(trees.Length==58,"58 trees");
            var lods=trees.Where(n=>n.Name!="PlazaTree"&&n.Name!="Tree_037"&&n.Name!="Tree_043").Select(n=>n.Mesh).OfType<ArrayMesh>().Distinct().ToArray();Require(lods.Length==3,"three shared replacement crowns");
            foreach(var mesh in lods)Require(Enumerable.Range(0,mesh.GetSurfaceCount()).Any(s=>{var surface=RenderingServer.Singleton.Call("mesh_get_surface",mesh.GetRid(),s).AsGodotDictionary();return surface.TryGetValue("lods",out var value)&&value.AsGodotArray().Count>0;}),"tree imported LOD present");
            foreach(var mesh in UiUxCapture.Find<MeshInstance3D>(_village.Exterior.ModelRoot).Where(n=>n.Name=="HouseShell_house02"||n.Name=="PlazaTree").Select(n=>n.Mesh).OfType<ArrayMesh>())
                Require(Enumerable.Range(0,mesh.GetSurfaceCount()).All(s=>{var surface=RenderingServer.Singleton.Call("mesh_get_surface",mesh.GetRid(),s).AsGodotDictionary();return !surface.TryGetValue("lods",out var value)||value.AsGodotArray().Count==0;}),"non-target mesh keeps original detail");
        }
        foreach(var size in new[]{new Vector2I(1280,720),new Vector2I(1600,900),new Vector2I(1920,1080)})
        {
            DisplayServer.WindowSetMode(DisplayServer.WindowMode.Windowed);DisplayServer.WindowSetFlag(DisplayServer.WindowFlags.Borderless,true);GetWindow().Size=size;if(size.X==1920)DisplayServer.WindowSetMode(DisplayServer.WindowMode.Fullscreen);await Frames(15);
            var dir=Path.Combine(Output,$"{size.X}x{size.Y}");Directory.CreateDirectory(dir);
            foreach(var (name,x,z,yaw) in new[]{("home-west",-65f,55f,-38f),("home-east",-46f,54f,35f),("gate",-56f,55f,0f),("porch",-56f,49f,0f),("trees-west",-70f,55f,-90f),("trees-center",-28f,34f,0f),("trees-east",68f,12f,-90f),("trees-station",-28f,-36f,0f)})
            {await Place(x,z);_village.ActiveCamera.SetYawImmediate(yaw);_village.ActiveCamera.SnapToTarget();await SaveCapture(dir,name,size);}
            var tree=UiUxCapture.Find<MeshInstance3D>(_village.Exterior.ModelRoot).First(n=>n.Name=="Tree_037");
            var camera=new Camera3D{Fov=66,Near=.1f,Far=350};_village.Exterior.AddChild(camera);camera.GlobalPosition=tree.GlobalPosition+new Vector3(2,1.5f,3);camera.LookAt(tree.GlobalPosition+Vector3.Up*5);camera.MakeCurrent();await SaveCapture(dir,"tree-under",size);camera.QueueFree();_village.ActiveCamera.Camera.MakeCurrent();await Frames(3);
            foreach(var name in new[]{"Tree_000","Tree_001","Tree_002"})
            {
                var t=UiUxCapture.Find<MeshInstance3D>(_village.Exterior.ModelRoot).Single(n=>n.Name==name);var c=new Camera3D{Fov=58,Near=.1f,Far=350};_village.Exterior.AddChild(c);c.GlobalPosition=t.GlobalPosition+new Vector3(6,3.1f,8)*t.Scale.X;c.LookAt(t.GlobalPosition+Vector3.Up*3.8f*t.Scale.X);c.MakeCurrent();await SaveCapture(dir,name+"-shape",size);c.QueueFree();_village.ActiveCamera.Camera.MakeCurrent();await Frames(3);
            }
            if(HomeTreesBaseline || OS.GetCmdlineUserArgs().Contains("--home-trees-views-only"))continue;
            var treeVideo=Path.Combine(dir,"trees-walk-frames");Directory.CreateDirectory(treeVideo);_houseFrame=0;
            await Place(-94,-59);_village.ActiveCamera.SetYawImmediate(0);_village.ActiveCamera.SnapToTarget();
            await HouseWalk(-94,-82,false,treeVideo);await HouseWalk(-90,-82,true,treeVideo);await HouseWalk(-90,-90,false,treeVideo);await HouseWalk(-90,-82,false,treeVideo);await HouseWalk(-94,-59,false,treeVideo);
            // Initial placement is an actual deserialized bedroom snapshot, then uninterrupted input to the road and back.
            var fixture=SaveGameSerializer.Deserialize(File.ReadAllText(ProjectSettings.GlobalizePath("res://outputs/home-access/user-save-original.json")));
            var snapshot=fixture.VillageExploration!;snapshot.X=-3.1f;snapshot.Y=0;snapshot.Z=2.8f;snapshot.CameraYaw=0;snapshot.WakeIntroductionComplete=true;snapshot.Doors["bedroom"]=false;
            File.WriteAllText(Path.Combine(dir,"isolated-save.json"),SaveGameSerializer.Serialize(fixture));
            snapshot=SaveGameSerializer.Deserialize(File.ReadAllText(Path.Combine(dir,"isolated-save.json"))).VillageExploration!;
            await _village.RestoreAsync(VillageExplorationState.HomeLocation,snapshot);_village.SetHostLocked(false);await Frames(8);
            Require(_village.Interior!.UseIdentitySample,"matching home appearance");
            var video=Path.Combine(dir,"home-walk-frames");Directory.CreateDirectory(video);_houseFrame=0;
            await HouseWalk(-2.55f,2.1f,false,video);await HomeTreesDoor("bedroom");await HouseWalk(-.6f,2.1f,false,video);await HouseWalk(0,3.05f,false,video);await HomeTreesTransition(false);
            foreach(var (x,z,slow) in new[]{(-56f,49f,false),(-56f,55f,false),(-56f,61f,false),(-56f,55f,true),(-65f,55f,false),(-56f,55f,false)})await HouseWalk(x,z,slow,video);
            _village.SetPaused(true);var p=_village.ActivePlayer.Position;Input.ActionPress("move_up");await Frames(10);ReleaseMovement();Require(p.DistanceTo(_village.ActivePlayer.Position)<.01f,"pause stable");_village.SetPaused(false);
            foreach(var offset in new[]{-.85f,0f,.85f})
            {await HouseWalk(-56+offset,55,true,video);await HouseWalk(-56+offset,49,true,video);await HouseWalk(-56+offset,55,true,video);await HouseWalk(-56+offset,60,false,video);await HouseWalk(-56+offset,55,false,video);}
            await HouseWalk(-56,49,false,video);await HouseWalk(-56,45.85f,false,video);await HomeTreesTransition(true);Require(_village.Interior.DoorOpen("bedroom"),"door cache retained");
            await HouseWalk(0,2.1f,false,video);await HouseWalk(-3.1f,2.1f,false,video);await HouseWalk(-3.1f,2.8f,false,video);
            var end=_village.Capture();await _village.RestoreAsync(VillageExplorationState.HomeLocation,end);_village.SetHostLocked(false);await Frames(8);Require(_village.ActivePlayer.Position.DistanceTo(new(end.X,end.Y,end.Z))<.15f,"repeat restore at actual bedroom endpoint");
            await _village.SwitchArea(false);
        }
        Require(SaveGameSerializer.Serialize(GetNode<GameSession>("/root/GameSession").State)==state,"formal state unchanged");Require(save is null?!File.Exists(path):File.ReadAllBytes(path).SequenceEqual(save),"real save unchanged");
        var report=new{loadMs,checks=_checks,screens=_captures,baseline=HomeTreesBaseline,humanAcceptance="pending"};await ClosePreview();File.WriteAllText(Path.Combine(Output,"home-trees.json"),JsonSerializer.Serialize(report,new JsonSerializerOptions{WriteIndented=true}));GD.Print("VILLAGE_V6_HOME_TREES_PASS checks="+_checks.Count);GetTree().Quit();
    }
}
