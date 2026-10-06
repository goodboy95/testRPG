using Godot;
using System.Text.Json;
using System.Security.Cryptography;
using TownRpg.Application;

namespace TownRpg.Scripts.Testing;

public partial class BedroomTargetProbe : Node
{
    private BedroomTargetReview _review=null!;
    private readonly List<string> _checks=[];
    private readonly List<double> _walkingFrames=[];
    private bool _sampleWalking;
    private ulong _lastWalkingTick;
    private ulong _walkingTexturePeak;
    private ulong _walkingVideoPeak;
    private ulong _walkingDrawPeak;
    private static Dictionary<string,string> Saves()
    {
        var path=ProjectSettings.GlobalizePath("user://");
        return Directory.Exists(path)?Directory.GetFiles(path,"*.json",SearchOption.AllDirectories)
            .ToDictionary(p=>p,p=>Convert.ToHexString(SHA256.HashData(File.ReadAllBytes(p)))):new();
    }
    private void Require(bool b,string s){if(!b)throw new InvalidOperationException(s);_checks.Add(s);}
    private async Task Frames(int n=10){for(var i=0;i<n;i++)await ToSignal(GetTree(),SceneTree.SignalName.ProcessFrame);}
    public override void _Ready()=>CallDeferred(MethodName.Run);
    private static void Release(){foreach(var a in new[]{"move_up","move_down","move_left","move_right"})Input.ActionRelease(a);}
    private async Task Walk(Vector3 destination)
    {
        _review.Actor.Notification((int)NotificationWMWindowFocusIn);
        for(var i=0;i<900;i++)
        {
            var d=destination-_review.Actor.GlobalPosition;d.Y=0;
            if(d.Length()<.12f){Release();Require(true,"walk reached "+destination);return;}
            // Synthetic stick strength must remain above the project's deadzone.
            var strength=Mathf.Clamp(d.Length()*1.5f,.65f,1);d=d.Normalized();
            var f=_review.Rig.HorizontalForward;var r=f.Cross(Vector3.Up);
            Input.ActionPress("move_up",Math.Max(0,d.Dot(f))*strength);Input.ActionPress("move_down",Math.Max(0,-d.Dot(f))*strength);
            Input.ActionPress("move_left",Math.Max(0,-d.Dot(r))*strength);Input.ActionPress("move_right",Math.Max(0,d.Dot(r))*strength);
            await ToSignal(GetTree(),SceneTree.SignalName.PhysicsFrame);
        }
        Release();throw new InvalidOperationException($"Walk blocked {_review.Actor.GlobalPosition} -> {destination}");
    }
    private async void Run()
    {
        var session=GetNode<GameSession>("/root/GameSession");var before=JsonSerializer.Serialize(session.State);var saves=Saves();
        try
        {
            var gray=OS.GetCmdlineUserArgs().Contains("--bedroom-gray");
            var captureOnly=OS.GetCmdlineUserArgs().Contains("--capture-only");
            var material=OS.GetCmdlineUserArgs().Contains("--bedroom-material");
            var textureBytes=0L;
            var textureFormats=new Dictionary<string,string>();
            var assetDirectory=material?BedroomMaterialReview.AssetDirectory:"res://assets/generated/bedroom-target-v2/";
            var directory=ProjectSettings.GlobalizePath((material?"res://outputs/bedroom-material-v1/":"res://outputs/bedroom-target-v2/")+DateTime.Now.ToString("yyyyMMdd-HHmmss")+(gray?"-gray":"-final"));
            Directory.CreateDirectory(directory);
            _review=material?new BedroomMaterialReview():new BedroomTargetReview();AddChild(_review);await Frames(30);
            var gi=_review.Visual.GetNode<LightmapGI>("LightmapGI");
            Require(gi.LightData is not null && gi.LightData.GetUserCount()>0,"LightmapGI baked users loaded");
            var model=_review.Visual.GetNode<Node3D>("BedroomModel");
            var runtimeMeshes=Nodes(model).OfType<MeshInstance3D>().ToArray();
            using(var manifest=JsonDocument.Parse(File.ReadAllText(ProjectSettings.GlobalizePath(assetDirectory+(material?"bedroom-material-v1":gray?"bedroom-gray":"bedroom-target-v2")+"-manifest.json"))))
                Require(runtimeMeshes.Length==manifest.RootElement.GetProperty("meshObjects").GetInt32(),"exactly one runtime copy of each source mesh");
            Require(runtimeMeshes.Where(m=>m.Name.ToString().StartsWith("Glass_")).All(m=>m.CastShadow==GeometryInstance3D.ShadowCastingSetting.Off),"all glass panes excluded from shadow casting");
            Require(RenderingServer.GetCurrentRenderingMethod()=="gl_compatibility","Compatibility runtime renderer");
            _review.Help.Visible=false;
            if(OS.GetCmdlineUserArgs().Contains("--lighting-diagnosis"))
            {
                DisplayServer.WindowSetSize(new(1920,1080));_review.SetView(0);
                var data=gi.LightData;var sun=_review.Visual.GetNode<DirectionalLight3D>("MorningSun");
                var environment=_review.Visual.GetNode<WorldEnvironment>("WorldEnvironment").Environment;
                GD.Print("BEDROOM_SUN_BASIS_Z ",sun.GlobalBasis.Z," rotation=",sun.GlobalRotationDegrees);
                foreach(var mesh in Nodes(_review.Visual).OfType<MeshInstance3D>())
                {
                    var body=new StaticBody3D {CollisionLayer=8,CollisionMask=0};
                    body.AddChild(new CollisionShape3D {Shape=mesh.Mesh.CreateTrimeshShape()});mesh.AddChild(body);
                }
                for(var i=0;i<3;i++)await ToSignal(GetTree(),SceneTree.SignalName.PhysicsFrame);
                foreach(var point in new[]{new Vector3(-1.3f,.84f,.2f),new Vector3(-2.04f,1.2f,-.2f),new Vector3(1.5f,.85f,-.3f)})
                {
                    var query=PhysicsRayQueryParameters3D.Create(point,point+sun.GlobalBasis.Z*30,8);query.HitBackFaces=true;
                    var hit=_review.GetWorld3D().DirectSpaceState.IntersectRay(query);
                    GD.Print("BEDROOM_SUN_BLOCKER ",point," ",hit.Count==0?"clear":((Node)hit["collider"]).GetParent().Name.ToString()," ",hit.Count==0?Vector3.Zero:(Vector3)hit["position"]);
                }
                foreach(var mode in new[]{"baked","indirect-only","unbaked","unshadowed","lowbias","highbias","reversecull"})
                {
                    gi.LightData=mode is "baked" or "indirect-only"?data:null;
                    sun.Visible=mode!="indirect-only";sun.ShadowEnabled=mode!="unshadowed";
                    if(mode=="lowbias"){sun.ShadowBias=.001f;sun.ShadowNormalBias=.01f;}
                    if(mode=="highbias"){sun.ShadowBias=.2f;sun.ShadowNormalBias=3;}
                    if(mode=="reversecull"){sun.ShadowBias=.06f;sun.ShadowNormalBias=1;sun.ShadowReverseCullFace=!sun.ShadowReverseCullFace;}
                    await Frames(15);await ToSignal(RenderingServer.Singleton,RenderingServer.SignalName.FramePostDraw);
                    GetViewport().GetTexture().GetImage().SavePng(Path.Combine(directory,"lighting-"+mode+".png"));
                }
                GD.Print("BEDROOM_LIGHT_DIAGNOSIS "+directory);GetTree().Quit();return;
            }
            foreach(var size in new[]{new Vector2I(1920,1080),new Vector2I(1280,720)})
            {
                DisplayServer.WindowSetSize(size);await Frames(10);
                var names=material?BedroomMaterialReview.ViewNames:BedroomTargetReview.Views.Select(v=>v.Name).ToArray();
                if(_review is BedroomMaterialReview uvCheck)
                {
                    uvCheck.SetNeutral(true);uvCheck.SetUvCheck(true);await Frames(15);
                    await ToSignal(RenderingServer.Singleton,RenderingServer.SignalName.FramePostDraw);
                    Require(GetViewport().GetTexture().GetImage().SavePng(Path.Combine(directory,"uv-checker-"+size.Y+".png"))==Error.Ok,"UV checker capture "+size.Y);
                    uvCheck.SetUvCheck(false);uvCheck.SetNeutral(false);
                }
                foreach(var refined in material?new[]{false,true}:new[]{true})
                {
                if(_review is BedroomMaterialReview mr)mr.SetRefined(refined);
                for(var i=0;i<names.Length;i++)
                {
                    _review.SetView(i);await Frames(12);await ToSignal(RenderingServer.Singleton,RenderingServer.SignalName.FramePostDraw);
                    using var image=GetViewport().GetTexture().GetImage();
                    Require(image.GetWidth()==size.X && image.GetHeight()==size.Y,"viewport "+size);
                    Require(image.SavePng(Path.Combine(directory,names[i]+(material?(refined?"-new":"-old"):"")+"-"+size.Y+".png"))==Error.Ok,"capture "+i+" "+size.Y+" "+refined);
                }
                if(_review is BedroomMaterialReview neutral)
                {
                    neutral.SetNeutral(true);await Frames(15);await ToSignal(RenderingServer.Singleton,RenderingServer.SignalName.FramePostDraw);
                    Require(GetViewport().GetTexture().GetImage().SavePng(Path.Combine(directory,"neutral-"+(refined?"new":"old")+"-"+size.Y+".png"))==Error.Ok,"neutral material capture "+size.Y+" "+refined);
                    neutral.SetNeutral(false);
                }
                }
            }
            if(_review is BedroomMaterialReview materials)
            {
                var lightData=gi.LightData;
                foreach(var refined in new[]{false,true})
                {
                    materials.SetRefined(refined);materials.SetView(5);materials.SetLamp(true);await Frames(15);
                    await ToSignal(RenderingServer.Singleton,RenderingServer.SignalName.FramePostDraw);
                    GetViewport().GetTexture().GetImage().SavePng(Path.Combine(directory,"desk-lamp-"+(refined?"new":"old")+"-720.png"));
                    Require(gi.LightData==lightData,"A/B shares identical LightmapGI data "+refined);
                }
                materials.ResetReview();Require(materials.Refined && !materials.Neutral,"reset restores new material room mode");
                foreach(var kind in new[]{"Desk","Wardrobe"})
                {
                    var mesh=model.GetNode<MeshInstance3D>("Furniture_"+kind);
                    var mat=(BaseMaterial3D)mesh.GetActiveMaterial(0);
                    Require(mat.AlbedoTexture is not null && mat.NormalTexture is not null && mat.RoughnessTexture is not null,"complete texture channels "+kind);
                    Require(mat.NormalEnabled && mat.RoughnessTextureChannel==BaseMaterial3D.TextureChannel.Green,"normal enabled and roughness uses green "+kind);
                    var arrays=mesh.Mesh.SurfaceGetArrays(0);
                    Require(arrays[(int)Mesh.ArrayType.TexUV2].AsVector2Array().Length>0,"UV2 exists "+kind);
                    Require(arrays[(int)Mesh.ArrayType.Tangent].AsFloat32Array().Length>0,"tangents exist "+kind);
                    Require(mat.TextureFilter==BaseMaterial3D.TextureFilterEnum.LinearWithMipmapsAnisotropic,"anisotropic filtering "+kind);
                }
                foreach(var kind in new[]{"desk","wardrobe"})
                foreach(var suffix in new[]{"color","normal","orm","old-color","old-orm"})
                {
                    var name=(suffix.StartsWith("old-")?"":"bedroom-material-v1_")+kind+"-"+suffix+".png";
                    using var image=ResourceLoader.Load<Texture2D>(assetDirectory+name).GetImage();
                    Require(image.GetWidth()==2048 && image.GetHeight()==2048 && image.HasMipmaps(),"2048 atlas with mipmaps "+name);
                    textureBytes+=image.GetData().LongLength;textureFormats[name]=image.GetFormat().ToString();
                }
                Require(textureBytes<=64L*1024*1024,"A/B atlas data fits 64 MiB budget");
            }
            _review.SetLamp(true);Require(_review.LampOn,"lamp switched on");_review.SetView(2);await Frames(12);
            await ToSignal(RenderingServer.Singleton,RenderingServer.SignalName.FramePostDraw);
            GetViewport().GetTexture().GetImage().SavePng(Path.Combine(directory,"desk-lamp-720.png"));
            _review.SetLamp(false);Require(!_review.LampOn,"lamp switched off");
            foreach(var detail in new[]{("clock",new Vector3(.65f,2.16f,1.42f),new Vector3(.65f,2.16f,2.25f),32f),
                ("paper",new Vector3(1.09f,1.40f,-.46f),new Vector3(1.66f,.82f,-.68f),37f)})
            {
                _review.ReviewCamera.GlobalPosition=detail.Item2;_review.ReviewCamera.LookAt(detail.Item3);_review.ReviewCamera.Fov=detail.Item4;
                await Frames(15);await ToSignal(RenderingServer.Singleton,RenderingServer.SignalName.FramePostDraw);
                GetViewport().GetTexture().GetImage().SavePng(Path.Combine(directory,detail.Item1+"-detail-720.png"));
            }
            if(!gray && !captureOnly)
            {
                DisplayServer.WindowSetSize(new(1920,1080));DisplayServer.WindowSetVsyncMode(DisplayServer.VSyncMode.Disabled);
                GetTree().ProcessFrame+=SampleWalking;_sampleWalking=true;_lastWalkingTick=Time.GetTicksUsec();
                _review.SetWalking(true);_review.Rig.Notification((int)NotificationWMWindowFocusIn);await Frames(5);
                foreach(var p in new[]{new Vector3(-1.06f,0,1.80f),new Vector3(-.30f,0,1.5f),new Vector3(-.30f,0,.35f),
                         new Vector3(-.30f,0,-1.28f),new Vector3(.3f,0,-1.28f),new Vector3(.45f,0,-.85f),
                         new Vector3(.38f,0,.5f),new Vector3(.3f,0,1.35f),new Vector3(-1.2f,0,1.9f),new Vector3(-1.42f,0,2.92f)})await Walk(p);
                _review.SetDoor(false);await Frames(70);Require(!_review.DoorOpen,"door closed");
                // Closed doorway must stop the real movement capsule.
                _review.Actor.GlobalPosition=new(-1.42f,0,2.95f);Input.ActionPress("move_up");
                for(var i=0;i<55;i++)await ToSignal(GetTree(),SceneTree.SignalName.PhysicsFrame);
                Release();Require(_review.Actor.GlobalPosition.Z>2.5f,"closed door blocks actor");
                _review.SetDoor(true);await Frames(70);await Walk(new(-1.42f,0,2.02f));
                var hit=_review.GetWorld3D().DirectSpaceState.IntersectRay(PhysicsRayQueryParameters3D.Create(new(0,1.3f,0),new(-4,1.3f,0),3));
                Require(hit.Count>0,"camera wall ray collision");
                _review.Rig.SetYawImmediate(-90);_review.Rig.SnapToTarget();await Frames(15);
                Require(_review.Rig.SpringArm.GetHitLength()<_review.Rig.SpringArm.SpringLength,"camera spring arm retracts at wall");
                _sampleWalking=false;GetTree().ProcessFrame-=SampleWalking;
            }
            DisplayServer.WindowSetSize(new(1920,1080));DisplayServer.WindowSetVsyncMode(DisplayServer.VSyncMode.Disabled);
            _review.SetView(0);await Frames(80);
            var frameMs=new List<double>();ulong last=Time.GetTicksUsec();
            for(var i=0;i<180;i++){await Frames(1);var now=Time.GetTicksUsec();frameMs.Add((now-last)/1000d);last=now;}
            frameMs.Sort();
            _walkingFrames.Sort();
            var walkingMetrics=new {samples=_walkingFrames.Count,frameWallMedianMs=_walkingFrames.Count>0?_walkingFrames[_walkingFrames.Count/2]:0,
                frameWallP95Ms=_walkingFrames.Count>0?_walkingFrames[(int)(_walkingFrames.Count*.95)]:0,
                texturePeakBytes=_walkingTexturePeak,videoPeakBytes=_walkingVideoPeak,drawCallPeak=_walkingDrawPeak};
            var metrics=new {frameWallMedianMs=frameMs[frameMs.Count/2],frameWallP95Ms=frameMs[(int)(frameMs.Count*.95)],
                primitives=RenderingServer.GetRenderingInfo(RenderingServer.RenderingInfo.TotalPrimitivesInFrame),
                drawCalls=RenderingServer.GetRenderingInfo(RenderingServer.RenderingInfo.TotalDrawCallsInFrame),
                videoMemoryBytes=RenderingServer.GetRenderingInfo(RenderingServer.RenderingInfo.VideoMemUsed),
                textureMemoryBytes=RenderingServer.GetRenderingInfo(RenderingServer.RenderingInfo.TextureMemUsed)};
            Require(before==JsonSerializer.Serialize(session.State),"formal session unchanged");
            var after=Saves();Require(saves.Count==after.Count && saves.All(k=>after.TryGetValue(k.Key,out var h)&&h==k.Value),"formal JSON saves byte-identical");
            File.WriteAllText(Path.Combine(directory,"results.json"),JsonSerializer.Serialize(new{status="PASS",gray,material,captureOnly,checks=_checks,
                renderer=RenderingServer.GetCurrentRenderingMethod(),device=RenderingServer.GetVideoAdapterName(),bakedMeshes=gi.LightData!.GetUserCount(),
                metrics,walkingMetrics,atlasDataBytes=textureBytes,atlasFormats=textureFormats,targetGtx1060Verified=false,visualApproval=false},new JsonSerializerOptions{WriteIndented=true}));
            GD.Print($"BEDROOM_PROBE_PASS checks={_checks.Count} output={directory}");GetTree().Quit();
        }
        catch(Exception e){Release();GD.PushError(e.ToString());GetTree().Quit(1);}
    }
    private void SampleWalking()
    {
        if(!_sampleWalking)return;
        var now=Time.GetTicksUsec();_walkingFrames.Add((now-_lastWalkingTick)/1000d);_lastWalkingTick=now;
        _walkingTexturePeak=Math.Max(_walkingTexturePeak,RenderingServer.GetRenderingInfo(RenderingServer.RenderingInfo.TextureMemUsed));
        _walkingVideoPeak=Math.Max(_walkingVideoPeak,RenderingServer.GetRenderingInfo(RenderingServer.RenderingInfo.VideoMemUsed));
        _walkingDrawPeak=Math.Max(_walkingDrawPeak,RenderingServer.GetRenderingInfo(RenderingServer.RenderingInfo.TotalDrawCallsInFrame));
    }
    private static IEnumerable<Node> Nodes(Node root)
    {yield return root;foreach(var c in root.GetChildren())foreach(var n in Nodes(c))yield return n;}
}
