using Godot;
using TownRpg.Presentation;

namespace TownRpg.Scripts.Testing;

public partial class EnvironmentRefinementPreview : Node
{
    public Node? Area { get; private set; }
    public string AreaKind { get; private set; }="home";
    public bool Baseline { get; private set; }
    public bool Switching { get; private set; }
    private Label _title=null!;
    public PlayerController3D Actor => Area is RefinementVillage v?v.ActivePlayer:((RefinementClassroom)Area!).PlayerActor;
    public ThirdPersonCameraRig Rig => Area is RefinementVillage v?v.ActiveCamera:((RefinementClassroom)Area!).CameraController;
    public override async void _Ready()
    {
        var layer=new CanvasLayer {Layer=20};AddChild(layer);
        _title=new Label {Position=new(12,8),Theme=UiStyles.CreateTheme(1,cinematic:true),MouseFilter=Control.MouseFilterEnum.Ignore};layer.AddChild(_title);
        Baseline=OS.GetCmdlineUserArgs().Contains("--refinement-baseline");
        AreaKind=OS.GetCmdlineUserArgs().FirstOrDefault(a=>a.StartsWith("--refinement-area="))?.Split('=')[1]??"home";
        try { await Show(AreaKind); }
        catch(Exception e){GD.PushError(e.ToString());GetTree().Quit(1);}
    }
    public async Task Show(string kind)
    {
        if(Switching)return;
        if(kind is not ("home" or "village" or "classroom"))throw new ArgumentException(kind);
        Switching=true;AreaKind=kind;
        try
        {
            if(Area is not null){RemoveChild(Area);Area.Free();Area=null;await ToSignal(GetTree(),SceneTree.SignalName.ProcessFrame);}
            if(kind=="classroom")
            {
                var scene=ResourceLoader.Load<PackedScene>("res://scenes/testing/RefinementClassroom.tscn").Instantiate<RefinementClassroom>();
                scene.Baseline=Baseline;
                if(Baseline)
                {
                    var old=scene.GetNode("ClassroomModel");scene.RemoveChild(old);old.Free();
                    var model=ResourceLoader.Load<PackedScene>("res://assets/generated/dream_escape/3d/classroom-art-pass-v6.glb").Instantiate<Node3D>();
                    model.Name="ClassroomModel";scene.AddChild(model);scene.MoveChild(model,0);
                }
                Area=scene;AddChild(Area);
            }
            else
            {
                var village=new RefinementVillage {Baseline=Baseline};Area=village;AddChild(village);
                if(kind=="home")
                {
                    await village.SwitchArea(true);
                    village.ActivePlayer.GlobalPosition=new(-2.6f,0,2.7f);village.ActivePlayer.SetFacingImmediate(Vector3.Left);
                    village.ActiveCamera.SetYawImmediate(90);village.ActiveCamera.SnapToTarget();
                }
                else
                {
                    village.ActivePlayer.GlobalPosition=village.Exterior.SurfacePoint(-20,28,.04f);
                    village.ActivePlayer.SetFacingImmediate(Vector3.Forward);
                    village.ActiveCamera.SetYawImmediate(0);village.ActiveCamera.SnapToTarget();
                }
            }
            _title.Text=$"环境精修 · {(Baseline?"原版对照":"独立样板")}　1 卧室　2 街景　3 教室　B 新旧切换"+
                (kind=="classroom"?"　T 表盘状态　Esc 释放鼠标":"");
        }
        finally {Switching=false;}
    }
    public override async void _Input(InputEvent e)
    {
        if(Switching || e is not InputEventKey {Pressed:true,Echo:false} key)return;
        var kind=key.Keycode switch {Key.Key1=>"home",Key.Key2=>"village",Key.Key3=>"classroom",_=>null};
        if(key.Keycode==Key.B)
        {
            GetViewport().SetInputAsHandled();
            try {await ToggleBaseline();} catch(Exception error){GD.PushError(error.ToString());GetTree().Quit(1);}
            return;
        }
        if(kind is null)return;
        GetViewport().SetInputAsHandled();
        try {await Show(kind);} catch(Exception error){GD.PushError(error.ToString());GetTree().Quit(1);}
    }
    public async Task ToggleBaseline()
    {
        if(Switching || Area is null)return;
        var kind=Area is RefinementVillage village?(village.IsInterior?"home":"village"):"classroom";
        var transform=Actor.GlobalTransform;var yaw=Rig.YawDegrees;
        Baseline=!Baseline;await Show(kind);
        Actor.GlobalTransform=transform;Rig.SetYawImmediate(yaw);Rig.SnapToTarget();
    }
}
