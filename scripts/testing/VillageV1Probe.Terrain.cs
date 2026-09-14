using Godot;
using System.Text.Json;
using TownRpg.Presentation;
using TownRpg.Core.Domain;

namespace TownRpg.Testing;

public partial class VillageV1Probe
{
    private bool TerrainBaseline=>OS.GetCmdlineUserArgs().Contains("--terrain-baseline");
    private bool TerrainCheck=>TerrainBaseline || OS.GetCmdlineUserArgs().Contains("--terrain-check");
    private string TerrainOutput=>ProjectSettings.GlobalizePath($"res://outputs/village-terrain/{_travelRun}-{(TerrainBaseline?"baseline":"sample")}");
    private async Task TerrainContract()
    {
        Require(_village.Exterior.UseTerrainSample && _village.Exterior.Surface.Ready,"Terrain3D sample selected and ready");
        Require(_village.Exterior.GetNodeOrNull("VillageGroundCollision") is null,"original collision omitted");
        var source=ProjectSettings.GlobalizePath("res://art/terrain/village-sample-v1/");
        var height=File.ReadAllBytes(source+"height.bin");var baseline=File.ReadAllBytes(source+"baseline.bin");var protect=File.ReadAllBytes(source+"protected.bin");
        var maximum=0f;var protectedMaximum=0f;var samples=0;var collisionMaximum=0f;
        for(var j=32;j<465;j+=3) for(var i=48;i<465;i+=3)
        {
            var index=j*512+i;var x=i*.5f-128;var z=j*.5f-128;
            var actual=_village.Exterior.Surface.GroundHeight(x,z);var expected=BitConverter.ToSingle(height,index*4);
            maximum=Math.Max(maximum,Math.Abs(actual-expected));samples++;
            if(protect[index]!=0) protectedMaximum=Math.Max(protectedMaximum,Math.Abs(actual-BitConverter.ToSingle(baseline,index*4)));
        }
        Require(maximum<.001f,$"native height map equals input ({maximum})");
        Require(protectedMaximum<=.01f,$"protected native heights ({protectedMaximum})");
        // Query real physical ground away from buildings, walls, and decorative solids.
        foreach(var (x,z) in new[]{(-70.25f,57.25f),(-47.25f,57.25f),(-24.25f,18.25f),(-8.25f,18.25f),(38.25f,12.25f),(38.25f,22.25f)})
        {
            var y=_village.Exterior.Surface.GroundHeight(x,z);
            using var query=PhysicsRayQueryParameters3D.Create(new(x,y+1,z),new(x,y-1,z),1);
            query.Exclude=new Godot.Collections.Array<Rid>{_village.ActivePlayer.GetRid()};
            var hit=_village.Exterior.GetWorld3D().DirectSpaceState.IntersectRay(query);
            Require(hit.Count>0,$"sample ground ray {x},{z}");
            var error=Math.Abs(hit["position"].AsVector3().Y-y);collisionMaximum=Math.Max(collisionMaximum,error);
            Require(error<.025f,$"native query/physics consistency {x},{z}: {error}");
        }
        await Place(-56,55);var snapshot=_village.Capture();
        await _village.RestoreAsync(VillageExplorationState.VillageLocation,snapshot);_village.SetHostLocked(false);await Frames(5);
        Require(_village.ActivePlayer.GlobalPosition.DistanceTo(new(snapshot.X,snapshot.Y,snapshot.Z))<.12f,"isolated pose roundtrip");
        snapshot.Y+=3;await _village.RestoreAsync(VillageExplorationState.VillageLocation,snapshot);_village.SetHostLocked(false);await Frames(5);
        Require(_village.ActivePlayer.GlobalPosition.DistanceTo(new(-56,0,49))<.15f,"changed height restores safe point");
        snapshot.X=200;await _village.RestoreAsync(VillageExplorationState.VillageLocation,snapshot);_village.SetHostLocked(false);await Frames(5);
        Require(_village.ActivePlayer.GlobalPosition.DistanceTo(new(-56,0,49))<.15f,"outside terrain restores safe point");
        snapshot.X=-56;snapshot.Y=0;snapshot.Z=40;
        await _village.RestoreAsync(VillageExplorationState.VillageLocation,snapshot);_village.SetHostLocked(false);await Frames(5);
        Require(_village.ActivePlayer.GlobalPosition.DistanceTo(new(-56,0,49))<.15f,"building collision restores safe point");
        GD.Print("TERRAIN_CONTRACT_ENTER_HOME");
        await _village.SwitchArea(true);Require(!_village.Exterior.Surface.Ready,"terrain collision disabled in cached exterior");
        await Frames(60);
        GD.Print("TERRAIN_CONTRACT_EXIT_HOME");
        await _village.SwitchArea(false);Require(_village.Exterior.Surface.Ready,"terrain collision restored after return");
        _village.ActivePlayer._Notification((int)NotificationWMWindowFocusIn);
        for(var frame=0;frame<30 && !_village.ActivePlayer.IsOnFloor();frame++) await Frames(1);
        Require(_village.ActivePlayer.IsOnFloor(),$"return physics ready: {_village.ActivePlayer.GlobalPosition}, velocity={_village.ActivePlayer.Velocity}, input={_village.ActivePlayer.InputEnabled}");
        GD.Print("TERRAIN_CONTRACT_PASS");
        File.WriteAllText(Path.Combine(Output,"terrain-contract.json"),JsonSerializer.Serialize(new {samples,maximum,protectedMaximum,collisionMaximum,checks=_checks},new JsonSerializerOptions { WriteIndented=true }));
    }
    private async Task CaptureTerrainMatrix()
    {
        foreach(var size in new[]{new Vector2I(1280,720),new Vector2I(1600,900),new Vector2I(1920,1080)})
        {
            DisplayServer.WindowSetMode(DisplayServer.WindowMode.Windowed);DisplayServer.WindowSetFlag(DisplayServer.WindowFlags.Borderless,true);
            GetWindow().Size=size;if(size.X==1920) DisplayServer.WindowSetMode(DisplayServer.WindowMode.Fullscreen);await Frames(20);
            var folder=Path.Combine(Output,size.X.ToString());Directory.CreateDirectory(folder);
            foreach(var view in new[]{("home",-61f,55f,-90f),("plaza",-9f,18f,25f),("bank",38f,16f,-90f)})
            {
                await Place(view.Item2,view.Item3);_village.ActiveCamera.SetYawImmediate(view.Item4);_village.ActiveCamera.SnapToTarget();
                _village.ActivePlayer.SetFacingImmediate(_village.ActiveCamera.HorizontalForward);
                await SaveCapture(folder,view.Item1,size);
            }
            var camera=new Camera3D { Fov=58,Near=.1f,Far=850 };_village.Exterior.AddChild(camera);camera.MakeCurrent();
            foreach(var view in new[]{("home-detail",new Vector3(-66,1,59),new Vector3(-63,-.5f,57)),
                ("plaza-detail",new Vector3(-9,3.2f,22),new Vector3(-10,1,17)),
                ("bank-detail",new Vector3(40,4,24),new Vector3(43,1,18))})
            { camera.GlobalPosition=view.Item2;camera.LookAt(view.Item3);await SaveCapture(folder,view.Item1,size); }
            camera.QueueFree();_village.ActiveCamera.Camera.MakeCurrent();await Frames(3);
            await Place(-71,55);_village.ActiveCamera.SetYawImmediate(-90);_village.ActiveCamera.SnapToTarget();
            _village.ActivePlayer.SetFacingImmediate(Vector3.Right);await Frames(12);
            var times=new List<double>();Input.ActionPress("move_up");
            for(var f=0;f<180;f++) { var before=Time.GetTicksUsec();await ToSignal(RenderingServer.Singleton,RenderingServer.SignalName.FramePostDraw);times.Add((Time.GetTicksUsec()-before)/1000d); }
            ReleaseMovement();await Frames(8);
            File.WriteAllText(Path.Combine(folder,"performance.json"),JsonSerializer.Serialize(new {
                frames=times.Count,meanMs=times.Average(),p95Ms=times.Order().ElementAt(171),maxMs=times.Max(),
                primitives=Performance.GetMonitor(Performance.Monitor.RenderTotalPrimitivesInFrame),drawCalls=Performance.GetMonitor(Performance.Monitor.RenderTotalDrawCallsInFrame),
                videoMemory=Performance.GetMonitor(Performance.Monitor.RenderVideoMemUsed),textureMemory=Performance.GetMonitor(Performance.Monitor.RenderTextureMemUsed),
                workingSet=System.Diagnostics.Process.GetCurrentProcess().WorkingSet64,
                note="Wall clock including frame pacing; not target hardware certification."},new JsonSerializerOptions {WriteIndented=true}));
            // Fixed-fps=30 only for explicitly requested video recording.
            if(OS.GetCmdlineUserArgs().Contains("--terrain-video"))
            {
                var frames=Path.Combine(folder,"frames");Directory.CreateDirectory(frames);
                foreach(var (zone,px,pz,yaw) in new[]{("home",-69f,55f,0f),("plaza",-20f,18f,0f),("bank",37f,23f,0f)})
                {
                    await Place(px,pz);_village.ActiveCamera.SetYawImmediate(yaw);_village.ActiveCamera.SnapToTarget();
                    for(var f=0;f<120;f++)
                    {
                        ReleaseMovement();if(f<65)Input.ActionPress(zone=="bank"?"move_up":"move_right");else if(f>=85 && f<105){Input.ActionPress("move_slow");Input.ActionPress("move_left");}
                        await ToSignal(RenderingServer.Singleton,RenderingServer.SignalName.FramePostDraw);
                        using var image=GetViewport().GetTexture().GetImage();Require(image.SaveJpg(Path.Combine(frames,$"{zone}-{f:D4}.jpg"),.92f)==Error.Ok,"video frame");
                    }
                    ReleaseMovement();
                }
            }
        }
        File.WriteAllText(Path.Combine(Output,"captures.json"),JsonSerializer.Serialize(_captures));
    }
}
