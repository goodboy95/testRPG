using Godot;
using TownRpg.Presentation;

namespace TownRpg.Scripts.Testing;

/// <summary>Review-only lighting and precise world graphics. Never attached by formal routes.</summary>
public static class RefinementArt
{
    public const string Folder = "res://assets/generated/environment-refinement-v1/";
    public static IEnumerable<Node> Nodes(Node root)
    {
        yield return root;
        foreach (var c in root.GetChildren()) foreach (var n in Nodes(c)) yield return n;
    }
    public static MeshInstance3D Graphic(Node parent, string name, string graphic, Vector3 position, Vector2 size, Vector3 rotation = default)
    {
        var texture = ResourceLoader.Load<Texture2D>(Folder+"graphics/"+graphic+".png")
            ?? throw new InvalidOperationException("Missing refined graphic " + graphic);
        var material = new StandardMaterial3D {
            AlbedoTexture=texture, Roughness=.86f, Transparency=BaseMaterial3D.TransparencyEnum.AlphaScissor,
            AlphaScissorThreshold=.25f, TextureFilter=BaseMaterial3D.TextureFilterEnum.LinearWithMipmapsAnisotropic,
            CullMode=BaseMaterial3D.CullModeEnum.Disabled
        };
        var mesh = new MeshInstance3D { Name=name, Mesh=new QuadMesh { Size=size }, Position=position,
            RotationDegrees=rotation, MaterialOverride=material, CastShadow=GeometryInstance3D.ShadowCastingSetting.Off };
        parent.AddChild(mesh); return mesh;
    }
    public static void Lighting(Node root, bool indoor, bool dream=false)
    {
        foreach (var world in Nodes(root).OfType<WorldEnvironment>())
        {
            var e=(Godot.Environment)world.Environment.Duplicate(); world.Environment=e;
            e.AmbientLightColor=new Color(dream?"b6c9db":"c6d0d5");
            e.AmbientLightEnergy=indoor?.40f:.43f;
            e.TonemapMode=Godot.Environment.ToneMapper.Filmic;e.TonemapExposure=.98f;
            e.SsaoEnabled=true;e.SsaoRadius=.48f;e.SsaoIntensity=1.05f;
            if(!indoor) {e.FogEnabled=true;e.FogLightColor=new Color("c1d5d7");e.FogDensity=.0016f;}
        }
        foreach(var light in Nodes(root).OfType<DirectionalLight3D>())
        {
            light.LightColor=new Color(dream?"d5e3f1":"fff0d4");
            light.LightEnergy=dream?.38f:indoor?.55f:.44f;
            light.ShadowBlur=2.2f;
            light.DirectionalShadowMaxDistance=indoor?40:75;
        }
        foreach(var mesh in Nodes(root).OfType<MeshInstance3D>())
        {
            if(mesh.Mesh is null)continue;
            for(var i=0;i<mesh.Mesh.GetSurfaceCount();i++)
                if(mesh.GetActiveMaterial(i) is StandardMaterial3D original)
                {
                    var mat=(StandardMaterial3D)original.Duplicate();
                    mat.TextureFilter=BaseMaterial3D.TextureFilterEnum.LinearWithMipmapsAnisotropic;
                    if(mat.ResourceName.StartsWith("Refined_leaf",StringComparison.Ordinal))
                    {mat.CullMode=BaseMaterial3D.CullModeEnum.Disabled;mat.BacklightEnabled=true;mat.Backlight=new Color("29371e");}
                    mesh.SetSurfaceOverrideMaterial(i,mat);
                }
        }
    }
    public static void Home(HomePreviewController3D home)
    {
        Lighting(home,true);
        Graphic(home.ModelRoot,"Refined_ExerciseCover","notebook",new(-3.38f,.803f,.53f),new(.30f,.22f),new(-90,0,0));
        home.AddChild(new OmniLight3D {Name="Refined_WindowBounce",Position=new(-4.6f,1.9f,3.7f),
            LightColor=new Color("fff0d6"),LightEnergy=.26f,OmniRange=3.7f,ShadowEnabled=true});
    }
    public static void Village(VillageExterior3D exterior)
    {
        Lighting(exterior,false);
        var shop=exterior.ModelRoot.FindChild("Building_shop",true,false) as Node3D
            ?? throw new InvalidOperationException("Sample shop missing");
        foreach(var name in new[]{"SignBoard_shop","SignText_shop"})
            if(shop.FindChild(name,true,false) is Node3D old)old.Visible=false;
        var mounting=new StandardMaterial3D {AlbedoColor=new Color("42564d"),Roughness=.78f};
        void Support(string name,Vector3 position,Vector3 size)=>shop.AddChild(new MeshInstance3D {
            Name=name,Position=position,Mesh=new BoxMesh {Size=size},MaterialOverride=mounting});
        Support("Refined_SignBacking",new(0,2.77f,7.11f),new(2.44f,.79f,.065f));
        foreach(var x in new[]{-.86f,.86f})
        {
            Support("Refined_SignHanger",new(x,3.21f,7.10f),new(.028f,.34f,.035f));
            Support("Refined_SignBracket",new(x,3.365f,6.84f),new(.028f,.035f,.55f));
        }
        Graphic(shop,"Refined_ShopSign","shop",new(0,2.77f,7.15f),new(2.40f,.75f));
    }
    public static void Classroom(Node root)
    {
        Lighting(root,true,true);
        foreach(var state in new[]{"Missing","HandOnly","KeyOnly","Ready","Restored"})
        {
            var clock=root.FindChild("Clock_"+state,true,false) ?? throw new InvalidOperationException("Missing clock state "+state);
            Graphic(clock,"Refined_Dial_"+state,"clock",new(3.55f,2.66f,-4.245f),new(.703f,.703f));
        }
        var visual=root.FindChild("ClassroomVisual",true,false)!;
        foreach(var item in new[]{("WindowMaintenanceCard","maintenance"),("MorningSchedule","schedule"),("DutyNote","duty")})
        {
            var mesh=visual.FindChild(item.Item1,true,false) as MeshInstance3D ?? throw new InvalidOperationException(item.Item1);
            if(visual.FindChild(item.Item1+"Label",true,false) is Node3D label)label.Visible=false;
            mesh.MaterialOverride=new StandardMaterial3D {
                AlbedoTexture=ResourceLoader.Load<Texture2D>(Folder+"graphics/"+item.Item2+".png"),
                Roughness=.94f,TextureFilter=BaseMaterial3D.TextureFilterEnum.LinearWithMipmapsAnisotropic
            };
        }
    }
}
