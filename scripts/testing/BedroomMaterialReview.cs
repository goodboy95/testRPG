using Godot;

namespace TownRpg.Scripts.Testing;

/// <summary>Material-only A/B uses one mesh, one baked GI and fixed reflection capture.</summary>
public partial class BedroomMaterialReview : BedroomTargetReview
{
    public const string AssetDirectory = "res://assets/generated/bedroom-material-v1/";
    public bool Refined { get; private set; } = true;
    public bool Neutral { get; private set; }
    public static readonly string[] ViewNames = ["doorway","bedside","desk","window","reverse","desk-surface","wardrobe-front"];
    private readonly List<(MeshInstance3D Mesh, Material New, Material Old)> _furniture = [];
    private Node3D _neutralRoot = null!;
    private Godot.Environment _roomEnvironment = null!;
    private Godot.Environment _neutralEnvironment = null!;
    private bool _initialized;
    private int _view;
    private Material? _checker;

    public BedroomMaterialReview() => VisualScenePath = "res://scenes/testing/BedroomMaterialVisual.tscn";

    public override void _Ready()
    {
        base._Ready();
        _roomEnvironment = Visual.GetNode<WorldEnvironment>("WorldEnvironment").Environment;
        _neutralEnvironment = new Godot.Environment {
            BackgroundMode=Godot.Environment.BGMode.Color,BackgroundColor=new("777777"),
            AmbientLightSource=Godot.Environment.AmbientSource.Color,AmbientLightColor=Colors.White,AmbientLightEnergy=.25f,
            ReflectedLightSource=Godot.Environment.ReflectionSource.Sky,
            Sky=new Sky {SkyMaterial=new ProceduralSkyMaterial {SkyTopColor=new("bbbbbb"),SkyHorizonColor=new("888888"),GroundBottomColor=new("777777"),GroundHorizonColor=new("888888")}},
            TonemapMode=Godot.Environment.ToneMapper.Linear,TonemapExposure=1
        };
        _neutralRoot = new Node3D {Name="NeutralMaterialCheck",Visible=false}; AddChild(_neutralRoot);
        var light=new DirectionalLight3D {Name="WhiteRakingLight",LightColor=Colors.White,LightEnergy=.65f,ShadowEnabled=true,DirectionalShadowMaxDistance=12};
        _neutralRoot.AddChild(light); light.RotationDegrees=new(-35,-55,0);
        foreach(var kind in new[]{"Desk","Wardrobe"})
        {
            var mesh=Visual.GetNode<MeshInstance3D>("BedroomModel/Furniture_"+kind);
            var old=new StandardMaterial3D {
                AlbedoTexture=ResourceLoader.Load<Texture2D>(AssetDirectory+kind.ToLowerInvariant()+"-old-color.png"),
                RoughnessTexture=ResourceLoader.Load<Texture2D>(AssetDirectory+kind.ToLowerInvariant()+"-old-orm.png"),
                RoughnessTextureChannel=BaseMaterial3D.TextureChannel.Green,Roughness=1,Metallic=0,
                CullMode=BaseMaterial3D.CullModeEnum.Back,TextureRepeat=false,
                TextureFilter=BaseMaterial3D.TextureFilterEnum.LinearWithMipmapsAnisotropic
            };
            var material=mesh.Mesh.SurfaceGetMaterial(0);
            if(material is BaseMaterial3D standard)standard.TextureRepeat=false;
            _furniture.Add((mesh,material,old));
            var clone=new MeshInstance3D {Name=kind+"Neutral",Mesh=mesh.Mesh,GIMode=GeometryInstance3D.GIModeEnum.Disabled,
                Position=kind=="Desk"?new(-2.76f,0,.3f):new(1.48f,0,1.97f)};
            _neutralRoot.AddChild(clone);_furniture.Add((clone,material,old));
        }
        _initialized=true; SetRefined(true); UpdateHelp();
    }

    public void SetRefined(bool refined)
    {
        Refined=refined;
        foreach(var item in _furniture)item.Mesh.SetSurfaceOverrideMaterial(0,refined?item.New:item.Old);
        if(_initialized)UpdateHelp();
    }

    public void SetUvCheck(bool enabled)
    {
        if(!enabled){SetRefined(Refined);return;}
        _checker??=new StandardMaterial3D {AlbedoTexture=ResourceLoader.Load<Texture2D>(AssetDirectory+"uv-checker.png"),
            Roughness=.85f,TextureRepeat=false,TextureFilter=BaseMaterial3D.TextureFilterEnum.LinearWithMipmapsAnisotropic};
        foreach(var item in _furniture)item.Mesh.SetSurfaceOverrideMaterial(0,_checker);
    }

    public void SetNeutral(bool neutral)
    {
        if(!_initialized)return;
        Neutral=neutral; Visual.Visible=!neutral; _neutralRoot.Visible=neutral;
        Visual.GetNode<WorldEnvironment>("WorldEnvironment").Environment=neutral?_neutralEnvironment:_roomEnvironment;
        if(neutral)
        {
            base.SetView(0);ReviewCamera.LookAtFromPosition(new(-3.4f,2.2f,4.5f),new(.05f,1,0));ReviewCamera.Fov=46;
        }
        else SetView(_view);
        UpdateHelp();
    }

    public override void SetView(int index)
    {
        if(_initialized && Neutral){Neutral=false;Visual.Visible=true;_neutralRoot.Visible=false;Visual.GetNode<WorldEnvironment>("WorldEnvironment").Environment=_roomEnvironment;}
        _view=index;
        base.SetView(index<5?index:0);
        if(index==5){ReviewCamera.LookAtFromPosition(new(.83f,1.15f,.35f),new(1.66f,.755f,-.3f));ReviewCamera.Fov=43;}
        if(index==6){ReviewCamera.LookAtFromPosition(new(.27f,1.53f,-.43f),new(-.38f,1.27f,-1.66f));ReviewCamera.Fov=48;}
    }

    public override void SetWalking(bool walking)
    {if(_initialized && Neutral)SetNeutral(false);base.SetWalking(walking);}

    public override void ResetReview()
    {if(_initialized){SetNeutral(false);SetRefined(true);}base.ResetReview();}

    public override void _UnhandledInput(InputEvent e)
    {
        if(e is InputEventKey {Pressed:true,Echo:false} key)
        {
            if(key.Keycode==Key.V){SetRefined(!Refined);return;}
            if(key.Keycode==Key.N){SetNeutral(!Neutral);return;}
            if(key.Keycode==Key.Key6){SetView(5);return;}
            if(key.Keycode==Key.Key7){SetView(6);return;}
        }
        base._UnhandledInput(e);
    }

    private void UpdateHelp()
    {
        var label=Help.GetChild<Label>(0);
        label.Position=new(12,328);
        label.Text=$"木家具材质 · {(Refined?"新材质":"旧材质（共用新烘焙间接光）")}\nV 对照 / N 中性光 / 1–7 视角 / Tab 走动 / L 台灯 / F 房门 / Esc 鼠标 / R 重置";
    }
}
