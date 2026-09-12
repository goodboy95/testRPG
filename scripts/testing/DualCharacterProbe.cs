using Godot;
using System.Text.Json;
using TownRpg.Application;
using TownRpg.Core.Domain;
using TownRpg.Presentation;

namespace TownRpg.Testing;

public partial class DualCharacterProbe : Node
{
    private bool _capture;
    private string _directory = "";
    private readonly List<object> _results = [];
    private Label _label = null!;
    public override void _Ready() => CallDeferred(MethodName.Run);
    private static void Require(bool ok, string message) { if (!ok) throw new InvalidOperationException(message); }
    private async Task Frames(int n = 2) { for (var i=0;i<n;i++) await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame); }
    private async Task Shot(string name)
    {
        if (!_capture) return;
        await ToSignal(RenderingServer.Singleton, RenderingServer.SignalName.FramePostDraw);
        using var image = GetViewport().GetTexture().GetImage();
        Require(image.SavePng(Path.Combine(_directory,name+".png")) == Error.Ok,"Cannot save capture");
    }
    private async void Run()
    {
        var args = OS.GetCmdlineUserArgs();
        _capture = args.Contains("--dual-capture");
        var before = args.Contains("--before");
        var width = int.Parse(args.FirstOrDefault(a=>a.StartsWith("--width="))?[8..] ?? "1280");
        _directory=ProjectSettings.GlobalizePath($"res://outputs/character-motion-v3/{(before?"before":"after")}-{width}");
        Directory.CreateDirectory(_directory);
        var session=GetNode<GameSession>("/root/GameSession");
        using var lease=session.SuppressPersistence();
        try
        {
            if(_capture) { DisplayServer.WindowSetSize(new(width,width*9/16));await Frames(10); }
            var layer=new CanvasLayer();AddChild(layer);
            _label=new Label { Position=new(14,12), Text="双角色动作检查" };layer.AddChild(_label);
            _label.AddThemeFontSizeOverride("font_size",16);
            var areas=new[]{("DreamClassroom",DreamEscapeIds.AreaClassroom), ("DreamMusicRoom",DreamEscapeIds.AreaMusic),
                ("DreamArchiveRoom",DreamEscapeIds.AreaArchive),("DreamAuditorium",DreamEscapeIds.AreaAuditorium),("DreamCorridor",DreamEscapeIds.AreaCorridor)};
            foreach(var (scene,id) in areas)
            {
                session.NewGame("动作验收","同学");var dream=session.State.DreamEscape!;
                dream.XiaoyuJoined=true;dream.UnlockedAreas.UnionWith(areas.Select(a=>a.Item2));
                dream.SolvedPuzzles.Add(DreamEscapeIds.PuzzleMusicCage);
                var route=DreamAreaSpawnCatalog.Resolve(id==DreamEscapeIds.AreaCorridor?DreamEscapeIds.AreaClassroom:DreamEscapeIds.AreaCorridor,id);
                session.MoveDreamToArea(id,route.StoredPosition.X,route.StoredPosition.Y);
                var area=ResourceLoader.Load<PackedScene>($"res://scenes/dream/{scene}.tscn").Instantiate<WorldArea3D>();AddChild(area);await Frames(4);
                try
                {
                    var player=area.GetNode<PlayerController3D>("Player");var companion=area.GetNode<DreamCompanion3D>("XiaoyuCompanion");
                    var camera=area.GetNode<ThirdPersonCameraRig>("ThirdPersonCameraRig");
                    area.SetProcess(false);player.SetPhysicsProcess(false);companion.SetProcess(false);camera.SetInputActive(false);
                    var resolve=companion.FollowPositionResolver!;var blocked=companion.FollowPositionIsBlocked!;
                    player.GlobalPosition=resolve(new(0,0,id==DreamEscapeIds.AreaCorridor ? -2 : 2.4f));
                    player.SetFacingImmediate(Vector3.Forward);companion.RefreshImmediate(force:true);
                    camera.SetYawImmediate(125);camera.SnapToTarget();
                    if(before)
                    {
                        player.ConfigureDefaultCharacterVisual(DreamCharacterFrames.Create(ResourceLoader.Load<Texture2D>("res://assets/generated/dream_escape/pixel/protagonist-walk-4x4-v1.png")),"res://tests/xiaoyu-model-runtime/caelus-before.glb");
                        companion.ConfigureCharacterVisual("res://tests/missing-xiaoyu.glb");
                    }
                    else
                    {
                        Require(!ResourceLoader.Exists(DreamCompanion3D.DefaultModelPath)||companion.CharacterModel is not null,"Local Xiaoyu model rejected");
                        if(companion.CharacterModel is not null) Require(!companion.Sprite.Visible,"Billboard remained visible");
                    }
                    async Task Tick(int count,string label)
                    {
                        _label.Text=$"{scene} · {label} · {(before?"旧资产对照 / 当前路线":"双角色新版")}";
                        for(var i=0;i<count;i++)
                        {
                            var previous=companion.GlobalPosition;
                            player._PhysicsProcess(1d/60);companion._Process(1d/60);
                            Require(!blocked(companion.GlobalPosition),$"{scene} companion crossed obstacle: {previous} -> {companion.GlobalPosition}, phase={label}");
                            Require(previous.DistanceTo(companion.GlobalPosition)<.06f,scene+" companion teleported");
                            if(_capture)await Frames(1);else if(i%120==0)await Frames(1);
                        }
                    }
                    await Tick(90,"待机正面");await Shot(scene+"-idle-front");
                    camera.SetYawImmediate(0);camera.SnapToTarget();await Tick(60,"待机背面");await Shot(scene+"-idle-back");
                    foreach(var (action,slow,count,label) in new[]{("move_up",true,75,"walk"),("",false,30,"stop"),("move_down",false,65,"reverse"),("",false,30,"stop-after-jog"),("move_right",true,40,"turn90")})
                    {
                        if(slow)Input.ActionPress("move_slow");if(action.Length>0)Input.ActionPress(action);
                        await Tick(count,label);await Shot(scene+"-"+label);
                        if(action.Length>0)Input.ActionRelease(action);Input.ActionRelease("move_slow");
                    }
                    await Tick(120,"停止跟随");
                    var armLength = camera.SpringArm.SpringLength;
                    camera.SpringArm.SpringLength = .45f;
                    await Frames(8);await Tick(8,"近镜头");await Shot(scene+"-near-camera");
                    if(player.CharacterModel is not null)Require(!player.CharacterModel.Visible,"Near camera did not hide player");
                    camera.SpringArm.SpringLength = armLength;
                    await Frames(8);await Tick(8,"恢复镜头");
                    player.InputEnabled=false;
                    foreach(var pose in new[]{"observe","pickup","operate","talk"})
                    {
                        player.PlayInteractionPose(pose,player.GlobalPosition+Vector3.Forward);
                        if(pose=="talk")companion.PlayConversationPose(player.GlobalPosition);
                        await Tick(30,pose);await Shot(scene+"-"+pose);await Tick(110,pose+"结束");
                    }
                    player.InputEnabled=true;
                    if(id is DreamEscapeIds.AreaMusic or DreamEscapeIds.AreaAuditorium or DreamEscapeIds.AreaArchive)
                    {
                        var destination=id==DreamEscapeIds.AreaMusic?MusicRoomLayout3D.SideObservation:id==DreamEscapeIds.AreaArchive?ArchiveRoomLayout3D.RecorderCompanion:AuditoriumLayout3D.LeftLeverCompanion;
                        var pose=id==DreamEscapeIds.AreaAuditorium?"hold":id==DreamEscapeIds.AreaArchive?"operate":"observe";
                        var facing=id==DreamEscapeIds.AreaMusic?Vector3.Left:id==DreamEscapeIds.AreaArchive?Vector3.Right:Vector3.Forward;
                        companion.SetHeldPose(true,destination,pose,facing);
                        for(var i=0;i<2400 && !companion.PoseArrived;i++)await Tick(1,"走向协作位置");
                        Require(companion.PoseArrived,scene+" did not reach cooperation");
                        player.GlobalPosition=resolve(destination+new Vector3(1.4f,0,1.8f));camera.SetYawImmediate(-40);camera.SnapToTarget();
                        Camera3D? reviewCamera = null;
                        if (_capture)
                        {
                            reviewCamera = new Camera3D { Fov = 55, Near = .05f };
                            area.AddChild(reviewCamera);
                            reviewCamera.GlobalPosition = destination + (id==DreamEscapeIds.AreaMusic ? new Vector3(1.0f,1.6f,1.15f) : new Vector3(2.1f,1.8f,1.0f));
                            reviewCamera.LookAt(destination + Vector3.Up * .9f);
                            reviewCamera.Current = true;
                            player.Visible = false;
                        }
                        await Tick(70,"协作姿态");await Shot(scene+"-cooperate");
                        if(reviewCamera is not null) { reviewCamera.QueueFree();camera.Camera.Current=true;player.Visible=true; }
                        if(companion.CharacterModel is not null)Require(companion.AnimationState==pose,"Pose animation missing");
                        companion.SetHeldPose(false,destination);Require(companion.RequestedPose is null,"Cancel retained request");
                        using(session.Modal.Acquire("dual-cancel"))
                        {
                            var position=companion.GlobalPosition;await Tick(30,"取消协作/模态");
                            Require(companion.GlobalPosition==position,"Modal moved companion");
                        }
                    }
                    var stable=player.GlobalPosition;
                    player.Notification((int)NotificationWMWindowFocusOut);companion.Notification((int)NotificationWMWindowFocusOut);
                    Input.ActionPress("move_up");await Tick(15,"失焦");Input.ActionRelease("move_up");
                    Require(player.GlobalPosition==stable && companion.RequestedPose is null,"Focus reset failed");
                    player.Notification((int)NotificationWMWindowFocusIn);companion.Notification((int)NotificationWMWindowFocusIn);
                    if(!before)
                    {
                        companion.ConfigureCharacterVisual("res://tests/missing-xiaoyu.glb");
                        Require(companion.CharacterModel is null && companion.Sprite.Visible,"Xiaoyu fallback failed");
                        companion.ConfigureCharacterVisual();
                    }
                    _results.Add(new{scene,model=companion.CharacterModel is not null,status="PASS"});
                    GD.Print("DUAL_AREA_PASS "+scene);
                }
                finally {area.QueueFree();await Frames(3);GC.Collect();}
            }
            File.WriteAllText(Path.Combine(_directory,"report.json"),JsonSerializer.Serialize(_results,new JsonSerializerOptions{WriteIndented=true}));
            GD.Print("DUAL_CHARACTER_PASS areas=5 routes poses modal focus fallback");GetTree().Quit();
        }
        catch(Exception e){GD.PushError("DUAL_CHARACTER_FAIL "+e);GetTree().Quit(1);}
        finally{foreach(var a in new[]{"move_up","move_down","move_left","move_right","move_slow"})Input.ActionRelease(a);}
    }
}
