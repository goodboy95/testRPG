using Godot;
using System.Text.Json;
using TownRpg.Presentation;
using TownRpg.Application;
using TownRpg.Core.Domain;
using TownRpg.Core.Services;

namespace TownRpg.Testing;

/// <summary>Real rendering metrics, plus streaming/restore checks. Never accesses the real save for writing.</summary>
public partial class VillageStreamingProbe : Node
{
    private VillagePreviewController _host=null!;
    private string _output="";
    private readonly List<object> _samples=[];
    private readonly List<string> _checks=[];
    private readonly List<object> _frames=[];
    private ulong _lastFrame;
    private Vector2I _renderSize;
    private string _phase="load";
    public override void _Process(double delta)
    {
        var now=Time.GetTicksUsec();
        if(_lastFrame!=0 && _host is not null && IsInstanceValid(_host))
        {
            var s=_host.Exterior.Streaming;
            _frames.Add(new{phase=_phase,interior=_host.IsInterior,width=_renderSize.X,height=_renderSize.Y,frameMs=(now-_lastFrame)/1000d,gpuMs=s?.GpuMs,controllerMs=s?.ControllerMs,
                rebuilding=s?.ProxyRebuilds,pending=s?.PendingLoads,primitives=Performance.GetMonitor(Performance.Monitor.RenderTotalPrimitivesInFrame)});
        }
        _lastFrame=now;
    }
    private readonly bool _baseline=OS.GetCmdlineUserArgs().Contains("--v7-baseline");
    private readonly bool _anime=OS.GetCmdlineUserArgs().Contains("--village-v8");
    public override void _Ready()=>CallDeferred(MethodName.Run);
    private void Require(bool condition,string message)
    {if(!condition)throw new InvalidOperationException(message);_checks.Add(message);}
    private async Task Frames(int count)
    {for(var i=0;i<count;i++)await ToSignal(GetTree(),SceneTree.SignalName.ProcessFrame);}
    private async Task Settle()
    {
        _phase="stream";
        var stream=_host.Exterior.Streaming;
        if(stream is null){await Frames(6);return;}
        await Frames(5);
        for(var i=0;i<1200 && stream.PendingLoads>0;i++)await Frames(1);
        Require(stream.PendingLoads==0,"stream requests finish");Require(stream.LoadFailures==0,"no stream failures");await Frames(5);_phase="settled";
    }
    private async Task Place(float x,float z,float yaw=13)
    {
        _host.ActivePlayer.GlobalPosition=_host.Exterior.SurfacePoint(x,z,.04f);_host.ActivePlayer.Velocity=Vector3.Zero;
        _host.ActiveCamera.SetYawImmediate(yaw);_host.ActiveCamera.SnapToTarget();await Frames(2);
    }
    private async Task SaveImage(string name)
    {
        var previous=_phase;_phase="capture";
        await ToSignal(RenderingServer.Singleton,RenderingServer.SignalName.FramePostDraw);
        using var image=GetViewport().GetTexture().GetImage();image.SavePng(Path.Combine(_output,name+".png"));
        await Frames(2);_phase=previous;
    }
    private async Task Sample(string point,int yaw,Vector2I size,string phase)
    {
        _phase=phase;
        _host.ActiveCamera.SetYawImmediate(yaw);_host.ActiveCamera.SnapToTarget();await Frames(4);
        var start=Time.GetTicksUsec();await ToSignal(RenderingServer.Singleton,RenderingServer.SignalName.FramePostDraw);
        var frameMs=(Time.GetTicksUsec()-start)/1000d;
        var rid=GetViewport().GetViewportRid();var gpu=RenderingServer.ViewportGetMeasuredRenderTimeGpu(rid);
        var s=_host.Exterior.Streaming;
        if(s is not null)
        {
            Require(s.Selection.Fits,"scenery budget "+point+" "+yaw);
            var limit=s.BudgetTier=="near"?s.Profile.FrameNear:s.Profile.FrameWide;
            Require(Performance.GetMonitor(Performance.Monitor.RenderTotalPrimitivesInFrame)<=limit.Triangles,"whole-frame triangle budget "+point+" "+yaw);
            if(s.Profile.EnforceFrameDrawTarget)
                Require(Performance.GetMonitor(Performance.Monitor.RenderTotalDrawCallsInFrame)<=limit.Batches,"whole-frame draw budget "+point+" "+yaw);
        }
        _samples.Add(new {point,yaw,width=size.X,height=size.Y,phase,waitForPostDrawMs=frameMs,
            primitives=Performance.GetMonitor(Performance.Monitor.RenderTotalPrimitivesInFrame),
            drawCalls=Performance.GetMonitor(Performance.Monitor.RenderTotalDrawCallsInFrame),
            objects=Performance.GetMonitor(Performance.Monitor.RenderTotalObjectsInFrame),
            gpuMs=gpu>0?(double?)gpu:null,renderCpuMs=RenderingServer.ViewportGetMeasuredRenderTimeCpu(rid),
            processMs=Performance.GetMonitor(Performance.Monitor.TimeProcess)*1000,
            videoMemory=Performance.GetMonitor(Performance.Monitor.RenderVideoMemUsed),stream=s?.Snapshot(),npc=(_host as VillageNpcPreviewController)?.Snapshot()});
    }
    private async void Run()
    {
        try
        {
            _output=ProjectSettings.GlobalizePath("res://outputs/village-"+(_anime?"v8":"v7")+"/"+DateTime.Now.ToString("yyyyMMdd-HHmmss")+(_baseline?"-baseline":"-probe"));Directory.CreateDirectory(_output);
            var session=GetNode<GameSession>("/root/GameSession");var before=SaveGameSerializer.Serialize(session.State);
            var savePath=ProjectSettings.GlobalizePath(GameSession.SavePath);var bytes=File.Exists(savePath)?File.ReadAllBytes(savePath):null;
            var loadStart=Time.GetTicksUsec();
            var withNpcs=OS.GetCmdlineUserArgs().Contains("--v7-npc");
            _host=ResourceLoader.Load<PackedScene>(_baseline?"res://scenes/testing/VillagePreview.tscn":withNpcs?"res://scenes/testing/VillageNpcPreview.tscn":"res://scenes/testing/VillageStreamingPreview.tscn").Instantiate<VillagePreviewController>();AddChild(_host);
            if (_host is VillageNpcPreviewController npcHost)
            {
                for(var i=0;i<600 && !npcHost.ReadyForInteraction && npcHost.LastError is null;i++)await Frames(1);
                Require(npcHost.ReadyForInteraction && npcHost.LastError is null,"NPC pilot initialization");
            }
            _host.SetHostLocked(true);await Settle();var loadMs=(Time.GetTicksUsec()-loadStart)/1000d;
            RenderingServer.ViewportSetMeasureRenderTime(GetViewport().GetViewportRid(),true);
            Require(_host.Exterior.UseStreaming!=_baseline,"version selection");Require(_host.Exterior.Layout.Geometry.Buildings.Length==24,"24 building collisions");
            Require(_host.Exterior.Surface.Ready,"repaired terrain collision ready");
            var quick=OS.GetCmdlineUserArgs().Contains("--v7-quick");
            var points=new List<(string,float,float)> {("home",-56,55),("plaza",-22,27),("community",-48,24),("west",-71,-10),("east",81,51),("station",-29,-35),("north-bridge",41,-28),("south-bridge",41,28),("exit",-29,-75)};
            if(!quick)
                for(var x=-90;x<=90;x+=30)for(var z=-90;z<=90;z+=30)
                    if(_host.SafePose(_host.Exterior.SurfacePoint(x,z,.04f)))points.Add(($"grid-{x}-{z}",x,z));
            foreach(var size in quick?new[]{new Vector2I(1280,720)}:new[]{new Vector2I(1280,720),new Vector2I(1600,900),new Vector2I(1920,1080)})
            {
                DisplayServer.WindowSetMode(DisplayServer.WindowMode.Windowed);DisplayServer.WindowSetFlag(DisplayServer.WindowFlags.Borderless,true);
                GetWindow().Size=size;if(size.X==1920)DisplayServer.WindowSetMode(DisplayServer.WindowMode.Fullscreen);await Frames(8);
                using(var pixels=GetViewport().GetTexture().GetImage())Require(pixels.GetSize()==size,"actual render resolution "+size+" got "+pixels.GetSize());
                _renderSize=size;
                foreach(var (name,x,z) in points)
                {
                    await Place(x,z);await Settle();
                    foreach(var yaw in new[]{13,58,103,148,193,238,283,328})await Sample(name,yaw,size,"settled");
                    if(!name.StartsWith("grid-"))await SaveImage($"{size.X}-{name}");
                }
            }
            if(!_baseline)
            {
                await Place(-56,55);await Settle();var snapshot=_host.Capture();
                var unloadsBefore=_host.Exterior.Streaming!.UnloadedDistricts;
                await Place(105,95);await Settle();var tick=Time.GetTicksMsec();
                while(Time.GetTicksMsec()-tick<4800)await Frames(1);
                Require(_host.Exterior.Streaming!.LoadedDistricts<7,"distant detail districts unloaded");
                Require(_host.Exterior.Streaming.UnloadedDistricts>unloadsBefore,"actual unload event after leaving the home districts");
                await _host.RestoreAsync(VillageExplorationState.VillageLocation,snapshot);_host.SetHostLocked(true);await Settle();
                Require(_host.ActivePlayer.GlobalPosition.DistanceTo(new(snapshot.X,snapshot.Y,snapshot.Z))<.2f,"v6-compatible outdoor snapshot restored");
                _phase="transition";
                var home=new VillageExplorationState {X=-3.1f,Y=0,Z=2.8f,CameraYaw=19,WakeIntroductionComplete=true,Doors=new(){{"bedroom",true}},DoorOpenSides=new(){{"bedroom",-1}}};
                await _host.RestoreAsync(VillageExplorationState.HomeLocation,home);await Frames(60);
                Require(_host.IsInterior && _host.Interior!.DoorOpen("bedroom"),"home door restored");
                var fixture=_host.Capture();_host.SetHostLocked(false);Require(await _host.SwitchArea(false),"leave home for streaming re-entry");await Settle();_phase="transition";await _host.RestoreAsync(VillageExplorationState.HomeLocation,fixture);await Frames(60);
                Require(_host.Capture().DoorOpenSides["bedroom"]==fixture.DoorOpenSides["bedroom"],"cached door direction retained across stream re-entry");
                _host.SetHostLocked(false);Require(await _host.SwitchArea(false),"leave restored home");await Settle();
                // Continuous physical input and fast orbit exercise update/stream boundaries.
                await Place(-56,55,0);_host.SetHostLocked(false);_phase="moving-orbit";
                for(var i=0;i<240;i++)
                {
                    if(i==0)Input.ActionPress("move_down");if(i==120){Input.ActionRelease("move_down");Input.ActionPress("move_up");}
                    _host.ActiveCamera.SetYawImmediate(i*7+13);await Frames(1);
                }
                Input.ActionRelease("move_up");await Frames(12);
                var actual=_host.ActivePlayer.GlobalPosition;
                File.WriteAllText(Path.Combine(_output,"movement-end.json"),JsonSerializer.Serialize(new{x=actual.X,y=actual.Y,z=actual.Z,ground=_host.Exterior.WalkHeight(actual.X,actual.Z),onFloor=_host.ActivePlayer.IsOnFloor()}));
                _host.SetHostLocked(true);Require(_host.SafePose(actual),"movement and rotating camera retain valid collision pose "+actual);
            }
            Require(SaveGameSerializer.Serialize(session.State)==before,"formal state unchanged");
            Require(bytes is null?!File.Exists(savePath):bytes.SequenceEqual(File.ReadAllBytes(savePath)),"formal save unchanged");
            File.WriteAllText(Path.Combine(_output,"results.json"),JsonSerializer.Serialize(new{baseline=_baseline,budgetProfile=_host.Exterior.Streaming?.Profile.Id??"v6-baseline",loadMs,adapter=RenderingServer.GetVideoAdapterName(),renderer=RenderingServer.GetCurrentRenderingMethod(),samples=_samples,frames=_frames,checks=_checks,humanAcceptance="pending",targetHardwareAcceptance="pending"},new JsonSerializerOptions{WriteIndented=true}));
            GD.Print($"VILLAGE_V7_PROBE_PASS samples={_samples.Count} checks={_checks.Count} output={_output}");
            _host.QueueFree();await Frames(3);GetTree().Quit();
        }
        catch(Exception e)
        {
            GD.PushError(e.ToString());
            try {if(_output!="")File.WriteAllText(Path.Combine(_output,"failure.json"),JsonSerializer.Serialize(new{error=e.ToString(),samples=_samples,checks=_checks},new JsonSerializerOptions{WriteIndented=true}));}
            finally {GetTree().Quit(1);}
        }
    }
}
