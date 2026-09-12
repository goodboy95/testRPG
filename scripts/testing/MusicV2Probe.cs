using Godot;
using System.Text.Json;
using TownRpg.Application;
using TownRpg.Core.Domain;
using TownRpg.Presentation;

namespace TownRpg.Testing;

// Synthetic acceptance with real scene/physics; never modifies the player's save files.
public partial class MusicV2Probe : Node
{
    private DreamMusicRoomController3D _room = null!;
    private readonly bool _graybox = OS.GetCmdlineUserArgs().Contains("--music-graybox");
    private readonly bool _behaviorOnly = OS.GetCmdlineUserArgs().Contains("--music-behavior-only");
    private readonly bool _preview = OS.GetCmdlineUserArgs().Contains("--music-preview");
    private string Output => ProjectSettings.GlobalizePath(_graybox ? "res://outputs/music-v2/graybox" : _preview ? "res://outputs/music-v2/preview" : "res://outputs/music-v2/probe");
    private static void Require(bool value, string message) { if (!value) throw new InvalidOperationException(message); }
    public override void _Ready() => CallDeferred(MethodName.Run);

    private async void Run()
    {
        try
        {
            Directory.CreateDirectory(Output);
            var session = GetNode<GameSession>("/root/GameSession");
            using var suppress = session.SuppressPersistence();
            session.NewGame("音乐室验收", "同学");
            session.State.CompletedEvents.UnionWith(DreamPrologueCatalog.Groups.Select(g => DreamPrologueCatalog.EventId(g.Id)));
            var state = session.State.DreamEscape!;
            state.UnlockedAreas.Add(DreamEscapeIds.AreaMusic);
            var stored = MusicRoomLayout3D.Mapper.ToStored(MusicRoomLayout3D.Entry);
            session.MoveDreamToArea(DreamEscapeIds.AreaMusic, stored.X, stored.Y);
            var started = Time.GetTicksUsec();
            var resource = ResourceLoader.Load<PackedScene>("res://assets/generated/dream_escape/3d/music-room-art-pass-v2.glb")!;
            var resourceMs = (Time.GetTicksUsec() - started) / 1000d;
            var packed = ResourceLoader.Load<PackedScene>("res://scenes/dream/DreamMusicRoom.tscn")!;
            started = Time.GetTicksUsec();
            _room = packed.Instantiate<DreamMusicRoomController3D>();
            AddChild(_room);
            await Frames(5);
            var readyMs = (Time.GetTicksUsec() - started) / 1000d;
            Require(_room.CurrentInteractionTargetId is null, "Entry automatically focuses an interaction.");
            _room.SetProcess(false);
            _room.PlayerActor.SetPhysicsProcess(false);
            _room.Companion.SetProcess(false);
            _room.CameraController.SetInputActive(false);
            var visual = _room.MusicVisual;
            ValidateModel();
            Require(!_room.Companion.IsFollowing && !visual.EquipmentPassable && visual.VisiblePickupCount == 0, "Initial state is not closed.");
            Require(_room.Companion.GlobalPosition.DistanceTo(MusicRoomLayout3D.BeforeJoin) < .001f, "Girl is not inside the room.");
            await ValidateMarkers();
            await PhysicalRoute(new(4.35f,0,-.8f), new(4.35f,0,-3.1f), false);
            // Joining during the introduction must not relocate her through the closed/animating door.
            using (session.Modal.Acquire("music-v2-introduction-probe"))
            {
                state.SolvedPuzzles.Add(DreamEscapeIds.PuzzleMusicCage);
                state.XiaoyuJoined = true;
                _room.RefreshState();
                _room.Companion._Process(.1);
                Require(_room.Companion.GlobalPosition.DistanceTo(MusicRoomLayout3D.BeforeJoin) < .001f, "Joining teleported the companion.");
                visual._Process(.2);
                Require(!visual.EquipmentPassable, "Equipment collision opened before the leaf cleared.");
                visual._Process(.2);
                Require(visual.EquipmentPassable, "Open equipment door retained its collision.");
                _room.Companion._Process(.1);
                Require(_room.Companion.GlobalPosition.DistanceTo(MusicRoomLayout3D.BeforeJoin) < .001f, "Companion moved during introduction.");
            }
            await Frames(2);
            await ValidateMarkers();
            var routes = new[]
            {
                (new Vector3(4.35f,0,-.8f),new Vector3(4.35f,0,-4.1f)),
                (new Vector3(4.35f,0,-4.1f),MusicRoomLayout3D.SideObservation),
                (new Vector3(-1.5f,0,-2.4f),new Vector3(-1.5f,0,2.7f)),
                (new Vector3(0f,0,-2.4f),new Vector3(0f,0,2.7f)),
                (new Vector3(1.5f,0,-2.4f),new Vector3(1.5f,0,2.7f)),
                (new Vector3(-4.7f,0,-2.5f),new Vector3(-4.7f,0,2.7f)),
                (new Vector3(-4.7f,0,2.7f),new Vector3(4.6f,0,2.7f)),
                (new Vector3(-3.35f,0,3.6f),new Vector3(-.98f,0,3.6f)),
                (new Vector3(1.4f,0,3.2f),new Vector3(3.4f,0,3.2f)),
                (new Vector3(-4.35f,0,-3.3f),new Vector3(-.65f,0,-3.3f))
            };
            foreach (var (a,b) in routes)
            {
                await PhysicalRoute(a,b,true);
                await PhysicalRoute(b,a,true);
            }
            await PhysicalRoute(new(2.1f,0,-2.4f),new(3.45f,0,-2.4f),false);
            await PhysicalRoute(new(4.35f,0,-2.5f),new(5.2f,0,-2.5f),false);
            await PhysicalRoute(new(-5.3f,0,3.55f),new(-6.4f,0,3.55f),false);
            var restoredCount = await ValidateRestores(session, packed);
            foreach (var (a,b) in new[]
            {
                (MusicRoomLayout3D.BeforeJoin,MusicRoomLayout3D.Entry),
                (MusicRoomLayout3D.Entry,MusicRoomLayout3D.SideObservation),
                (MusicRoomLayout3D.SideObservation,new Vector3(-4.7f,0,2.7f)),
                (new Vector3(-4.7f,0,-2.5f),new Vector3(-1.2f,0,2.7f))
            }) VerifyNavigation(a,b);
            ValidateCompanion(session);
            ValidateStates(state);
            var measurements = new List<object>();
            if (DisplayServer.GetName() != "headless" && !_behaviorOnly)
            {
                var sizes = _graybox || _preview ? new[] { new Vector2I(1280,720) } : new[] { new Vector2I(1280,720),new Vector2I(1600,900),new Vector2I(1920,1080) };
                foreach (var size in sizes)
                {
                    DisplayServer.WindowSetMode(DisplayServer.WindowMode.Windowed);
                    DisplayServer.WindowSetFlag(DisplayServer.WindowFlags.Borderless,true);
                    DisplayServer.WindowSetSize(size);
                    await Frames(20);
                    var directory = Path.Combine(Output,$"{size.X}x{size.Y}");
                    Directory.CreateDirectory(directory);
                    state.SolvedPuzzles.Clear();state.InstalledParts.Clear();state.XiaoyuJoined=false;
                    _room.RefreshState();
                    await Shot(directory,"01-entry-overview.png",MusicRoomLayout3D.Entry,-35);
                    await Shot(directory,"02-teaching-and-windows.png",new(-1.2f,0,-1.8f),55);
                    await Shot(directory,"03-closed-equipment-room.png",new(4.35f,0,.4f),0);
                    await Shot(directory,"04-seating.png",new(.3f,0,.8f),90);
                    await Shot(directory,"05-piano.png",new(0,0,2.6f),180);
                    await Shot(directory,"06-paper-worktable.png",new(2.8f,0,2.7f),180);
                    state.SolvedPuzzles.Add(DreamEscapeIds.PuzzleMusicCage);state.XiaoyuJoined=true;_room.RefreshState();visual._Process(.4);
                    _room.Companion.GlobalPosition = MusicRoomLayout3D.BeforeJoin;
                    await Shot(directory,"07-open-equipment-room.png",new(4.35f,0,.4f),0);
                    state.SolvedPuzzles.Add(DreamEscapeIds.PickupRubberMallet);_room.RefreshState();
                    Require(visual.VisiblePickupCount==4,"Picked tool remained visible.");
                    await Shot(directory,"20-tool-picked-up.png",new(-1.5f,0,-3.35f),0);
                    state.InstalledParts.Add(DreamEscapeIds.SlotMusicMetronomeWeight);_room.RefreshState();
                    Require(visual.MetronomeWeightInstalled && !visual.MetronomeSolved,"Partial metronome installation is not distinct.");
                    await Shot(directory,"21-partial-installation.png",new(0,0,2.6f),180);
                    await Shot(directory,"08-equipment-interior.png",new(4.35f,0,-2.4f),0);
                    await Shot(directory,"09-calibration.png",new(1.4f,0,-2.25f),0);
                    await Shot(directory,"10-acoustic-wall.png",new(4.2f,0,1.25f),-90);
                    await Shot(directory,"11-photo-lightbox.png",new(-4.7f,0,-.45f),90);
                    await Shot(directory,"12-exit-and-notice.png",new(-4.6f,0,3.3f),90);
                    await Shot(directory,"13-score-paper.png",new(-4.35f,0,-3.3f),0);
                    await Shot(directory,"14-tools.png",new(-1.5f,0,-3.35f),0);
                    _room.ApplyTransientState(new(false,DreamEscapeIds.PuzzleMusicChannelCalibration,[]));
                    for(var tick=0;tick<400;tick++)_room.Companion._Process(.02);
                    await Shot(directory,"15-side-observation.png",new(4.35f,0,-3.8f),90);
                    _room.ApplyTransientState(new(false,null,null));
                    ValidateStates(state);
                    await Shot(directory,"16-solved-calibration.png",new(1.4f,0,-2.25f),0);
                    await Shot(directory,"17-solved-paper.png",new(2.8f,0,2.7f),180);
                    await Shot(directory,"18-solved-lightbox.png",new(-4.7f,0,-.45f),90);
                    visual.SetCollisionDebug(true);
                    await Shot(directory,"19-collision-layout.png",new(.3f,0,.8f),135);
                    visual.SetCollisionDebug(false);
                    _room.PlayerActor.GlobalPosition=MusicRoomLayout3D.Entry;
                    for(var tick=0;tick<500;tick++)_room.Companion._Process(.025);
                    await Shot(directory,"22-companion-follow.png",MusicRoomLayout3D.Entry,-35);
                    _room.PlayerActor.Visible=false;_room.Companion.Visible=false;
                    await Shot(directory,"23-side-plate-detail.png",new(4.35f,0,-3.8f),90);
                    // Art inspection: close only the leaf transform to expose its inner plaque.
                    // Gameplay collision/state remains covered separately by the earlier phase tests.
                    var equipmentHinge=(Node3D)visual.ModelRoot.FindChild("EquipmentDoorHinge",true,false);
                    var openRotation=equipmentHinge.Rotation;
                    equipmentHinge.Rotation=new Vector3(openRotation.X,0,openRotation.Z);
                    await Shot(directory,"24-equipment-number-detail.png",new(4.35f,0,-3.8f),180);
                    equipmentHinge.Rotation=openRotation;
                    _room.PlayerActor.Visible=true;_room.Companion.Visible=true;
                    foreach (var (name,point) in new[] { ("main",new Vector3(0,0,.8f)),("equipment",new Vector3(4.35f,0,-3.15f)) })
                    {
                        _room.PlayerActor.GlobalPosition=point;_room.CameraController.SnapToTarget();await Frames(20);
                        var times=new List<double>();
                        for(var frame=0;frame<180;frame++)
                        {
                            _room.CameraController.SetYawImmediate(frame*2f);
                            var before=Time.GetTicksUsec();await Frames(1);times.Add((Time.GetTicksUsec()-before)/1000d);
                        }
                        var sorted=times.Order().ToArray();
                        measurements.Add(new {width=size.X,height=size.Y,area=name,frames=times.Count,averageMs=times.Average(),p95Ms=sorted[(int)(sorted.Length*.95)],maxMs=times.Max()});
                    }
                }
            }
            File.WriteAllText(Path.Combine(Output,DisplayServer.GetName()=="headless" || _behaviorOnly ? "behavior-metrics.json" : "metrics.json"),JsonSerializer.Serialize(new
            {
                engine=Engine.GetVersionInfo()["string"].AsString(),renderer=RenderingServer.GetCurrentRenderingMethod(),adapter=RenderingServer.GetVideoAdapterName(),
                environmentResourceLoadMs=resourceMs,roomReadyIncludingCharacterAndFiveFramesMs=readyMs,measurements,physicalRoutes=20,barriers=4,
                restoredPositions=restoredCount,markers=18,collisions=visual.CollisionBodyCount,companion=true,graybox=_graybox,
                limitations="Synthetic engine checks and screenshots; no physical input, blind playtest, target-hardware or export acceptance."
            },new JsonSerializerOptions{WriteIndented=true}));
            GD.Print($"MUSIC_V2_PROBE_PASS targets=18 routes=20 barriers=4 restores={restoredCount} companion=true graybox={_graybox}");
            GC.KeepAlive(resource);
            _room.QueueFree();
            await Frames(3);
            resource.Dispose();
            packed.Dispose();
            GC.Collect();
            GC.WaitForPendingFinalizers();
            await Frames(2);
            GetTree().Quit(0);
        }
        catch(Exception exception) { GD.PushError("MUSIC_V2_PROBE_FAIL: "+exception);GetTree().Quit(1); }
    }

    private void ValidateModel()
    {
        var visual=_room.MusicVisual;
        foreach(var spec in MusicRoomLayout3D.Collisions.Where(s=>s.Furniture))
        {
            var mesh=visual.ModelRoot.FindChild(spec.Name,true,false) as MeshInstance3D;
            Require(mesh is not null,"Missing model furniture: "+spec.Name);
            var bounds=mesh!.GlobalTransform*mesh.GetAabb();
            Require(bounds.Position.X>=spec.Position.X-spec.Size.X/2-.035f && bounds.End.X<=spec.Position.X+spec.Size.X/2+.035f &&
                bounds.Position.Z>=spec.Position.Z-spec.Size.Z/2-.035f && bounds.End.Z<=spec.Position.Z+spec.Size.Z/2+.035f,"Model escapes collision: "+spec.Name+" "+bounds);
        }
        Require(visual.CollisionBodyCount==31 && visual.FurnitureCollisionCount==16,"Unexpected music collision counts.");
        for(var x=-1.5f;x<=1.5f;x+=.15f)
        for(var z=-2.4f;z<=2.7f;z+=.15f)Require(!visual.IsBlocked(new(x,0,z)),"Central aisle narrowed.");
        var env=_room.GetNode<WorldEnvironment>("WorldEnvironment").Environment;
        Require(env.AmbientLightSource==Godot.Environment.AmbientSource.Color && env.AmbientLightSkyContribution==0,"Ambient light is not Color.");
    }

    private async Task ValidateMarkers()
    {
        Require(_room.InteractionMarkerCount==18,"Music target count changed.");
        foreach(var marker in _room.GetNode<Node3D>("InteractionMarkers").GetChildren().OfType<InteractionMarker3D>())
        {
            if(!_room.ActiveInteractionIds.Contains(marker.TargetId))continue;
            Require(!_room.MusicVisual.IsBlocked(marker.Position),"Active target is obstructed: "+marker.TargetId);
            _room.PlayerActor.GlobalPosition=marker.Position;_room._Process(0);_room.CameraController.SetInputActive(false);
            await Frames(1);
            Require(_room.CurrentInteractionTargetId==marker.TargetId,$"Target focus mismatch: {marker.TargetId} / {_room.CurrentInteractionTargetId}");
        }
        foreach(var pair in new[] {("observe:target.music.tempo_notice","target.music.metronome"),
            ("observe:target.music.fork_reference","puzzle:music_channel_calibration"),("observe:target.music.roll_template","puzzle:music_piano_roll")})
            Require(MusicRoomLayout3D.MarkerPositions[pair.Item1].DistanceTo(MusicRoomLayout3D.MarkerPositions[pair.Item2])>=2,"Clue and mechanism are too close.");
    }

    private async Task PhysicalRoute(Vector3 from,Vector3 to,bool clear)
    {
        _room.PlayerActor.GlobalPosition=from;
        await ToSignal(GetTree(),SceneTree.SignalName.PhysicsFrame);
        Require(!_room.MusicVisual.IsBlocked(from),"Invalid physical route start: "+from);
        Require(_room.PlayerActor.TestMove(_room.PlayerActor.GlobalTransform,to-from)!=clear,$"Physical route mismatch clear={clear}: {from} -> {to}");
        if(clear)Require(!_room.MusicVisual.IsBlocked(to),"Invalid physical route destination: "+to);
    }

    private async Task<int> ValidateRestores(GameSession session,PackedScene packed)
    {
        var state=session.State.DreamEscape!;
        var positions=MusicRoomLayout3D.Collisions.Where(s=>s.Furniture).Select(s=>new Vector3(s.Position.X,0,s.Position.Z))
            .Concat([MusicRoomLayout3D.BeforeJoin,new(2.8f,0,-3.4f),new(4.35f,0,-1.8f)]).ToArray();
        var count=0;
        foreach(var open in new[]{false,true})
        {
            if(open)state.SolvedPuzzles.Add(DreamEscapeIds.PuzzleMusicCage);else state.SolvedPuzzles.Remove(DreamEscapeIds.PuzzleMusicCage);
            state.XiaoyuJoined=open;
            foreach(var requested in positions)
            {
                var stored=MusicRoomLayout3D.Mapper.ToStored(requested);session.MoveDreamToArea(DreamEscapeIds.AreaMusic,stored.X,stored.Y);
                var restored=packed.Instantiate<DreamMusicRoomController3D>();AddChild(restored);await Frames(1);
                var position=restored.PlayerActor.GlobalPosition;
                Require(!restored.MusicVisual.IsBlocked(position),"Restore remains obstructed: "+requested);
                Require(position.DistanceTo(MusicRoomLayout3D.Mapper.ToWorld(new(state.PositionX,state.PositionY)))<.001f,"Corrected restore did not persist coordinates.");
                Require(restored.MusicVisual.EquipmentPassable==open,"Restore used wrong door phase.");
                if(open && requested==MusicRoomLayout3D.BeforeJoin)Require(position.DistanceTo(requested)<.001f,"Valid saved equipment-room position was discarded.");
                restored.QueueFree();await Frames(1);count++;
                if(count%8==0) { GC.Collect();GC.WaitForPendingFinalizers(); }
            }
        }
        _room.RefreshState();_room.MusicVisual._Process(.4);
        return count;
    }

    private void VerifyNavigation(Vector3 from,Vector3 to)
    {
        var position=from;
        for(var tick=0;tick<1600 && position.DistanceTo(to)>.04f;tick++)
        {
            var next=_room.MusicVisual.AdvanceCompanion(position,to,.08f);
            Require(!_room.MusicVisual.IsBlocked(next) && position.DistanceTo(next)<=.081f,"Navigation clipped or teleported.");
            position=next;
        }
        Require(position.DistanceTo(to)<.04f,$"Navigation did not arrive: {from} -> {to}, stopped={position}");
    }

    private void ValidateCompanion(GameSession session)
    {
        var companion=_room.Companion;var visual=_room.MusicVisual;
        _room.PlayerActor.GlobalPosition=new(4.35f,0,.8f);_room.PlayerActor.SetFacingImmediate(Vector3.Back);
        companion.GlobalPosition=MusicRoomLayout3D.BeforeJoin;
        for(var tick=0;tick<350;tick++)
        {
            var before=companion.GlobalPosition;companion._Process(.025);
            Require(!visual.IsBlocked(companion.GlobalPosition) && before.DistanceTo(companion.GlobalPosition)<=.086f,"Live follow clipped or teleported.");
        }
        Require(companion.GlobalPosition.Z>-.8f,"Companion did not leave equipment room through door.");
        _room.ApplyTransientState(new(false,DreamEscapeIds.PuzzleMusicChannelCalibration,[]));
        using(session.Modal.Acquire("music-calibration-probe"))
        {
            for(var tick=0;tick<350;tick++)
            {
                var before=companion.GlobalPosition;companion._Process(.025);
                Require(!visual.IsBlocked(companion.GlobalPosition) && before.DistanceTo(companion.GlobalPosition)<=.086f,"Calibration motion clipped or teleported.");
            }
        }
        Require(companion.GlobalPosition.DistanceTo(MusicRoomLayout3D.SideObservation)<.04f,"Calibration did not reach side observation.");
        _room.ApplyTransientState(new(false,null,null));
        for(var tick=0;tick<350;tick++)companion._Process(.025);
        Require(companion.GlobalPosition.Z>-.8f,"Closing calibration did not restore following.");
        _room.ApplyTransientState(new(false,DreamEscapeIds.PuzzleMusicChannelCalibration,[]));
        for(var tick=0;tick<25;tick++)companion._Process(.025);
        _room.ApplyTransientState(new(false,null,null));
        using(session.Modal.Acquire("music-inventory-interrupt-probe"))
        {
            var paused=companion.GlobalPosition;
            for(var tick=0;tick<20;tick++)companion._Process(.025);
            Require(paused.DistanceTo(companion.GlobalPosition)<.001f,"Other modal did not pause interrupted route.");
        }
        for(var tick=0;tick<350;tick++)companion._Process(.025);
        Require(companion.GlobalPosition.Z>-.8f,"Interrupted calibration did not resume following.");
        companion.GlobalPosition=new(100,0,100);companion._Process(.025);
        Require(!visual.IsBlocked(companion.GlobalPosition) && companion.GlobalPosition.DistanceTo(_room.PlayerActor.GlobalPosition)<1.5f,"Invalid companion pose did not recover safely.");
    }

    private void ValidateStates(DreamEscapeState state)
    {
        state.InstalledParts.UnionWith([DreamEscapeIds.SlotMusicMetronomeWeight,DreamEscapeIds.SlotMusicPianoRoll]);
        state.SolvedPuzzles.UnionWith([DreamEscapeIds.PuzzleMusicCage,DreamEscapeIds.PuzzleMusicScore,DreamEscapeIds.ProgressMetronomeReady,
            DreamEscapeIds.PuzzleMusicChannelCalibration,DreamEscapeIds.PuzzleMusicPianoRoll,DreamEscapeIds.PuzzleMusicAcousticWall,DreamEscapeIds.PuzzleMusicBackstagePhoto,
            DreamEscapeIds.PickupRubberMallet,DreamEscapeIds.PickupListeningCone,DreamEscapeIds.PickupMetronomeWeight,DreamEscapeIds.PickupTornPianoRoll,DreamEscapeIds.PickupWaxPatch]);
        state.XiaoyuJoined=true;_room.RefreshState();
        var v=_room.MusicVisual;
        Require(v.ScoreRevealed && v.MetronomeWeightInstalled && v.MetronomeSolved && v.TuningForksSolved && v.PianoRollInstalled &&
            v.AcousticWallSolved && v.AttendanceReaderOpen && !v.PianoBenchOpen && v.VisiblePickupCount==0,"Persistent model state mismatch.");
    }

    private async Task Frames(int count) {for(var i=0;i<count;i++)await ToSignal(GetTree(),SceneTree.SignalName.ProcessFrame);}
    private async Task Shot(string directory,string name,Vector3 position,float yaw)
    {
        Require(!_room.MusicVisual.IsBlocked(position),"Unsafe capture: "+name);
        _room.PlayerActor.GlobalPosition=position;_room._Process(0);_room.CameraController.SetInputActive(false);
        _room.CameraController.SetYawImmediate(yaw);_room.CameraController.SnapToTarget();
        _room.PlayerActor.SetFacingImmediate(_room.CameraController.HorizontalForward);await Frames(8);
        await ToSignal(RenderingServer.Singleton,RenderingServer.SignalName.FramePostDraw);
        using var image=GetViewport().GetTexture().GetImage();
        Require(image.GetSize()==DisplayServer.WindowGetSize(),"Capture dimensions differ from requested window.");
        Require(image.SavePng(Path.Combine(directory,name))==Error.Ok,"Could not write capture.");
    }
}
