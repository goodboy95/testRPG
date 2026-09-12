using Godot;
using System.Text.Json;
using TownRpg.Application;
using TownRpg.Core.Domain;
using TownRpg.Presentation;

namespace TownRpg.Testing;

// Engine geometry/camera acceptance. Persistence is suppressed for the entire run.
public partial class ClassroomV6Probe : Node
{
    private DreamAreaController3D _room = null!;
    private readonly string _output = ProjectSettings.GlobalizePath("res://outputs/classroom-v6/probe");
    public override void _Ready() => CallDeferred(MethodName.Run);

    private async void Run()
    {
        try
        {
            Directory.CreateDirectory(_output);
            var session = GetNode<GameSession>("/root/GameSession");
            using var suppress = session.SuppressPersistence();
            session.NewGame("场景验收", "同学");
            session.State.CompletedEvents.Add(DreamPrologueCatalog.EventId("intro"));
            session.State.CompletedEvents.UnionWith(DreamPrologueCatalog.Groups.Select(g => DreamPrologueCatalog.EventId(g.Id)));
            var resourceStarted = Time.GetTicksUsec();
            var environmentResource = ResourceLoader.Load<PackedScene>("res://assets/generated/dream_escape/3d/classroom-art-pass-v6.glb");
            var environmentResourceMs = (Time.GetTicksUsec() - resourceStarted) / 1000d;
            var packed = ResourceLoader.Load<PackedScene>("res://scenes/dream/DreamClassroom.tscn");
            var started = Time.GetTicksUsec();
            _room = packed.Instantiate<DreamAreaController3D>();
            AddChild(_room);
            await Frames(5);
            var loadMs = (Time.GetTicksUsec() - started) / 1000d;
            var visual = _room.ClassroomVisual;
            var player = _room.PlayerActor;
            var start = player.GlobalPosition;
            if (visual.IsBlocked(start) || _room.CurrentInteractionTargetId is not null)
                throw new InvalidOperationException("New-game spawn is blocked or auto-focuses a target.");
            ValidateDeskBounds();
            foreach (var marker in _room.GetNode<Node3D>("InteractionMarkers").GetChildren().OfType<InteractionMarker3D>())
            {
                if (visual.IsBlocked(marker.Position)) throw new InvalidOperationException("Blocked marker: " + marker.TargetId);
                player.GlobalPosition = marker.Position;
                await Frames(2);
                if (_room.CurrentInteractionTargetId != marker.TargetId)
                    throw new InvalidOperationException("Wrong focus at " + marker.TargetId + ": " + _room.CurrentInteractionTargetId);
            }
            // Physical sweeps along the major routes, both directions, include the actual capsule radius.
            var routes = new[]
            {
                (new Vector3(-4.75f,0,-2.3f), new Vector3(-4.75f,0,3.7f)),
                (new Vector3(0,0,-1.8f), new Vector3(0,0,3.7f)),
                (new Vector3(4.75f,0,-2.3f), new Vector3(4.75f,0,3.7f)),
                (new Vector3(-4.75f,0,3.7f), new Vector3(4.75f,0,3.7f)),
                (new Vector3(-2.4f,0,-1.8f), new Vector3(4.75f,0,-1.8f))
            };
            foreach (var (a,b) in routes)
            {
                foreach (var (from,to) in new[] { (a,b), (b,a) })
                {
                    player.GlobalPosition = from;
                    await ToSignal(GetTree(), SceneTree.SignalName.PhysicsFrame);
                    if (player.TestMove(player.GlobalTransform, to-from))
                        throw new InvalidOperationException($"Capsule route blocked: {from} -> {to}");
                }
            }
            foreach (var position in ClassroomLayout3D.DeskPositions)
                if (visual.IsBlocked(visual.ResolveSpawn(position)))
                    throw new InvalidOperationException("Saved-position recovery failed at " + position);
            foreach (var (from,to) in new[] {
                (new Vector3(0,0,3.7f), new Vector3(4.75f,0,-1.8f)),
                (new Vector3(4.75f,0,-1.8f), new Vector3(-4.75f,0,3.7f)) })
            {
                var position=from;
                for(var tick=0;tick<300 && position.DistanceTo(to)>.05f;tick++)
                {
                    position=visual.AdvanceCompanion(position,to,.10f);
                    if(visual.IsBlocked(position)) throw new InvalidOperationException("Companion detour crossed an obstacle.");
                }
                if(position.DistanceTo(to)>.05f) throw new InvalidOperationException("Companion detour failed to arrive.");
            }

            var metrics = new List<object>();
            if (DisplayServer.GetName() != "headless")
            {
                foreach (var size in new[] { new Vector2I(1280,720), new Vector2I(1600,900), new Vector2I(1920,1080) })
                {
                    DisplayServer.WindowSetMode(DisplayServer.WindowMode.Windowed);
                    DisplayServer.WindowSetFlag(DisplayServer.WindowFlags.Borderless, true);
                    DisplayServer.WindowSetSize(size);
                    await Frames(20);
                    var directory = Path.Combine(_output, $"{size.X}x{size.Y}");
                    Directory.CreateDirectory(directory);
                    await Shot(directory, "01-spawn.png", start, 0f);
                    await Shot(directory, "02-center-to-blackboard.png", new(0,0,2.3f), 0f);
                    await Shot(directory, "03-center-to-rear.png", new(0,0,-1.5f), 180f);
                    await Shot(directory, "04-west-window.png", new(-4.65f,0,.4f), 90f);
                    await Shot(directory, "05-east-door.png", new(4.7f,0,3.35f), -90f);
                    await Shot(directory, "06-student-desk.png", new(.8f,0,.6f), -75f);
                    await Shot(directory, "07-maintenance-card.png", new(-4.45f,0,2.3f), 90f);
                    await Shot(directory, "08-duty-note.png", new(4.45f,0,2.3f), -90f);
                    await Shot(directory, "09-schedule.png", new(-2.45f,0,-2.05f), 70f);
                    await Shot(directory, "10-clock-initial.png", new(3.55f,0,-3.25f), 0f);
                    session.State.DreamEscape!.SolvedPuzzles.UnionWith([
                        DreamEscapeIds.PuzzleWindowLatches, DreamEscapeIds.PickupClockHand,
                        DreamEscapeIds.PuzzleBlackboard, DreamEscapeIds.PuzzleClockRestore,
                        DreamEscapeIds.PuzzleRosterSlider, DreamEscapeIds.PuzzleTeacherDesk, DreamEscapeIds.PuzzleMapCabinet]);
                    visual.ApplyState(session.State.DreamEscape);
                    await Shot(directory, "11-teacher-drawer-open.png", new(.55f,0,-2.1f), 0f);
                    await Shot(directory, "12-map-cabinet-open.png", new(-4.05f,0,-3.05f), 20f);
                    await Shot(directory, "13-window-cache-open.png", new(-5.2f,0,-.9f), 90f);
                    await Shot(directory, "14-clock-restored.png", new(3.55f,0,-3.25f), 0f);
                    await Shot(directory, "15-roster-aligned.png", new(4.75f,0,-3.65f), 0f);
                    player.GlobalPosition = new(4.7f,0,3.35f);
                    _room.CameraController.SetYawImmediate(-90f);
                    _room.CameraController.SnapToTarget();
                    await _room.PlayExitTransitionAsync("area:dream.corridor", CancellationToken.None);
                    await Capture(directory,"16-door-open.png");
                    _room.ResetDoor();
                    visual.SetCollisionDebug(true);
                    await Shot(directory,"17-collision.png",new(0,0,2.3f),0f);
                    visual.SetCollisionDebug(false);
                    session.State.DreamEscape.SolvedPuzzles.Clear();
                    visual.ApplyState(session.State.DreamEscape);
                    player.GlobalPosition = new(0,0,.8f);
                    _room.CameraController.SnapToTarget();
                    await Frames(30);
                    var elapsed = new List<double>();
                    var minimumArm = float.MaxValue;
                    var previous = Time.GetTicksUsec();
                    for (var frame=0; frame<180; frame++)
                    {
                        _room.CameraController.SetYawImmediate(frame*2f);
                        await ToSignal(RenderingServer.Singleton, RenderingServer.SignalName.FramePostDraw);
                        var now = Time.GetTicksUsec();
                        elapsed.Add((now-previous)/1000d);
                        previous=now;
                        minimumArm=Math.Min(minimumArm,_room.CameraController.SpringArm.GetHitLength());
                    }
                    elapsed.Sort();
                    metrics.Add(new { resolution=$"{size.X}x{size.Y}", meanFrameMs=elapsed.Average(),
                        p95FrameMs=elapsed[(int)(elapsed.Count*.95)], maxFrameMs=elapsed[^1], minimumArm });
                }
            }
            // Follow the actual classroom loop after Xiaoyu joins. This is synthetic engine input.
            session.State.DreamEscape!.XiaoyuJoined = true;
            var companion = _room.GetNode<DreamCompanion3D>("XiaoyuCompanion");
            player.SetPhysicsProcess(false);
            player.GlobalPosition = new(0,0,-1.8f);
            player.SetFacingImmediate(Vector3.Back);
            companion.RefreshImmediate();
            foreach (var target in new[] { new Vector3(0,0,3.7f), new Vector3(4.75f,0,3.7f),
                                          new Vector3(4.75f,0,-1.8f), new Vector3(0,0,-1.8f) })
            {
                while (player.GlobalPosition.DistanceTo(target)>.02f)
                {
                    var step=player.GlobalPosition.DirectionTo(target)*Math.Min(.18f,player.GlobalPosition.DistanceTo(target));
                    player.SetFacingImmediate(step.Normalized());
                    if (player.MoveAndCollide(step) is not null) throw new InvalidOperationException("Traversal hit furniture.");
                    await Frames(1);
                    if (visual.IsBlocked(companion.GlobalPosition))
                        throw new InvalidOperationException("Companion entered furniture at " + companion.GlobalPosition);
                }
            }
            await Frames(15);
            if (DisplayServer.GetName() != "headless") await Shot(_output,"18-companion-return.png",new(0,0,2.3f),0f);
            File.WriteAllText(Path.Combine(_output,"metrics.json"),JsonSerializer.Serialize(new {
                engine=Engine.GetVersionInfo()["string"].AsString(),renderer=RenderingServer.GetCurrentRenderingMethod(),
                adapter=RenderingServer.GetVideoAdapterName(),environmentResourceLoadMs=environmentResourceMs,
                roomReadyIncludingCharacterAndFiveFramesMs=loadMs,
                measurements=metrics,physicalRoutes=10,targets=11,desks=12,companionLoop=true,
                limits="Synthetic engine checks; not raw mouse, Xbox hardware, blind playtest or export acceptance."
            },new JsonSerializerOptions { WriteIndented=true }));
            GD.Print("CLASSROOM_V6_PROBE_PASS targets=11 desks=12 routes=10 saves=12 companion=true");
            GC.KeepAlive(environmentResource);
            GetTree().Quit(0);
        }
        catch(Exception exception)
        {
            GD.PushError("CLASSROOM_V6_PROBE_FAIL: " + exception);
            GetTree().Quit(1);
        }
    }

    private void ValidateDeskBounds()
    {
        var model=_room.GetNode<Node3D>("ClassroomModel");
        for(var i=1;i<=12;i++)
        {
            var root=(Node3D)model.FindChild($"StudentDeskGroup_{i:00}",true,false);
            var mesh=(MeshInstance3D)root.FindChild($"StudentDeskArt_{i:00}",true,false);
            var bounds=mesh.GetAabb();
            if (bounds.Position.Y < -.01f || bounds.End.Y > .90f || bounds.Size.X > .82f || bounds.Size.Z > 1.02f)
                throw new InvalidOperationException($"Desk {i} escapes its collision envelope: {bounds}");
        }
    }

    private async Task Frames(int count)
    {
        for(var i=0;i<count;i++) await ToSignal(GetTree(),SceneTree.SignalName.ProcessFrame);
    }

    private async Task Shot(string directory,string name,Vector3 position,float yaw)
    {
        if (_room.ClassroomVisual.IsBlocked(position)) throw new InvalidOperationException("Unsafe capture: " + name);
        _room.PlayerActor.GlobalPosition=position;
        _room.CameraController.SetYawImmediate(yaw);
        _room.CameraController.SnapToTarget();
        await Frames(8);
        await Capture(directory,name);
    }

    private async Task Capture(string directory,string name)
    {
        await ToSignal(RenderingServer.Singleton,RenderingServer.SignalName.FramePostDraw);
        using var image=GetViewport().GetTexture().GetImage();
        if (image.GetSize()!=DisplayServer.WindowGetSize()) throw new InvalidOperationException("Capture resolution mismatch.");
        if (image.SavePng(Path.Combine(directory,name))!=Error.Ok) throw new IOException("Cannot save " + name);
    }
}
