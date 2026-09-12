using Godot;
using System.Text.Json;
using TownRpg.Application;
using TownRpg.Core.Domain;
using TownRpg.Presentation;

namespace TownRpg.Testing;

// Synthetic engine acceptance, always suppresses the user's persistent save writes.
public partial class ManorV3Probe : Node
{
    private DreamMansionController3D _room = null!;
    private readonly bool _graybox = OS.GetCmdlineUserArgs().Contains("--manor-graybox");
    private readonly bool _behaviorOnly = OS.GetCmdlineUserArgs().Contains("--manor-behavior-only");
    private string Output => ProjectSettings.GlobalizePath(_graybox ? "res://outputs/manor-v3/graybox" : "res://outputs/manor-v3/probe");
    public override void _Ready() => CallDeferred(MethodName.Run);

    private async void Run()
    {
        try
        {
            Directory.CreateDirectory(Output);
            var session = GetNode<GameSession>("/root/GameSession");
            using var suppress = session.SuppressPersistence();
            session.NewGame("大厅验收", "同学");
            session.State.CompletedEvents.UnionWith(DreamPrologueCatalog.Groups.Select(g => DreamPrologueCatalog.EventId(g.Id)));
            var entry = DreamAreaSpawnCatalog.Resolve(DreamEscapeIds.AreaClassroom, DreamEscapeIds.AreaCorridor);
            session.MoveDreamToArea(DreamEscapeIds.AreaCorridor, entry.StoredPosition.X, entry.StoredPosition.Y);
            var started = Time.GetTicksUsec();
            var environment = ResourceLoader.Load<PackedScene>("res://assets/generated/dream_escape/3d/dream-manor-hall-v3.glb")
                ?? throw new InvalidOperationException("Manor v3 GLB missing.");
            var resourceMs = (Time.GetTicksUsec() - started) / 1000d;
            var packed = ResourceLoader.Load<PackedScene>("res://scenes/dream/DreamCorridor.tscn")!;
            started = Time.GetTicksUsec();
            _room = packed.Instantiate<DreamMansionController3D>();
            AddChild(_room);
            await Frames(5);
            var readyMs = (Time.GetTicksUsec() - started) / 1000d;
            var player = _room.PlayerActor;
            var visual = _room.MansionVisual;
            if (visual.IsBlocked(player.GlobalPosition) || _room.CurrentInteractionTargetId is not null)
                throw new InvalidOperationException("Door-entry position is blocked or auto-focuses a door.");
            ValidateModel();
            await ValidateMarkers(session);
            var routes = new[]
            {
                (new Vector3(0,0,-7.1f),new Vector3(0,0,10.8f)),
                (new Vector3(-1.5f,0,-6.8f),new Vector3(-1.5f,0,10.5f)),
                (new Vector3(1.5f,0,-6.8f),new Vector3(1.5f,0,10.5f)),
                (new Vector3(-2.35f,0,2.4f),new Vector3(2.35f,0,2.4f)),
                (new Vector3(-2.35f,0,8.8f),new Vector3(1.7f,0,8.8f)),
                (new Vector3(-3.7f,0,-6.8f),new Vector3(2f,0,-6.8f)),
                (new Vector3(-3.7f,0,-1f),new Vector3(1.7f,0,-1f))
            };
            foreach (var (a,b) in routes)
            foreach (var (from,to) in new[] {(a,b),(b,a)})
            {
                player.GlobalPosition = from;
                await ToSignal(GetTree(), SceneTree.SignalName.PhysicsFrame);
                if (visual.IsBlocked(from) || visual.IsBlocked(to) || player.TestMove(player.GlobalTransform,to-from))
                    throw new InvalidOperationException($"Physical route blocked: {from} -> {to}");
            }
            // Use actual physics against stairs, narrowed shoulders, exterior walls and the front door.
            foreach (var (from,to) in new[]
            {
                (new Vector3(1.7f,0,-3.3f),new Vector3(3.7f,0,-3.3f)),
                (new Vector3(3.7f,0,-6.2f),new Vector3(3.7f,0,-4.8f)),
                (new Vector3(4.3f,0,-.5f),new Vector3(4.3f,0,1f)),
                (new Vector3(0,0,-6.6f),new Vector3(0,0,-8.6f)),
                (new Vector3(-2.35f,0,2.4f),new Vector3(-4f,0,2.4f))
            })
            {
                player.GlobalPosition=from;
                await ToSignal(GetTree(),SceneTree.SignalName.PhysicsFrame);
                if (!player.TestMove(player.GlobalTransform,to-from))
                    throw new InvalidOperationException($"Collision barrier leaked: {from} -> {to}");
            }
            var savedPositions = MansionLayout3D.Collisions.Where(c=>c.Furniture).Select(c=>c.Position)
                .Concat(new[] {new Vector3(4.4f,0,5f),new Vector3(-4.4f,0,.8f),new Vector3(5f,0,-4f),new Vector3(0,0,-8f)}).ToArray();
            var mapper = new DreamMansionCoordinateMapper3D();
            foreach (var requested in savedPositions)
            {
                var stored=mapper.ToStored(requested);
                session.MoveDreamToArea(DreamEscapeIds.AreaCorridor,stored.X,stored.Y);
                var restored=packed.Instantiate<DreamMansionController3D>();
                AddChild(restored);
                await Frames(1);
                var savedAgain=mapper.ToWorld(new(session.State.DreamEscape!.PositionX,session.State.DreamEscape.PositionY));
                if (restored.MansionVisual.IsBlocked(restored.PlayerActor.GlobalPosition) ||
                    savedAgain.DistanceTo(restored.PlayerActor.GlobalPosition)>.001f ||
                    session.State.DreamEscape.ContentVersion != 6)
                    throw new InvalidOperationException("Legacy-position safe restore did not persist corrected coordinates: "+requested);
                restored.QueueFree();
                await Frames(1);
            }
            foreach (var stored in new[] {Vector2.Zero,new Vector2(640,360),new Vector2(127.3f,234.7f),new Vector2(412.7f,32.2f)})
                if (mapper.ToStored(mapper.ToWorld(stored)).DistanceTo(stored)>.001f)
                    throw new InvalidOperationException("Stored coordinate roundtrip failed.");

            foreach (var (from,to) in new[]
            {
                (new Vector3(-3.7f,0,-6.8f),new Vector3(-3.7f,0,-1f)),
                (new Vector3(3.7f,0,-6.2f),new Vector3(1.7f,0,.8f)),
                (new Vector3(2.35f,0,2.4f),new Vector3(-2.35f,0,8.8f)),
                (new Vector3(0,0,10.6f),new Vector3(-3.7f,0,-6.8f))
            })
            {
                var position=from;
                for(var tick=0;tick<600 && position.DistanceTo(to)>.04f;tick++)
                {
                    position=visual.AdvanceCompanion(position,to,.10f);
                    if(visual.IsBlocked(position))throw new InvalidOperationException("Companion entered an obstacle: "+position);
                }
                if(position.DistanceTo(to)>.04f)throw new InvalidOperationException($"Companion failed to reach {to} from {from}.");
            }
            var measurements = new List<object>();
            // Captures and scripted traversal own movement; desktop input must not change the camera.
            _room.SetProcess(false);
            player.SetPhysicsProcess(false);
            _room.CameraController.SetInputActive(false);
            if (DisplayServer.GetName() != "headless" && !_behaviorOnly)
            {
                var sizes = _graybox ? new[] {new Vector2I(1280,720)} : new[] {new Vector2I(1280,720),new Vector2I(1600,900),new Vector2I(1920,1080)};
                foreach(var size in sizes)
                {
                    DisplayServer.WindowSetMode(DisplayServer.WindowMode.Windowed);
                    DisplayServer.WindowSetFlag(DisplayServer.WindowFlags.Borderless,true);
                    DisplayServer.WindowSetSize(size);
                    await Frames(20);
                    var directory=Path.Combine(Output,$"{size.X}x{size.Y}");
                    Directory.CreateDirectory(directory);
                    await Shot(directory,"01-foyer-front-door.png",new(0,0,-4.2f),0);
                    await Shot(directory,"02-foyer-reverse.png",new(0,0,-3.9f),180);
                    await Shot(directory,"03-hall-looking-north.png",new(0,0,-1.5f),180);
                    await Shot(directory,"04-hall-looking-south.png",new(0,0,6),0);
                    await Shot(directory,"05-stair-enclosure.png",new(0,0,-3.3f),-90);
                    await Shot(directory,"06-fireplace.png",new(-1.6f,0,-4.2f),90);
                    await Shot(directory,"07-seating.png",new(-1.7f,0,-5.5f),90);
                    await Shot(directory,"08-console.png",new(2,0,-6.6f),-100);
                    await Shot(directory,"09-room-one.png",new(-1.25f,0,2.4f),90);
                    await Shot(directory,"10-room-two.png",new(1.25f,0,2.4f),-90);
                    await Shot(directory,"11-room-three.png",new(-1.25f,0,8.8f),90);
                    await Shot(directory,"12-room-four.png",new(0,0,9.9f),180);
                    await Shot(directory,"13-bench.png",new(1,0,6.6f),-90);
                    await _room.PlayExitTransitionAsync("area:dream.classroom",CancellationToken.None);
                    await Shot(directory,"14-room-one-open.png",new(-1.25f,0,2.4f),90);
                    _room.ResetExitTransition();
                    await _room.PlayExitTransitionAsync("area:dream.auditorium",CancellationToken.None);
                    await Shot(directory,"15-room-four-open.png",new(0,0,9.9f),180);
                    _room.ResetExitTransition();
                    visual.SetCollisionDebug(true);
                    await Shot(directory,"16-collision-foyer.png",new(0,0,-4.2f),-35);
                    await Shot(directory,"17-collision-hall.png",new(0,0,4.3f),180);
                    visual.SetCollisionDebug(false);
                    foreach(var (label,position) in new[] {("foyer",new Vector3(0,0,-3.9f)),("hall",new Vector3(0,0,6))})
                    {
                        player.GlobalPosition=position;
                        _room.CameraController.SnapToTarget();
                        await Frames(30);
                        var elapsed=new List<double>();
                        var minimumArm=float.MaxValue;
                        var previous=Time.GetTicksUsec();
                        for(var frame=0;frame<180;frame++)
                        {
                            _room.CameraController.SetYawImmediate(frame*2);
                            await ToSignal(RenderingServer.Singleton,RenderingServer.SignalName.FramePostDraw);
                            var now=Time.GetTicksUsec();
                            elapsed.Add((now-previous)/1000d);previous=now;
                            minimumArm=Math.Min(minimumArm,_room.CameraController.SpringArm.GetHitLength());
                        }
                        elapsed.Sort();
                        measurements.Add(new {resolution=$"{size.X}x{size.Y}",area=label,meanFrameMs=elapsed.Average(),
                            p95FrameMs=elapsed[(int)(elapsed.Count*.95)],maxFrameMs=elapsed[^1],minimumArm});
                    }
                }
            }
            session.State.DreamEscape!.XiaoyuJoined=true;
            var companion=_room.GetNode<DreamCompanion3D>("XiaoyuCompanion");
            player.SetPhysicsProcess(false);
            player.GlobalPosition=new(0,0,-6.7f);
            player.SetFacingImmediate(Vector3.Back);
            companion.RefreshImmediate();
            foreach(var target in new[] {new Vector3(0,0,10.6f),new Vector3(-2.35f,0,8.8f),
                         new Vector3(0,0,6),new Vector3(2.35f,0,2.4f),new Vector3(0,0,-6.7f)})
            {
                while(player.GlobalPosition.DistanceTo(target)>.02f)
                {
                    var step=player.GlobalPosition.DirectionTo(target)*Math.Min(.15f,player.GlobalPosition.DistanceTo(target));
                    player.SetFacingImmediate(step.Normalized());
                    if(player.MoveAndCollide(step) is not null)throw new InvalidOperationException("Traversal hit an obstacle.");
                    await Frames(1);
                    if(visual.IsBlocked(companion.GlobalPosition))throw new InvalidOperationException("Live companion crossed a barrier.");
                }
            }
            await Frames(15);
            if(DisplayServer.GetName()!="headless")
            {
                if (_behaviorOnly) { DisplayServer.WindowSetSize(new(1920,1080)); await Frames(20); }
                await Shot(Output,"18-companion-return.png",player.GlobalPosition,90);
            }
            File.WriteAllText(Path.Combine(Output,DisplayServer.GetName()=="headless" || _behaviorOnly ? "behavior-metrics.json" : "metrics.json"),JsonSerializer.Serialize(new {
                engine=Engine.GetVersionInfo()["string"].AsString(),renderer=RenderingServer.GetCurrentRenderingMethod(),
                adapter=RenderingServer.GetVideoAdapterName(),environmentResourceLoadMs=resourceMs,
                roomReadyIncludingCharacterAndFiveFramesMs=readyMs,measurements,
                physicalRoutes=14,barriers=5,savedPositionRestores=savedPositions.Length,markers=8,activeMarkers=5,
                companionLoop=true,graybox=_graybox,
                limits="Synthetic checks, not physical mouse/Xbox/blind playtest/target hardware/export acceptance."
            },new JsonSerializerOptions{WriteIndented=true}));
            GD.Print($"MANOR_V3_PROBE_PASS targets=8 active=5 routes=14 barriers=5 saves={savedPositions.Length} companion=true graybox={_graybox}");
            GC.KeepAlive(environment);
            GetTree().Quit(0);
        }
        catch(Exception exception)
        {
            GD.PushError("MANOR_V3_PROBE_FAIL: "+exception);
            GetTree().Quit(1);
        }
    }

    private void ValidateModel()
    {
        var visual=_room.MansionVisual;
        if(visual.CollisionBodyCount!=25 || visual.FurnitureCollisionCount!=7 || visual.DoorBarrierCount!=5 || visual.DoorHingeCount!=7)
            throw new InvalidOperationException("Manor composition counts do not match v3.");
        foreach(var (name,center) in new[] {("Door_Front_Root",MansionLayout3D.FrontDoor),
            ("Door_Room1_Root",MansionLayout3D.RoomOneDoor),("Door_Room2_Root",MansionLayout3D.RoomTwoDoor),
            ("Door_Room3_Root",MansionLayout3D.RoomThreeDoor),("Door_Room4_Root",MansionLayout3D.RoomFourDoor)})
        {
            var node=visual.ModelRoot.FindChild(name,true,false) as Node3D;
            if(node is null || node.GlobalPosition.DistanceTo(center)>.002f)
                throw new InvalidOperationException("Misaligned door root: "+name+" actual="+node?.GlobalPosition);
        }
        foreach(var (meshName,collisionName) in new[] {("V3_Fireplace","Fireplace"),("V3_ChairSouth","ChairSouth"),
            ("V3_ChairNorth","ChairNorth"),("V3_ConsoleTable","ConsoleTable"),("V3_FoyerBench","FoyerBench"),
            ("V3_SideTable","SideTable"),("V3_StairStructure","StairEnclosure")})
        {
            var mesh=visual.ModelRoot.FindChild(meshName,true,false) as MeshInstance3D
                ?? throw new InvalidOperationException("Missing physical model "+meshName);
            var spec=MansionLayout3D.Collisions.Single(c=>c.Name==collisionName);
            var bounds=mesh.GlobalTransform*mesh.GetAabb();
            if(bounds.Position.X<spec.Position.X-spec.Size.X/2-.06f || bounds.End.X>spec.Position.X+spec.Size.X/2+.06f ||
               bounds.Position.Z<spec.Position.Z-spec.Size.Z/2-.06f || bounds.End.Z>spec.Position.Z+spec.Size.Z/2+.06f)
                throw new InvalidOperationException($"{meshName} escapes collision footprint: {bounds}");
        }
        for(var z=-6.8f;z<=10.5f;z+=.2f)
        for(var x=-1.5f;x<=1.5f;x+=.25f)
            if(visual.IsBlocked(new(x,0,z)))throw new InvalidOperationException("Three-metre central route narrowed.");
        var environment=_room.GetNode<WorldEnvironment>("WorldEnvironment").Environment;
        if(environment.AmbientLightSource!=Godot.Environment.AmbientSource.Color || environment.AmbientLightSkyContribution>0)
            throw new InvalidOperationException("Manor ambient light must be explicit Color with zero sky blend.");
    }

    private async Task ValidateMarkers(GameSession session)
    {
        var markers=_room.GetNode<Node3D>("InteractionMarkers").GetChildren().OfType<InteractionMarker3D>().ToArray();
        var dream=session.State.DreamEscape!;
        var unlocks=new[]{DreamEscapeIds.AreaMusic,DreamEscapeIds.AreaArchive,DreamEscapeIds.AreaAuditorium};
        for(var stage=0;stage<=3;stage++)
        {
            foreach(var area in unlocks)dream.UnlockedAreas.Remove(area);
            dream.UnlockedAreas.UnionWith(unlocks.Take(stage));
            _room.RefreshState();
            if(_room.InteractionMarkerCount!=8 || _room.ActiveInteractionMarkerCount!=5)
                throw new InvalidOperationException("Locked/open marker exclusivity failed.");
            foreach(var marker in markers)
            {
                _room.PlayerActor.GlobalPosition=marker.Position;
                await Frames(2);
                var expected=marker.TargetId;
                for(var i=0;i<3;i++)
                {
                    var locked=new[]{"target.corridor.room_two_door","target.corridor.room_three_door","target.corridor.room_four_door"}[i];
                    var open="area:"+unlocks[i];
                    if(expected==locked || expected==open)expected=stage>i?open:locked;
                }
                if(_room.MansionVisual.IsBlocked(marker.Position) || _room.CurrentInteractionTargetId!=expected)
                    throw new InvalidOperationException($"Marker focus failed at stage {stage}: {expected} actual={_room.CurrentInteractionTargetId}");
            }
        }
    }

    private async Task Frames(int count)
    {for(var i=0;i<count;i++)await ToSignal(GetTree(),SceneTree.SignalName.ProcessFrame);}

    private async Task Shot(string directory,string name,Vector3 position,float yaw)
    {
        if(_room.MansionVisual.IsBlocked(position))throw new InvalidOperationException("Unsafe capture: "+name);
        _room.PlayerActor.GlobalPosition=position;
        _room._Process(0); // Refresh only the marker highlight for this fixed pose.
        _room.CameraController.SetInputActive(false);
        _room.CameraController.SetYawImmediate(yaw);
        _room.CameraController.SnapToTarget();
        await Frames(8);
        await ToSignal(RenderingServer.Singleton,RenderingServer.SignalName.FramePostDraw);
        using var image=GetViewport().GetTexture().GetImage();
        if(image.GetSize()!=DisplayServer.WindowGetSize())throw new InvalidOperationException("Wrong capture dimensions.");
        if(image.SavePng(Path.Combine(directory,name))!=Error.Ok)throw new IOException("Cannot save "+name);
    }
}
