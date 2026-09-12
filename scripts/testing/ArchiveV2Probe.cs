using Godot;
using System.Text.Json;
using TownRpg.Application;
using TownRpg.Core.Domain;
using TownRpg.Presentation;

namespace TownRpg.Testing;

// Layout/physics acceptance only. The separate full prologue route uses real UI and input.
public partial class ArchiveV2Probe : Node
{
    private DreamArchiveRoomController3D _room = null!;
    private bool Gray => OS.GetCmdlineUserArgs().Contains("--graybox");
    private bool Behavior => OS.GetCmdlineUserArgs().Contains("--behavior-only");
    private string Output => ProjectSettings.GlobalizePath($"res://outputs/late-rooms-v2/archive/{(Gray ? "graybox" : "final")}");
    private static void Require(bool ok,string message) { if(!ok)throw new InvalidOperationException(message); }
    public override void _Ready() => CallDeferred(MethodName.Run);
    private async void Run()
    {
        try
        {
            Directory.CreateDirectory(Output);
            var session=GetNode<GameSession>("/root/GameSession");using var suppress=session.SuppressPersistence();
            session.NewGame("档案室验收","同学");var dream=session.State.DreamEscape!;dream.XiaoyuJoined=true;
            dream.UnlockedAreas.Add(DreamEscapeIds.AreaArchive);
            var entry=ArchiveRoomLayout3D.Mapper.ToStored(ArchiveRoomLayout3D.Entry);
            session.MoveDreamToArea(DreamEscapeIds.AreaArchive,entry.X,entry.Y);
            var start=Time.GetTicksUsec();
            var packed=ResourceLoader.Load<PackedScene>("res://scenes/dream/DreamArchiveRoom.tscn")!;
            _room=packed.Instantiate<DreamArchiveRoomController3D>();AddChild(_room);await Frames(5);
            var loadMs=(Time.GetTicksUsec()-start)/1000d;
            Require(_room.CurrentInteractionTargetId is null,"Entry automatically focuses an interaction.");
            _room.SetProcess(false);_room.PlayerActor.SetPhysicsProcess(false);_room.Companion.SetProcess(false);_room.CameraController.SetInputActive(false);
            var visual=_room.ArchiveVisual;var model=_room.GetNode<Node3D>("ArchiveModel");
            Require(_room.InteractionMarkerCount==13 && visual.CollisionBodyCount==20,"Archive composition mismatch.");
            var env=_room.GetNode<WorldEnvironment>("WorldEnvironment").Environment;
            Require(env.AmbientLightSource==Godot.Environment.AmbientSource.Color && env.AmbientLightSkyContribution==0,"Archive ambient must use Color only.");
            foreach(var spec in ArchiveRoomLayout3D.Collisions.Where(s=>s.Furniture))
            {
                var mesh=model.FindChild(spec.Name,true,false) as MeshInstance3D;
                Require(mesh is not null,"Missing furniture mesh: "+spec.Name);
                var bounds=mesh!.GlobalTransform*mesh.GetAabb();
                Require(bounds.Position.X>=spec.Position.X-spec.Size.X/2-.04f && bounds.End.X<=spec.Position.X+spec.Size.X/2+.04f &&
                    bounds.Position.Z>=spec.Position.Z-spec.Size.Z/2-.04f && bounds.End.Z<=spec.Position.Z+spec.Size.Z/2+.04f,"Mesh escapes collision: "+spec.Name+" "+bounds);
            }
            for(var x=-1.06f;x<=1.06f;x+=.15f)
            for(var z=-3.8f;z<=4f;z+=.15f)Require(!visual.IsBlocked(new(x,0,z)),"Main 2.8m aisle narrowed.");
            var targets=_room.GetNode<Node3D>("InteractionMarkers").GetChildren().OfType<InteractionMarker3D>().ToArray();
            _room.PlayerActor.GlobalPosition=ArchiveRoomLayout3D.Entry;
            var traversals=0;
            foreach(var marker in targets)
            {
                Require(!visual.IsBlocked(marker.Position),"Obstructed marker: "+marker.TargetId);
                await WalkCapsule(marker.Position);traversals++;
                if(marker.TargetId=="observe:target.archive.fuse_route") {dream.SolvedPuzzles.Add(DreamEscapeIds.PuzzleArchiveRetrieval);_room.RefreshState();}
                _room._Process(0);_room.CameraController.SetInputActive(false);
                if(_room.ActiveInteractionIds.Contains(marker.TargetId))Require(_room.CurrentInteractionTargetId==marker.TargetId,"Focus mismatch: "+marker.TargetId+" / "+_room.CurrentInteractionTargetId);
            }
            await WalkCapsule(ArchiveRoomLayout3D.Entry);traversals++;
            // Furniture and exit stop the actual player capsule, not just the layout predicate.
            foreach(var spec in ArchiveRoomLayout3D.Collisions.Where(s=>s.Furniture))
            {
                var center=new Vector3(spec.Position.X,0,spec.Position.Z);
                var from=ArchiveRoomLayout3D.ResolveSpawn(center);
                _room.PlayerActor.GlobalPosition=from;await Physics();
                Require(_room.PlayerActor.TestMove(_room.PlayerActor.GlobalTransform,center-from),"Furniture has no capsule collision: "+spec.Name);
            }
            _room.PlayerActor.GlobalPosition=new(6.1f,0,4.2f);await Physics();
            Require(_room.PlayerActor.TestMove(_room.PlayerActor.GlobalTransform,new(1.5f,0,0)),"Door barrier missing.");
            await _room.PlayExitTransitionAsync("area:dream.corridor",CancellationToken.None);
            Require(Mathf.Abs(visual.DoorHinge.Rotation.Y)>.8f,"Exit leaf did not animate.");_room.ResetExitTransition();
            Require(Mathf.Abs(visual.DoorHinge.Rotation.Y)<.001f,"Exit leaf did not reset.");
            _room.PlayerActor.GlobalPosition=new(-5.4f,0,0);
            _room.CameraController.SetYawImmediate(-90);_room.CameraController.SnapToTarget();await Frames(12);
            Require(_room.CameraController.SpringArm.GetHitLength()<1.1f,"Camera did not shorten against archive shelves.");
            var restores=0;
            foreach(var requested in ArchiveRoomLayout3D.Collisions.Where(s=>s.Furniture).Select(s=>new Vector3(s.Position.X,0,s.Position.Z)).Concat([ArchiveRoomLayout3D.Entry,new(100,0,100)]))
            {
                var stored=ArchiveRoomLayout3D.Mapper.ToStored(requested);session.MoveDreamToArea(DreamEscapeIds.AreaArchive,stored.X,stored.Y);
                var restored=packed.Instantiate<DreamArchiveRoomController3D>();AddChild(restored);await Frames(1);
                Require(!restored.ArchiveVisual.IsBlocked(restored.PlayerActor.GlobalPosition),"Unsafe saved-position restore.");
                Require(restored.PlayerActor.GlobalPosition.DistanceTo(ArchiveRoomLayout3D.Mapper.ToWorld(new(dream.PositionX,dream.PositionY)))<.001f,"Corrected position not recorded.");
                if(requested==ArchiveRoomLayout3D.Entry)Require(restored.PlayerActor.GlobalPosition.DistanceTo(requested)<.001f,"Valid position was moved.");
                restored.QueueFree();await Frames(1);restores++;
            }
            _room.PlayerActor.GlobalPosition=ArchiveRoomLayout3D.Entry;
            _room.Companion.GlobalPosition=new(-5.4f,0,-3.8f);
            for(var tick=0;tick<700;tick++)
            {
                var before=_room.Companion.GlobalPosition;_room.Companion._Process(.025);
                Require(!visual.IsBlocked(_room.Companion.GlobalPosition) && before.DistanceTo(_room.Companion.GlobalPosition)<=.086f,"Companion clipped or teleported.");
            }
            Require(_room.Companion.GlobalPosition.DistanceTo(_room.PlayerActor.GlobalPosition)<1.6f,"Companion failed to follow around cabinets.");
            using(session.Modal.Acquire("archive-probe-modal"))
            {
                var before=_room.Companion.GlobalPosition;_room.PlayerActor.GlobalPosition=new(1,0,-3);
                for(var tick=0;tick<20;tick++)_room.Companion._Process(.025);
                Require(before.DistanceTo(_room.Companion.GlobalPosition)<.001f,"Modal did not pause following.");
            }
            dream.SolvedPuzzles.Clear();dream.InstalledParts.Clear();_room.RefreshState();
            Require(!visual.RegistrySolved && !visual.FuseRepaired && !visual.RecorderRunning && !visual.CounterOpen && visual.IndexStripVisible,"Initial physical state mismatch.");
            _room.ApplyTransientState(new(false,DreamEscapeIds.PuzzleArchiveRetrieval,["choir","broadcast"]));Require(visual.RegistryPreviewCount==2,"Preview cards missing.");
            _room.ApplyTransientState(new(false,null,null));Require(visual.RegistryPreviewCount==0,"Preview survived close.");
            var measurements=new List<object>();
            if(DisplayServer.GetName()!="headless" && !Behavior)
            {
                foreach(var size in Gray ? new[]{new Vector2I(1280,720)} : new[]{new Vector2I(1280,720),new Vector2I(1600,900),new Vector2I(1920,1080)})
                {
                    DisplayServer.WindowSetMode(DisplayServer.WindowMode.Windowed);DisplayServer.WindowSetFlag(DisplayServer.WindowFlags.Borderless,true);DisplayServer.WindowSetSize(size);await Frames(20);
                    var directory=Path.Combine(Output,$"{size.X}x{size.Y}");Directory.CreateDirectory(directory);
                    dream.SolvedPuzzles.Clear();dream.InstalledParts.Clear();_room.RefreshState();
                    await Shot(directory,"01-entry.png",ArchiveRoomLayout3D.Entry,60);
                    await Shot(directory,"02-central-aisle.png",new(0,0,3.8f),0);
                    await Shot(directory,"03-archive-stacks.png",new(-4.8f,0,2.6f),15);
                    await Shot(directory,"04-retrieval-before.png",new(-2.5f,0,1.5f),0);
                    await Shot(directory,"05-repair-before.png",new(4.1f,0,-2.4f),-90);
                    await Shot(directory,"06-recorder-before.png",new(4.1f,0,1),-90);
                    await Shot(directory,"07-timeline-before.png",new(0,0,4.3f),180);
                    await Shot(directory,"08-index-strip.png",new(-3.1f,0,4.3f),0);
                    await Shot(directory,"09-window-and-exit.png",new(3.4f,0,3),-140);
                    dream.SolvedPuzzles.Add(DreamEscapeIds.PuzzleArchiveRetrieval);_room.RefreshState();
                    await Shot(directory,"10-open-drawer.png",new(-2.5f,0,-1.6f),0);
                    dream.SolvedPuzzles.Add(DreamEscapeIds.ProgressFuseAssembly);dream.InstalledParts.Add(DreamEscapeIds.SlotArchiveRecorderReel);_room.RefreshState();
                    Require(visual.FuseRepaired && !visual.RecorderRunning,"Partial installation misrepresented.");
                    await Shot(directory,"11-partial-recorder.png",new(4.1f,0,1),-90);
                    dream.SolvedPuzzles.UnionWith([DreamEscapeIds.PuzzleArchiveRecorderRebuild,DreamEscapeIds.PuzzleArchiveWorkTimeline,DreamEscapeIds.PickupArchiveIndexStrip]);_room.RefreshState();
                    Require(visual.RecorderRunning && visual.CounterOpen && !visual.IndexStripVisible,"Solved physical state mismatch.");
                    await Shot(directory,"12-recorder-running.png",new(4.1f,0,1),-90);
                    await Shot(directory,"13-timeline-sorted.png",new(0,0,4.3f),180);
                    await Shot(directory,"14-index-picked.png",new(-3.1f,0,4.3f),0);
                    _room.PlayerActor.Visible=false;_room.Companion.Visible=false;
                    await Shot(directory,"15-recorder-detail.png",new(4.5f,0,1),-90);
                    await Shot(directory,"16-supported-paper.png",new(2.7f,0,-4.3f),0);
                    _room.PlayerActor.Visible=true;_room.Companion.Visible=true;
                    visual.SetCollisionDebug(true);await Shot(directory,"17-collision-route.png",ArchiveRoomLayout3D.Entry,60);visual.SetCollisionDebug(false);
                    _room.PlayerActor.GlobalPosition=ArchiveRoomLayout3D.Entry;await Frames(20);
                    var times=new List<double>();
                    for(var frame=0;frame<180;frame++) {_room.CameraController.SetYawImmediate(frame*2);var before=Time.GetTicksUsec();await Frames(1);times.Add((Time.GetTicksUsec()-before)/1000d);}
                    var ordered=times.Order().ToArray();measurements.Add(new {width=size.X,height=size.Y,averageMs=times.Average(),p95Ms=ordered[(int)(ordered.Length*.95)],maxMs=times.Max()});
                }
            }
            File.WriteAllText(Path.Combine(Output,Behavior ? "behavior.json" : "metrics.json"),JsonSerializer.Serialize(new{engine=Engine.GetVersionInfo()["string"].AsString(),adapter=RenderingServer.GetVideoAdapterName(),loadMs,markers=13,traversals,restores,collisions=20,graybox=Gray,measurements,limitations="Synthetic capsule and input-independent scene checks. Separate full input route and human/device acceptance required."},new JsonSerializerOptions{WriteIndented=true}));
            GD.Print($"ARCHIVE_V2_PROBE_PASS markers=13 capsuleRoutes={traversals} restores={restores} graybox={Gray}");GetTree().Quit(0);
        }
        catch(Exception ex) {GD.PushError("ARCHIVE_V2_PROBE_FAIL: "+ex);GetTree().Quit(1);}
    }
    private async Task WalkCapsule(Vector3 destination)
    {
        var player=_room.PlayerActor;
        var navigator=new GroundWaypointNavigator3D(ArchiveRoomLayout3D.Waypoints(),ArchiveRoomLayout3D.IsBlocked,ArchiveRoomLayout3D.ResolveSpawn);
        for(var tick=0;tick<2500 && player.GlobalPosition.DistanceTo(destination)>.035f;tick++)
        {
            var before=player.GlobalPosition;var next=navigator.Advance(before,destination,.1f);
            Require(before.DistanceTo(next)<=.101f,"Route teleported.");
            player.MoveAndCollide(next-before);
            Require(player.GlobalPosition.DistanceTo(next)<.008f,"Capsule route hit furniture: "+before+" -> "+next);
            if(tick%30==0)await Physics();
        }
        Require(player.GlobalPosition.DistanceTo(destination)<.04f,"Capsule could not reach "+destination);
    }
    private async Task Shot(string directory,string name,Vector3 p,float yaw)
    {
        Require(!ArchiveRoomLayout3D.IsBlocked(p),"Unsafe screenshot pose: "+name);
        _room.PlayerActor.GlobalPosition=p;_room._Process(0);_room.CameraController.SetInputActive(false);_room.CameraController.SetYawImmediate(yaw);_room.CameraController.SnapToTarget();_room.PlayerActor.SetFacingImmediate(_room.CameraController.HorizontalForward);await Frames(8);
        await ToSignal(RenderingServer.Singleton,RenderingServer.SignalName.FramePostDraw);
        using var image=GetViewport().GetTexture().GetImage();Require(image.GetSize()==DisplayServer.WindowGetSize(),"Screenshot size mismatch.");Require(image.SavePng(Path.Combine(directory,name))==Error.Ok,"Screenshot failed.");
    }
    private async Task Frames(int n) {for(var i=0;i<n;i++)await ToSignal(GetTree(),SceneTree.SignalName.ProcessFrame);}
    private async Task Physics() => await ToSignal(GetTree(),SceneTree.SignalName.PhysicsFrame);
}
