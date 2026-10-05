using Godot;
using System.Security.Cryptography;
using System.Text.Json;

namespace TownRpg.Scripts.Testing;

/// <summary>Offline asset preparation only. No gameplay state or character assets.</summary>
public partial class BedroomTargetPrepare : Node
{
    public override void _Ready() => CallDeferred(MethodName.Prepare);

    private static IEnumerable<Node> Nodes(Node n)
    {
        yield return n;
        foreach (var c in n.GetChildren()) foreach (var child in Nodes(c)) yield return child;
    }

    private void Prepare()
    {
        try
        {
            var gray = OS.GetCmdlineUserArgs().Contains("--bedroom-gray");
            const string directory = "res://assets/generated/bedroom-target-v2/";
            var modelPath=directory+(gray?"bedroom-gray.glb":"bedroom-target-v2.glb");
            var sourceHash=Convert.ToHexString(SHA256.HashData(File.ReadAllBytes(ProjectSettings.GlobalizePath(modelPath))));
            var cachePath=ProjectSettings.GlobalizePath(directory+(gray?"gray-unwrap-cache.json":"unwrap-cache.json"));
            var reuse=false;
            if(File.Exists(cachePath))
            {
                using var cache=JsonDocument.Parse(File.ReadAllText(cachePath));
                reuse=cache.RootElement.GetProperty("sourceSha256").GetString()==sourceHash;
            }
            var model = ResourceLoader.Load<PackedScene>(modelPath).Instantiate<Node3D>();
            // Save a fully owned local hierarchy. Retaining SceneFilePath while
            // changing descendant ownership packs both the imported instance and
            // new children, duplicating every mesh (including shadow-casting glass).
            model.SceneFilePath=string.Empty;
            var root = new Node3D { Name = "BedroomTargetVisual" }; AddChild(root);
            model.Name = "BedroomModel"; root.AddChild(model);
            var meshes = 0;
            foreach (var n in Nodes(model))
            {
                n.Owner = root;
                if (n is not MeshInstance3D mesh) continue;
                var glass = mesh.Name.ToString().StartsWith("Glass_");
                var exterior = mesh.Name.ToString().StartsWith("Exterior_");
                var dynamic = mesh.GetParent().Name == "BedroomDoorPivot";
                mesh.GIMode = dynamic ? GeometryInstance3D.GIModeEnum.Dynamic : GeometryInstance3D.GIModeEnum.Static;
                if (glass) {mesh.GIMode = GeometryInstance3D.GIModeEnum.Disabled; mesh.CastShadow = GeometryInstance3D.ShadowCastingSetting.Off; continue;}
                if (dynamic) continue;
                if (exterior){mesh.GIMode=GeometryInstance3D.GIModeEnum.Disabled;continue;}
                var data = (ArrayMesh)mesh.Mesh.Duplicate();
                data.ShadowMesh=null;
                var path = directory + $"meshes/{mesh.Name}.res";
                // Godot xatlas produces UV2 plus the correct per-mesh lightmap size hint.
                if(reuse && ResourceLoader.Exists(path))
                {
                    var cached=ResourceLoader.Load<ArrayMesh>(path);
                    var clean=new ArrayMesh {LightmapSizeHint=cached.LightmapSizeHint};
                    for(var i=0;i<cached.GetSurfaceCount();i++)
                    {clean.AddSurfaceFromArrays(cached.SurfaceGetPrimitiveType(i),cached.SurfaceGetArrays(i));clean.SurfaceSetMaterial(i,data.SurfaceGetMaterial(i));}
                    data=clean;
                }
                else
                {
                    var result = data.LightmapUnwrap(mesh.GlobalTransform, gray ? .08f : .045f);
                    if (result != Error.Ok) throw new InvalidOperationException($"UV2 unwrap {mesh.Name}: {result}");
                }
                // UV2 unwrap may remap vertices. Persist full-detail surfaces without
                // imported LOD index buffers that referred to the previous vertex order.
                var stable=new ArrayMesh {LightmapSizeHint=data.LightmapSizeHint};
                for(var i=0;i<data.GetSurfaceCount();i++)
                {stable.AddSurfaceFromArrays(data.SurfaceGetPrimitiveType(i),data.SurfaceGetArrays(i));stable.SurfaceSetMaterial(i,data.SurfaceGetMaterial(i));}
                data=stable;
                for (var i=0;i<data.GetSurfaceCount();i++)
                    if (data.SurfaceGetMaterial(i) is BaseMaterial3D material)
                        material.TextureFilter = BaseMaterial3D.TextureFilterEnum.LinearWithMipmapsAnisotropic;
                Directory.CreateDirectory(ProjectSettings.GlobalizePath(directory + "meshes"));
                if (ResourceSaver.Save(data,path,ResourceSaver.SaverFlags.Compress) != Error.Ok) throw new IOException(path);
                data.TakeOverPath(path);
                mesh.Mesh = data; meshes++;
            }
            var environment = new Godot.Environment
            {
                BackgroundMode = Godot.Environment.BGMode.Sky,
                Sky = new Sky {SkyMaterial = new ProceduralSkyMaterial {
                    SkyTopColor = new("87b7d6"), SkyHorizonColor = new("d1dfe0"),
                    GroundBottomColor = new("708179"),GroundHorizonColor = new("c4cfca"),SunAngleMax=1.5f}},
                AmbientLightSource=Godot.Environment.AmbientSource.Color, AmbientLightColor = new("a1b7cd"),
                AmbientLightEnergy=.5f, AmbientLightSkyContribution=0,
                ReflectedLightSource=Godot.Environment.ReflectionSource.Sky,
                TonemapMode=Godot.Environment.ToneMapper.Filmic, TonemapWhite=4.0f, TonemapExposure=1.1f
            };
            Add(root, new WorldEnvironment {Name="WorldEnvironment",Environment=environment});
            // Rays enter the right window, falling toward the left/back wall.
            var sun = new DirectionalLight3D {Name="MorningSun", LightColor=new("ffe8c4"), LightEnergy=.65f,
                ShadowEnabled=true, ShadowBias=.06f, ShadowNormalBias=1.0f,ShadowBlur=3.0f,
                DirectionalShadowMaxDistance=16, LightBakeMode=Light3D.BakeMode.Dynamic};
            Add(root,sun);sun.LookAtFromPosition(new(10,3.8f,-2.5f),Vector3.Zero);
            // A broad, cool source outside the window approximates diffuse skylight.
            // Static direct + bounce lighting is baked, with occlusion and penumbra.
            Add(root,new OmniLight3D {Name="WindowDiffuse",Position=new(2.75f,1.95f,-.4f),LightColor=new("d8e7ff"),
                LightEnergy=2.4f,OmniRange=8,LightSize=1.0f,ShadowEnabled=true,LightBakeMode=Light3D.BakeMode.Static});
            var gi=new LightmapGI {Name="LightmapGI", Bounces=4, Interior=true,BounceIndirectEnergy=1.5f,
                Quality=LightmapGI.BakeQuality.High, Directional=false, MaxTextureSize=4096,
                EnvironmentMode=LightmapGI.EnvironmentModeEnum.CustomSky,EnvironmentCustomSky=environment.Sky,
                GenerateProbesSubdiv=LightmapGI.GenerateProbes.Subdiv8, UseDenoiser=true,Bias=.0005f};
            Add(root,gi);
            // Real-time practical lights are intentionally excluded from the bake.
            Add(root,new OmniLight3D {Name="DeskLight",Position=new(1.58f,1.14f,.04f),LightColor=new("ffdfae"),
                LightEnergy=.55f,OmniRange=2.3f,ShadowEnabled=true,Visible=false,LightBakeMode=Light3D.BakeMode.Disabled});
            Add(root,new ReflectionProbe {Name="RoomReflection",Position=new(0,1.35f,0),Size=new(4.1f,2.65f,4.5f),
                Interior=true,BoxProjection=true,MaxDistance=12,UpdateMode=ReflectionProbe.UpdateModeEnum.Once,Intensity=.45f});
            foreach(var p in new[]{new Vector3(-1.0f,.8f,.5f),new Vector3(.15f,1.1f,-.5f),new Vector3(1.2f,1.1f,-.2f),new Vector3(-1.5f,1.5f,1.8f)})
                Add(root,new LightmapProbe {Position=p});
            Add(root,new Camera3D {Name="ReviewCamera",Position=new(-1.72f,1.62f,2.16f),Fov=53,Near=.05f,Far=120,Current=true});
            root.GetNode<Camera3D>("ReviewCamera").LookAt(new(.1f,1.05f,-.85f));
            var scene = new PackedScene();if(scene.Pack(root)!=Error.Ok)throw new IOException("Scene pack failed");
            if(ResourceSaver.Save(scene,"res://scenes/testing/BedroomTargetVisual.tscn")!=Error.Ok)throw new IOException("Scene save failed");
            File.WriteAllText(cachePath,JsonSerializer.Serialize(new{sourceSha256=sourceHash,texelMetres=gray?.08f:.045f,meshes,
                note="Invalidate by exact GLB bytes; material surfaces are refreshed from source on reuse."},new JsonSerializerOptions{WriteIndented=true}));
            GD.Print($"BEDROOM_PREPARE_PASS gray={gray} meshes={meshes} reusedUV2={reuse}");GetTree().Quit();
        }
        catch(Exception e){GD.PushError(e.ToString());GetTree().Quit(1);}
    }

    private static void Add(Node root,Node child) {root.AddChild(child);child.Owner=root;}
}
