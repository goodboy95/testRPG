using Godot;
using System.Text.Json;

namespace TownRpg.Testing;

public partial class VillageV1Probe
{
    private bool ArtBaseline=>OS.GetCmdlineUserArgs().Contains("--village-art-baseline");
    private bool ArtCheck=>ArtBaseline || OS.GetCmdlineUserArgs().Contains("--village-art");
    private string ArtOutput=>ProjectSettings.GlobalizePath($"res://outputs/village-art-v3/{_travelRun}-{(ArtBaseline?"baseline":"sample")}");
    private void ArtContract()
    {
        Require(_village.Exterior.UseArtSample==!ArtBaseline,"art version isolated to explicit preview");
        Require(_village.Exterior.UseTerrainSample && _village.Exterior.Surface.Ready,"adopted Terrain3D retained");
        var meshes=UiUxCapture.Find<MeshInstance3D>(_village.Exterior.ModelRoot).ToArray();
        var special=meshes.Where(m=>m.Name.ToString() is "PlazaTree" or "Tree_037" or "Tree_043").ToArray();
        Require(special.Length==3,"three target trees present");
        if(ArtBaseline) Require(meshes.All(m=>!m.Name.ToString().StartsWith("V3_")),"default v2 has no sample joinery");
        else
        {
            Require(special.Single(m=>m.Name=="Tree_037").Mesh==special.Single(m=>m.Name=="Tree_043").Mesh,"ordinary sample trees share mesh");
            foreach(var tree in special)
            {
                var leaf=Enumerable.Range(0,tree.Mesh.GetSurfaceCount()).Select(tree.GetActiveMaterial).OfType<StandardMaterial3D>().Single(m=>m.ResourceName=="V3_Leaves");
                Require(leaf.Transparency==BaseMaterial3D.TransparencyEnum.AlphaScissor && leaf.CullMode==BaseMaterial3D.CullModeEnum.Disabled,"leaf cutout and double sided "+tree.Name);
            }
            Require(meshes.Count(m=>m.Name.ToString().StartsWith("V3_"))==3,"only three added construction meshes");
        }
    }
    private async Task CaptureArtMatrix()
    {
        foreach(var size in new[]{new Vector2I(1280,720),new Vector2I(1600,900),new Vector2I(1920,1080)})
        {
            DisplayServer.WindowSetMode(DisplayServer.WindowMode.Windowed);DisplayServer.WindowSetFlag(DisplayServer.WindowFlags.Borderless,true);GetWindow().Size=size;
            if(size.X==1920)DisplayServer.WindowSetMode(DisplayServer.WindowMode.Fullscreen);await Frames(16);
            var folder=Path.Combine(Output,size.X.ToString());Directory.CreateDirectory(folder);
            foreach(var (name,x,z,yaw) in new[]{("plaza",-10f,18f,0f),("shop",-3f,7f,0f),("workshop",-23f,-9f,0f),("tree037",-6f,-14f,0f),("tree043",18f,-3f,0f)})
            { await Place(x,z);_village.ActiveCamera.SetYawImmediate(yaw);_village.ActiveCamera.SnapToTarget();_village.ActivePlayer.SetFacingImmediate(_village.ActiveCamera.HorizontalForward);await SaveCapture(folder,name,size); }
            var camera=new Camera3D {Fov=58,Near=.1f,Far=850};_village.Exterior.AddChild(camera);camera.MakeCurrent();
            foreach(var (name,eye,look) in new[]{
                ("tree-under",new Vector3(-10,3.1f,12),new Vector3(-12,7.7f,10)),
                ("tree-profile",new Vector3(-3,5,25),new Vector3(-12,5.8f,10)),
                ("shop-side",new Vector3(5,4.3f,8),new Vector3(-1,4.1f,1)),
                ("shop-canopy",new Vector3(-4,3.5f,3.5f),new Vector3(-3,4.5f,1.5f)),
                ("workshop-side",new Vector3(-12,5.5f,-6),new Vector3(-22,4.8f,-15)),
                ("workbench",new Vector3(-26,4.2f,-11.1f),new Vector3(-25,4,-13.5f))})
            {camera.GlobalPosition=eye;camera.LookAt(look);await SaveCapture(folder,name,size);}
            camera.QueueFree();_village.ActiveCamera.Camera.MakeCurrent();await Frames(3);
            await Place(-10,18);_village.ActiveCamera.SetYawImmediate(0);_village.ActiveCamera.SnapToTarget();
            var timing=new List<double>();
            for(var i=0;i<180;i++)
            {var start=Time.GetTicksUsec();_village.ActiveCamera.SetYawImmediate(i*.25f);await ToSignal(RenderingServer.Singleton,RenderingServer.SignalName.FramePostDraw);timing.Add((Time.GetTicksUsec()-start)/1000d);}
            File.WriteAllText(Path.Combine(folder,"performance.json"),JsonSerializer.Serialize(new {meanMs=timing.Average(),p95Ms=timing.Order().ElementAt(171),workingSet=System.Diagnostics.Process.GetCurrentProcess().WorkingSet64,videoMemory=Performance.GetMonitor(Performance.Monitor.RenderVideoMemUsed),bufferMemory=Performance.GetMonitor(Performance.Monitor.RenderBufferMemUsed),textureMemory=Performance.GetMonitor(Performance.Monitor.RenderTextureMemUsed),drawCalls=Performance.GetMonitor(Performance.Monitor.RenderTotalDrawCallsInFrame),note="Wall clock including frame pacing. Godot video-memory counters are engine estimates, not whole-card VRAM. Not target hardware certification."},new JsonSerializerOptions{WriteIndented=true}));
            if(OS.GetCmdlineUserArgs().Contains("--art-video"))
            {
                var output=Path.Combine(folder,"frames");Directory.CreateDirectory(output);
                foreach(var (name,x,z) in new[]{("plaza",-8f,19f),("shop",2f,7f),("workshop",-19f,-9f)})
                {
                    var foliageOnly=OS.GetCmdlineUserArgs().Contains("--art-leaf-video");
                    if(foliageOnly && name!="plaza")continue;
                    await Place(x,z);_village.ActiveCamera.SetYawImmediate(0);_village.ActiveCamera.SnapToTarget();
                    Camera3D? foliageCamera=null;
                    if(foliageOnly){foliageCamera=new Camera3D{Fov=58,Near=.1f,Far=850};_village.Exterior.AddChild(foliageCamera);foliageCamera.MakeCurrent();}
                    for(var frame=0;frame<120;frame++)
                    {
                        ReleaseMovement();if(frame<50){Input.ActionPress("move_slow");Input.ActionPress("move_left");}
                        _village.ActiveCamera.SetYawImmediate(frame<65?0:(frame-65)*.7f);
                        if(foliageCamera is not null)
                        {var target=new Vector3(-12,5.8f,10);foliageCamera.GlobalPosition=target+new Vector3(9,-.8f,15).Rotated(Vector3.Up,Mathf.DegToRad(frame*.22f));foliageCamera.LookAt(target);}
                        await ToSignal(RenderingServer.Singleton,RenderingServer.SignalName.FramePostDraw);
                        using var image=GetViewport().GetTexture().GetImage();Require(image.SaveJpg(Path.Combine(output,$"{name}-{frame:D4}.jpg"),.9f)==Error.Ok,"motion frame");
                    }
                    ReleaseMovement();
                    if(foliageCamera is not null){foliageCamera.QueueFree();_village.ActiveCamera.Camera.MakeCurrent();await Frames(3);}
                }
            }
        }
        File.WriteAllText(Path.Combine(Output,"captures.json"),JsonSerializer.Serialize(_captures));
    }
}
