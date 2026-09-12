using Godot;
using System.Text.Json;
using TownRpg.Application;
using TownRpg.Core.Domain;
using TownRpg.Presentation;

namespace TownRpg.Testing;

// Physics/art acceptance. State setup here is explicitly not the full input playthrough.
public partial class AuditoriumV2Probe : Node
{
    private DreamAuditoriumController3D _room=null!;
    private GroundWaypointNavigator3D _navigator=null!;
    private bool Gray=>OS.GetCmdlineUserArgs().Contains("--graybox");
    private bool Behavior=>OS.GetCmdlineUserArgs().Contains("--behavior-only");
    private string Output=>ProjectSettings.GlobalizePath($"res://outputs/late-rooms-v2/auditorium/{(Gray?"graybox":"final")}");
    private static void Require(bool ok,string message) {if(!ok)throw new InvalidOperationException(message);}
    public override void _Ready()=>CallDeferred(MethodName.Run);
    private async void Run()
    {
        try
        {
            Directory.CreateDirectory(Output);var session=GetNode<GameSession>("/root/GameSession");using var suppress=session.SuppressPersistence();
            session.NewGame("礼堂验收","同学");var dream=session.State.DreamEscape!;dream.XiaoyuJoined=true;dream.UnlockedAreas.Add(DreamEscapeIds.AreaAuditorium);
            var entry=AuditoriumLayout3D.Mapper.ToStored(AuditoriumLayout3D.Entry);session.MoveDreamToArea(DreamEscapeIds.AreaAuditorium,entry.X,entry.Y);
            var start=Time.GetTicksUsec();var packed=ResourceLoader.Load<PackedScene>("res://scenes/dream/DreamAuditorium.tscn")!;
            _room=packed.Instantiate<DreamAuditoriumController3D>();AddChild(_room);await Frames(5);var loadMs=(Time.GetTicksUsec()-start)/1000d;
            Require(_room.CurrentInteractionTargetId is null,"Entry automatically focuses an interaction.");
            _room.SetProcess(false);_room.PlayerActor.SetPhysicsProcess(false);_room.Companion.SetProcess(false);_room.CameraController.SetInputActive(false);
            var visual=_room.AuditoriumVisual;var model=_room.GetNode<Node3D>("AuditoriumModel");
            Require(_room.InteractionMarkerCount==15 && visual.CollisionBodyCount==56,"Auditorium composition mismatch.");
            var env=_room.GetNode<WorldEnvironment>("WorldEnvironment").Environment;
            Require(env.AmbientLightSource==Godot.Environment.AmbientSource.Color && env.AmbientLightSkyContribution==0,"Ambient must use Color only.");
            foreach(var spec in AuditoriumLayout3D.Collisions.Where(s=>s.Furniture))
            {
                var mesh=model.FindChild(spec.Name,true,false) as MeshInstance3D;Require(mesh is not null,"Missing furniture mesh: "+spec.Name);
                var b=mesh!.GlobalTransform*mesh.GetAabb();
                Require(b.Position.X>=spec.Position.X-spec.Size.X/2-.045f && b.End.X<=spec.Position.X+spec.Size.X/2+.045f && b.Position.Z>=spec.Position.Z-spec.Size.Z/2-.045f && b.End.Z<=spec.Position.Z+spec.Size.Z/2+.045f,"Mesh escapes collider: "+spec.Name+" "+b);
            }
            var seats=Enumerable.Range(1,32).Select(i=>(MeshInstance3D)model.FindChild($"Seat{i:00}",true,false)).ToArray();
            Require(seats.Select(s=>s.Mesh.GetInstanceId()).Distinct().Count()==1,"32 seats do not share one mesh.");
            for(var x=-1.35f;x<=1.35f;x+=.15f)
            for(var z=-2.7f;z<=6.4f;z+=.15f)Require(!visual.IsBlocked(new(x,0,z)),"Central aisle narrowed.");
            Require(visual.IsBlocked(new(0,0,-5)),"Raised stage is walkable.");
            _navigator=new GroundWaypointNavigator3D(AuditoriumLayout3D.Waypoints(),AuditoriumLayout3D.IsBlocked,AuditoriumLayout3D.ResolveSpawn);
            _room.PlayerActor.GlobalPosition=AuditoriumLayout3D.Entry;var traversals=0;
            foreach(var marker in _room.GetNode<Node3D>("InteractionMarkers").GetChildren().OfType<InteractionMarker3D>())
            {
                Require(marker.Position.Y==0 && !visual.IsBlocked(marker.Position),"Obstructed/elevated target: "+marker.TargetId);
                await WalkCapsule(marker.Position);traversals++;
                if(marker.TargetId=="observe:target.auditorium.tape_order") {dream.SolvedPuzzles.Add(DreamEscapeIds.PuzzleAuditoriumStageAlignment);_room.RefreshState();}
                _room._Process(0);_room.CameraController.SetInputActive(false);
                if(_room.ActiveInteractionIds.Contains(marker.TargetId))Require(_room.CurrentInteractionTargetId==marker.TargetId,"Focus mismatch: "+marker.TargetId+" / "+_room.CurrentInteractionTargetId);
            }
            await WalkCapsule(AuditoriumLayout3D.Entry);traversals++;
            foreach(var spec in AuditoriumLayout3D.Collisions.Where(s=>s.Furniture))
            {
                var center=new Vector3(spec.Position.X,0,spec.Position.Z);var from=AuditoriumLayout3D.ResolveSpawn(center);_room.PlayerActor.GlobalPosition=from;await Physics();
                Require(_room.PlayerActor.TestMove(_room.PlayerActor.GlobalTransform,center-from),"No physical furniture collision: "+spec.Name);
            }
            _room.PlayerActor.GlobalPosition=new(0,0,7.05f);await Physics();Require(_room.PlayerActor.TestMove(_room.PlayerActor.GlobalTransform,new(0,0,1.5f)),"Entrance barrier missing.");
            await _room.PlayExitTransitionAsync("area:dream.corridor",CancellationToken.None);Require(Mathf.Abs(visual.DoorHinge.Rotation.Y)>.8f,"Entrance leaf did not animate.");_room.ResetExitTransition();Require(Mathf.Abs(visual.DoorHinge.Rotation.Y)<.001f,"Entrance leaf did not reset.");
            _room.PlayerActor.GlobalPosition=new(-8.1f,0,-4.8f);_room.CameraController.SetYawImmediate(-90);_room.CameraController.SnapToTarget();await Frames(12);
            Require(_room.CameraController.SpringArm.GetHitLength()<1.3f,"Camera did not shorten against projector.");
            var restores=0;
            foreach(var requested in AuditoriumLayout3D.Collisions.Where(s=>s.Furniture).Select(s=>new Vector3(s.Position.X,0,s.Position.Z)).Concat([AuditoriumLayout3D.Entry,new(100,0,100)]))
            {
                var stored=AuditoriumLayout3D.Mapper.ToStored(requested);session.MoveDreamToArea(DreamEscapeIds.AreaAuditorium,stored.X,stored.Y);
                var restored=packed.Instantiate<DreamAuditoriumController3D>();AddChild(restored);await Frames(1);
                Require(!restored.AuditoriumVisual.IsBlocked(restored.PlayerActor.GlobalPosition),"Unsafe saved-position restore.");
                Require(restored.PlayerActor.GlobalPosition.DistanceTo(AuditoriumLayout3D.Mapper.ToWorld(new(dream.PositionX,dream.PositionY)))<.001f,"Corrected restore not recorded.");
                Require(!restored.AuditoriumVisual.ExitLeverHeld,"Transient lever survived scene reload.");
                if(requested==AuditoriumLayout3D.Entry)Require(restored.PlayerActor.GlobalPosition.DistanceTo(requested)<.001f,"Valid position moved.");
                restored.QueueFree();await Frames(1);restores++;if(restores%8==0){GC.Collect();GC.WaitForPendingFinalizers();}
            }
            _room.PlayerActor.GlobalPosition=AuditoriumLayout3D.Entry;_room.Companion.GlobalPosition=new(6,0,4.8f);
            _room.ApplyTransientState(new(false,DreamEscapeIds.PuzzleAuditoriumStageAlignment,["1","1","1","1","window","window","window"]));
            using(session.Modal.Acquire("auditorium-side-probe"))await FollowTo(AuditoriumLayout3D.SideObservation);
            _room.ApplyTransientState(new(false,null,null));
            using(session.Modal.Acquire("auditorium-inventory-probe"))
            {
                var before=_room.Companion.GlobalPosition;for(var tick=0;tick<20;tick++)_room.Companion._Process(.025);
                Require(before.DistanceTo(_room.Companion.GlobalPosition)<.001f,"Other modal did not pause follow.");
            }
            _room.ApplyTransientState(new(true,null,null,1));await FollowTo(AuditoriumLayout3D.LeftLeverCompanion);
            _room.ApplyTransientState(new(false,null,null));Require(!visual.ExitLeverHeld,"Lever survived reset.");
            dream.SolvedPuzzles.Clear();dream.InstalledParts.Clear();_room.RefreshState();_room.ApplyTransientState(new(false,null,null));
            Require(!visual.CurtainRaised && !visual.ProjectorSolved && !visual.MasterTapeInstalled && !visual.ExitOpen && visual.VisiblePickupCount==2 && visual.SolvedConsoleCount==0,"Initial physical state mismatch.");
            _room.ApplyTransientState(new(false,DreamEscapeIds.PuzzleAuditoriumStageAlignment,["3","1","4","2","window","figure","bell"]));
            Require(visual.LineSetValues.SequenceEqual([3,1,4,2]),"Weight preview mismatch.");
            Require(Mathf.Abs(((Node3D)model.FindChild("Projector_Disc1",true,false)).RotationDegrees.X-120)<.01f,"Disc preview did not rotate physical disc.");
            _room.ApplyTransientState(new(false,null,null));Require(visual.LineSetValues.SequenceEqual([1,1,1,1]),"Weight preview survived close.");
            Require(Mathf.Abs(((Node3D)model.FindChild("Projector_Disc1",true,false)).RotationDegrees.X)<.01f,"Disc preview survived close.");
            var measurements=new List<object>();
            if(DisplayServer.GetName()!="headless" && !Behavior)
            {
                foreach(var size in Gray?new[]{new Vector2I(1280,720)}:new[]{new Vector2I(1280,720),new Vector2I(1600,900),new Vector2I(1920,1080)})
                {
                    DisplayServer.WindowSetMode(DisplayServer.WindowMode.Windowed);DisplayServer.WindowSetFlag(DisplayServer.WindowFlags.Borderless,true);DisplayServer.WindowSetSize(size);await Frames(20);
                    var dir=Path.Combine(Output,$"{size.X}x{size.Y}");Directory.CreateDirectory(dir);dream.SolvedPuzzles.Clear();dream.InstalledParts.Clear();dream.Status=DreamEscapeStatus.InProgress;_room.RefreshState();_room.ApplyTransientState(new(false,null,null));
                    await Shot(dir,"01-entry.png",AuditoriumLayout3D.Entry,0);
                    await Shot(dir,"02-seating-and-side-aisle.png",new(6.2f,0,4.8f),45);
                    await Shot(dir,"03-stage-front.png",new(0,0,-2.5f),0);
                    await Shot(dir,"04-projector-before.png",new(-8.1f,0,-4.8f),90);
                    await Shot(dir,"05-fly-rail-before.png",new(8.1f,0,-5.2f),-90);
                    await Shot(dir,"06-side-observation.png",AuditoriumLayout3D.SideObservation,-90);
                    await Shot(dir,"07-splice-before.png",new(8.1f,0,3.1f),-90);
                    await Shot(dir,"08-master-before.png",new(8.1f,0,5.8f),-90);
                    await Shot(dir,"09-rear-controls.png",new(0,0,5.7f),180);
                    dream.SolvedPuzzles.UnionWith([DreamEscapeIds.PickupCurtainCounterweight,DreamEscapeIds.PickupProjectorLens]);dream.InstalledParts.UnionWith([DreamEscapeIds.SlotAuditoriumCurtainCounterweight,DreamEscapeIds.SlotAuditoriumProjectorLens]);_room.RefreshState();
                    Require(visual.VisiblePickupCount==0 && !visual.CurtainRaised,"Partial stage installation mismatch.");
                    await Shot(dir,"10-installed-lens.png",new(-8.1f,0,-4.8f),90);
                    _room.ApplyTransientState(new(false,DreamEscapeIds.PuzzleAuditoriumStageAlignment,["3","1","4","2","window","figure","bell"]));await Shot(dir,"11-live-weights.png",new(8.1f,0,-5.2f),-90);_room.ApplyTransientState(new(false,null,null));
                    dream.SolvedPuzzles.Add(DreamEscapeIds.PuzzleAuditoriumStageAlignment);_room.RefreshState();
                    await Shot(dir,"12-curtain-raised.png",new(0,0,-2.5f),0);
                    await Shot(dir,"13-revealed-tape-evidence.png",new(-8.1f,0,3.1f),90);
                    dream.SolvedPuzzles.Add(DreamEscapeIds.PuzzleAuditoriumTapeReconstruction);_room.RefreshState();await Shot(dir,"14-spliced-reel.png",new(8.1f,0,3.1f),-90);
                    dream.InstalledParts.Add(DreamEscapeIds.SlotAuditoriumMasterTape);dream.SolvedPuzzles.Add(DreamEscapeIds.PuzzleAuditoriumHandoffPlan);_room.RefreshState();
                    await Shot(dir,"15-master-playing.png",new(8.1f,0,5.8f),-90);
                    _room.ApplyTransientState(new(true,null,null,1));await FollowTo(AuditoriumLayout3D.LeftLeverCompanion);await Shot(dir,"16-left-cooperation.png",new(-7.55f,0,-1.8f),90);
                    _room.ApplyTransientState(new(true,null,null,2));Require(visual.SolvedConsoleCount==2,"Monitor stage did not light correct controls.");await Shot(dir,"17-monitor.png",new(-3.3f,0,5.7f),180);
                    _room.ApplyTransientState(new(true,null,null,3));Require(visual.SolvedConsoleCount==3,"Light stage did not light correct controls.");await Shot(dir,"18-lights.png",new(-6,0,5.7f),180);
                    _room.ApplyTransientState(new(false,null,null));Require(visual.SolvedConsoleCount==1,"Transient controls survived interruption.");
                    dream.Status=DreamEscapeStatus.ExitOpened;_room.RefreshState();Require(visual.ExitOpen && visual.SolvedConsoleCount==3,"ExitOpened model mismatch.");await Shot(dir,"19-exit-open.png",new(0,0,-2.5f),0);
                    visual.SetCollisionDebug(true);await Shot(dir,"20-collision-layout.png",AuditoriumLayout3D.Entry,0);visual.SetCollisionDebug(false);
                    _room.PlayerActor.Visible=false;_room.Companion.Visible=false;
                    await Shot(dir,"21-disc-detail.png",new(-8.1f,0,-4.8f),90);await Shot(dir,"22-seat-detail.png",new(0,0,.75f),-90);
                    _room.PlayerActor.Visible=true;_room.Companion.Visible=true;
                    foreach(var point in new[]{AuditoriumLayout3D.Entry,new Vector3(0,0,-2.5f)})
                    {
                        _room.PlayerActor.GlobalPosition=point;_room.CameraController.SnapToTarget();await Frames(20);var times=new List<double>();
                        for(var frame=0;frame<180;frame++) {_room.CameraController.SetYawImmediate(frame*2);var before=Time.GetTicksUsec();await Frames(1);times.Add((Time.GetTicksUsec()-before)/1000d);}
                        var order=times.Order().ToArray();measurements.Add(new{width=size.X,height=size.Y,point=point.ToString(),averageMs=times.Average(),p95Ms=order[(int)(order.Length*.95)],maxMs=times.Max()});
                    }
                }
            }
            File.WriteAllText(Path.Combine(Output,Behavior?"behavior.json":"metrics.json"),JsonSerializer.Serialize(new{engine=Engine.GetVersionInfo()["string"].AsString(),adapter=RenderingServer.GetVideoAdapterName(),loadMs,markers=15,traversals,restores,collisions=56,seats=32,sharedMesh=true,graybox=Gray,measurements,limitations="Synthetic scene/capsule acceptance; separate full input playthrough and human/device acceptance required."},new JsonSerializerOptions{WriteIndented=true}));
            GD.Print($"AUDITORIUM_V2_PROBE_PASS markers=15 capsuleRoutes={traversals} restores={restores} seats=32 sharedMesh=true graybox={Gray}");GetTree().Quit(0);
        }
        catch(Exception ex){GD.PushError("AUDITORIUM_V2_PROBE_FAIL: "+ex);GetTree().Quit(1);}
    }
    private async Task WalkCapsule(Vector3 destination)
    {
        var player=_room.PlayerActor;
        for(var tick=0;tick<3500 && player.GlobalPosition.DistanceTo(destination)>.035f;tick++)
        {
            var before=player.GlobalPosition;var next=_navigator.Advance(before,destination,.1f);Require(before.DistanceTo(next)<=.101f,"Route teleported.");player.MoveAndCollide(next-before);
            Require(player.GlobalPosition.DistanceTo(next)<.008f,"Capsule route hit geometry: "+before+" -> "+next);if(tick%30==0)await Physics();
        }
        Require(player.GlobalPosition.DistanceTo(destination)<.04f,"Capsule could not reach "+destination);
    }
    private async Task FollowTo(Vector3 destination)
    {
        for(var tick=0;tick<1800 && _room.Companion.GlobalPosition.DistanceTo(destination)>.04f;tick++)
        {
            var before=_room.Companion.GlobalPosition;_room.Companion._Process(.025);
            Require(!_room.AuditoriumVisual.IsBlocked(_room.Companion.GlobalPosition) && before.DistanceTo(_room.Companion.GlobalPosition)<=.086f,"Companion clipped/teleported.");
            if(tick%80==0)await Frames(1);
        }
        Require(_room.Companion.GlobalPosition.DistanceTo(destination)<.04f,"Companion did not reach cooperation pose: "+destination);
    }
    private async Task Shot(string dir,string name,Vector3 p,float yaw)
    {
        Require(!AuditoriumLayout3D.IsBlocked(p),"Unsafe capture pose: "+name);_room.PlayerActor.GlobalPosition=p;_room._Process(0);_room.CameraController.SetInputActive(false);_room.CameraController.SetYawImmediate(yaw);_room.CameraController.SnapToTarget();_room.PlayerActor.SetFacingImmediate(_room.CameraController.HorizontalForward);await Frames(8);
        await ToSignal(RenderingServer.Singleton,RenderingServer.SignalName.FramePostDraw);using var image=GetViewport().GetTexture().GetImage();Require(image.GetSize()==DisplayServer.WindowGetSize(),"Wrong screenshot size.");Require(image.SavePng(Path.Combine(dir,name))==Error.Ok,"Screenshot failed.");
    }
    private async Task Frames(int n){for(var i=0;i<n;i++)await ToSignal(GetTree(),SceneTree.SignalName.ProcessFrame);}
    private async Task Physics()=>await ToSignal(GetTree(),SceneTree.SignalName.PhysicsFrame);
}
