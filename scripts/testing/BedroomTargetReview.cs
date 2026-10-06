using Godot;
using TownRpg.Presentation;

namespace TownRpg.Scripts.Testing;

/// <summary>Isolated art review. All doors/lights/input state belongs to this instance.</summary>
public partial class BedroomTargetReview : WorldArea3D
{
    [Export(PropertyHint.File,"*.tscn")] public string VisualScenePath {get;set;} = "res://scenes/testing/BedroomTargetVisual.tscn";
    public PlayerController3D Actor => Player;
    public ThirdPersonCameraRig Rig => CameraRig;
    public Node3D Visual {get;private set;} = null!;
    public Camera3D ReviewCamera {get;private set;} = null!;
    public CanvasLayer Help {get;private set;} = null!;
    public bool Walking {get;private set;}
    public bool Paused {get;private set;}
    public bool LampOn => Visual.GetNode<OmniLight3D>("DeskLight").Visible;
    public bool DoorOpen {get;private set;} = true;
    private Node3D? _door;
    private float _openAngle;
    private float _doorAngle;
    private float _previousLodThreshold;
    protected override bool InputBlocked => Paused || !Walking;
    protected override bool IsMarkerActive(InteractionMarker3D marker) => false;

    public static readonly (string Name,Vector3 Position,Vector3 Target,float Fov)[] Views =
    [
        ("doorway",new(-1.98f,1.65f,2.64f),new(.12f,1.02f,-.65f),53),
        ("bedside",new(-.15f,1.36f,.85f),new(-1.35f,.69f,-.39f),56),
        ("desk",new(.2f,1.46f,.65f),new(1.62f,.96f,-.28f),51),
        ("window",new(.6f,1.60f,-.85f),new(3.8f,1.63f,-.15f),57),
        ("reverse",new(.57f,1.62f,-1.42f),new(-.9f,1.10f,2.15f),56)
    ];

    public override void _Ready()
    {
        Visual=ResourceLoader.Load<PackedScene>(VisualScenePath).Instantiate<Node3D>();
        AddChild(Visual);ReviewCamera=Visual.GetNode<Camera3D>("ReviewCamera");
        var environment=Visual.GetNode<WorldEnvironment>("WorldEnvironment").Environment;
        environment.SsaoEnabled=true;environment.SsaoRadius=.48f;environment.SsaoIntensity=1.05f;
        _previousLodThreshold=GetViewport().MeshLodThreshold;
        GetViewport().MeshLodThreshold=0;
        _door=Visual.FindChild("BedroomDoorPivot",true,false) as Node3D;
        _openAngle=_door?.Rotation.Y ?? 0;_doorAngle=_openAngle;
        BuildCollision();
        var actor=new PlayerController3D {Name="Player",MotionMode=CharacterBody3D.MotionModeEnum.Floating};
        actor.AddChild(new CollisionShape3D {Position=new(0,.8f,0),Shape=new CapsuleShape3D {Radius=.25f,Height=1.6f}});
        AddChild(actor);
        var rig=new ThirdPersonCameraRig {Name="ThirdPersonCameraRig",FixedPitchDegrees=-15,FollowTargetHeight=1.35f};
        var arm=new SpringArm3D {Name="SpringArm3D",SpringLength=1.65f,Margin=.08f,CollisionMask=3,Shape=new SphereShape3D {Radius=.12f}};
        var socket=new Node3D {Name="CameraSocket"};
        socket.AddChild(new Camera3D {Name="Camera3D",Fov=60,Near=.045f,Far=120});
        arm.AddChild(socket);rig.AddChild(arm);AddChild(rig);AddChild(new Node3D {Name="InteractionMarkers"});
        base._Ready();
        Actor.ConfigureDefaultCharacterVisual(DreamCharacterFrames.Create(ResourceLoader.Load<Texture2D>(
            "res://assets/generated/dream_escape/pixel/protagonist-walk-4x4-v1.png")));
        ResetReview();
        Help=new CanvasLayer {Name="ReviewHelp"};AddChild(Help);
        var label=new Label {Text="卧室独立样板  ·  1–5 视角  /  Tab 走动  /  L 台灯  /  F 房门  /  Esc 鼠标  /  R 重置",Position=new(12,338)};
        label.AddThemeFontSizeOverride("font_size",8);Help.AddChild(label);
    }

    public override void _ExitTree()
    {GetViewport().MeshLodThreshold=_previousLodThreshold;base._ExitTree();}

    private void Box(string name,Vector3 p,Vector3 size,Node3D? parent=null)
    {
        var body=new StaticBody3D {Name=name,Position=p,CollisionLayer=3,CollisionMask=0};
        body.AddChild(new CollisionShape3D {Shape=new BoxShape3D {Size=size}});(parent??this).AddChild(body);
    }

    private void BuildCollision()
    {
        Box("FloorCollision",new(0,-.075f,0),new(4.6f,.15f,6.0f));
        Box("LeftWallCollision",new(-2.19f,1.45f,0),new(.18f,2.9f,4.6f));
        Box("RightWallCollision",new(2.19f,1.45f,0),new(.18f,2.9f,4.6f));
        Box("BackWallCollision",new(0,1.45f,-2.39f),new(4.44f,2.9f,.18f));
        Box("FrontWallCollision",new(.64f,1.45f,2.39f),new(2.92f,2.9f,.18f));
        Box("DoorLeftCollision",new(-2.07f,1.45f,2.39f),new(.10f,2.9f,.18f));
        Box("DoorLintelCollision",new(-1.42f,2.6f,2.39f),new(1.2f,.6f,.18f));
        Box("CeilingCollision",new(0,2.96f,0),new(4.44f,.12f,4.84f));
        Box("HallEndCollision",new(-1.4f,1.45f,3.50f),new(1.6f,2.9f,.12f));
        Box("HallLeftCollision",new(-2.25f,1.45f,2.9f),new(.1f,2.9f,1.1f));
        Box("HallRightCollision",new(-.55f,1.45f,2.9f),new(.1f,2.9f,1.1f));
        Box("BedCollision",new(-1.34f,.41f,-.35f),new(1.39f,.82f,2.14f));
        Box("WardrobeCollision",new(-.38f,1.09f,-1.97f),new(1.17f,2.18f,.58f));
        Box("DeskCollision",new(1.67f,.37f,-.3f),new(.63f,.74f,1.6f));
        Box("ChairCollision",new(.95f,.46f,-.24f),new(.46f,.92f,.47f));
        Box("BedsideTableCollision",new(-1.77f,.29f,1.35f),new(.52f,.58f,.52f));
        if(_door is not null)Box("DoorCollision",new(.54f,1.115f,0),new(1.08f,2.23f,.06f),_door);
    }

    public virtual void ResetReview()
    {
        Actor.GlobalPosition=new(-1.1f,0,1.95f);Actor.SetFacingImmediate(Vector3.Forward);
        Rig.SetYawImmediate(0);Rig.SnapToTarget();
        Paused=false;SetLamp(false);SetDoor(true,true);SetView(0);
    }

    public virtual void SetView(int index)
    {
        var v=Views[index];Walking=false;Actor.Visible=false;
        ReviewCamera.GlobalPosition=v.Position;ReviewCamera.LookAt(v.Target);ReviewCamera.Fov=v.Fov;ReviewCamera.MakeCurrent();
        SetRuntimeInputLocked(true);Input.MouseMode=Input.MouseModeEnum.Visible;
    }

    public virtual void SetWalking(bool walking)
    {
        if(!walking){SetView(0);return;}
        Walking=true;Paused=false;Actor.Visible=true;Rig.Camera.MakeCurrent();Rig.SnapToTarget();SetRuntimeInputLocked(false);
    }

    public void SetLamp(bool on) => Visual.GetNode<OmniLight3D>("DeskLight").Visible=on;

    public void SetDoor(bool open,bool immediate=false)
    {
        DoorOpen=open;
        if(immediate && _door is not null){_doorAngle=open?_openAngle:0;_door.Rotation=new(0,_doorAngle,0);}
    }

    public override void _PhysicsProcess(double delta)
    {
        if(_door is null)return;
        _doorAngle=Mathf.MoveToward(_doorAngle,DoorOpen?_openAngle:0,(float)delta*2.1f);
        _door.Rotation=new(0,_doorAngle,0);
    }

    public override void _UnhandledInput(InputEvent e)
    {
        if(e is not InputEventKey {Pressed:true,Echo:false} key)return;
        if(key.Keycode is >= Key.Key1 and <= Key.Key5)SetView((int)(key.Keycode-Key.Key1));
        if(key.Keycode==Key.Tab)SetWalking(!Walking);
        if(key.Keycode==Key.L)SetLamp(!LampOn);
        if(key.Keycode==Key.F && (!Walking || Actor.GlobalPosition.DistanceTo(new(-1.42f,0,2.3f))<1.5f))SetDoor(!DoorOpen);
        if(key.Keycode==Key.R)ResetReview();
        if(key.Keycode==Key.Escape && Walking)
        {Paused=!Paused;SetRuntimeInputLocked(Paused);Input.MouseMode=Paused?Input.MouseModeEnum.Visible:Input.MouseModeEnum.Captured;}
    }
}
