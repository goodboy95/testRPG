using Godot;
using System.Text.Json;

namespace TownRpg.Presentation;

/// <summary>Seven district visual streams; collision and game state remain owned by the exterior.</summary>
public partial class VillageStreaming3D : Node3D
{
    public const string ManifestPath="res://assets/generated/village/stream-v7/manifest.json";
    public VillageRenderManifest Manifest { get; private set; }=null!;
    public VillageRenderProfile Profile { get; private set; }=VillageRenderProfile.Laptop1060;
    public VillageRenderSelection Selection { get; private set; }=new([],new(),true);
    public int LoadedDistricts=>_detail.Count;
    public int PendingLoads=>_pending.Count;
    public int LoadFailures { get; private set; }
    public int UnloadedDistricts { get; private set; }
    public int ProxyRebuilds { get; private set; }
    public double LastLoadMs { get; private set; }
    public double ControllerMs { get; private set; }
    public double? GpuMs { get; private set; }
    public double RenderCpuMs { get; private set; }
    public int Pressure { get; private set; }
    public string BudgetTier { get; private set; }="near";
    public int WholeFramePrimitives { get; private set; }
    public int WholeFrameDrawCalls { get; private set; }
    public bool WholeFrameTriangleTargetMet=>WholeFramePrimitives<=Profile.FrameNear.Triangles;
    private VillageExterior3D _area=null!;
    private readonly Dictionary<string,MeshInstance3D> _meshes=[];
    private readonly Dictionary<string,Node3D> _proxy=[];
    private readonly Dictionary<string,Node3D> _detail=[];
    private readonly HashSet<string> _pending=[];
    private readonly HashSet<string> _failed=[];
    private readonly Dictionary<string,double> _outsideSince=[];
    private readonly Dictionary<string,string> _signatures=[];
    private readonly Dictionary<string,MeshInstance3D> _batches=[];
    private readonly Dictionary<string,int> _previous=[];
    private readonly Dictionary<string,Material> _proxyMaterials=[];
    private sealed record ProxySurface(Material Material,Vector3[] Vertices,Vector3[] Normals,Vector2[] Uv,Color[] Colors,int[] Indices);
    private readonly Dictionary<string,ProxySurface[]> _proxySurfaces=[];
    private VillageRenderCandidate[] _candidates=[];
    private Label? _hud;
    private StreamWriter? _log;
    private double _clock,_nextLog,_pressureClock;
    private double _gpuSum;
    private int _gpuSamples;
    private Vector3 _lastSelectionPosition=new(float.PositiveInfinity,0,0);
    private Vector3 _lastSelectionDirection;
    private float _lastFov;
    private int _lastDetailGeneration=-1,_detailGeneration,_lastPressure=-1;

    public void Initialize(VillageExterior3D area)
    {
        _area=area;Profile=area.RenderProfile;ProcessPriority=100;
        Manifest=VillageRenderManifest.Parse(Godot.FileAccess.GetFileAsString(area.VisualProfile.StreamManifest));
        foreach(var district in Manifest.Districts)
        {
            var resource=ResourceLoader.Load<PackedScene>(Bundle(district+"_proxy").Path,cacheMode:ResourceLoader.CacheMode.Ignore);
            _proxy[district]=Install(resource,district,false);
        }
        if(OS.GetCmdlineUserArgs().Contains("--village-budget-hud"))
        {
            var layer=new CanvasLayer { Layer=95 };AddChild(layer);
            _hud=new Label { Position=new(8,34),MouseFilter=Control.MouseFilterEnum.Ignore };
            _hud.AddThemeFontSizeOverride("font_size",11);layer.AddChild(_hud);
        }
        if(OS.GetCmdlineUserArgs().Contains("--village-budget-log"))
        {
            var folder=ProjectSettings.GlobalizePath("res://outputs/village-"+(area.VisualProfile.AnimeLighting?"v8":"v7")+"/telemetry");Directory.CreateDirectory(folder);
            _log=new StreamWriter(Path.Combine(folder,DateTime.Now.ToString("yyyyMMdd-HHmmss-fff")+".jsonl"));
        }
        RenderingServer.ViewportSetMeasureRenderTime(GetViewport().GetViewportRid(),true);
        UpdateVisuals();
    }
    private VillageRenderBundle Bundle(string id)=>Manifest.Bundles.Single(b=>b.Id==id);
    private static IEnumerable<MeshInstance3D> Meshes(Node root)
    {
        foreach(var child in root.GetChildren())
        {
            if(child is MeshInstance3D m)yield return m;
            foreach(var m2 in Meshes(child))yield return m2;
        }
    }
    private Node3D Install(PackedScene resource,string district,bool detail)
    {
        var root=resource.Instantiate<Node3D>();root.Name=district+(detail?"_detail":"_proxy");AddChild(root);
        foreach(var mesh in Meshes(root))
        {
            mesh.Visible=false;_meshes.Add(mesh.Name,mesh);
            mesh.CastShadow=detail?GeometryInstance3D.ShadowCastingSetting.On:GeometryInstance3D.ShadowCastingSetting.Off;
            for(var s=0;s<mesh.Mesh.GetSurfaceCount();s++)
            {
                if(mesh.GetActiveMaterial(s) is not StandardMaterial3D mat)continue;
                if(_area.VisualProfile.AnimeLighting && mat.ResourceName.StartsWith("V8_leaf",StringComparison.Ordinal))
                { mat.BacklightEnabled=true;mat.Backlight=new Color(.13f,.17f,.08f); }
                if(mat.ResourceName=="V3_Leaves")
                {
                    mat.Transparency=BaseMaterial3D.TransparencyEnum.AlphaScissor;mat.AlphaScissorThreshold=.45f;
                    mat.CullMode=BaseMaterial3D.CullModeEnum.Disabled;mat.BacklightEnabled=true;mat.Backlight=new(.12f,.17f,.06f);
                }
                if(mat.ResourceName is "V3_Leaves" or "V7_ProxyColour")
                {
                    if(!_proxyMaterials.TryGetValue(mat.ResourceName,out var shared))_proxyMaterials[mat.ResourceName]=shared=mat;
                    mesh.SetSurfaceOverrideMaterial(s,shared);
                }
            }
            if(!detail)CacheProxy(mesh);
        }
        return root;
    }
    private void CacheProxy(MeshInstance3D node)
    {
        var surfaces=new List<ProxySurface>();
        var transform=GlobalTransform.AffineInverse()*node.GlobalTransform;
        var normalsTransform=transform.Basis.Inverse().Transposed();
        for(var s=0;s<node.Mesh.GetSurfaceCount();s++)
        {
            using var arrays=node.Mesh.SurfaceGetArrays(s);
            var vertices=arrays[(int)Mesh.ArrayType.Vertex].AsVector3Array();
            var normals=arrays[(int)Mesh.ArrayType.Normal].AsVector3Array();
            var uv=arrays[(int)Mesh.ArrayType.TexUV].AsVector2Array();
            var colors=arrays[(int)Mesh.ArrayType.Color].VariantType==Variant.Type.Nil?Enumerable.Repeat(Colors.White,vertices.Length).ToArray():arrays[(int)Mesh.ArrayType.Color].AsColorArray();
            var indices=arrays[(int)Mesh.ArrayType.Index].VariantType==Variant.Type.Nil?Enumerable.Range(0,vertices.Length).ToArray():arrays[(int)Mesh.ArrayType.Index].AsInt32Array();
            for(var i=0;i<vertices.Length;i++)
            {
                vertices[i]=transform*vertices[i];normals[i]=(normalsTransform*normals[i]).Normalized();
                if(RenderingServer.GetCurrentRenderingMethod()=="gl_compatibility")colors[i]=colors[i].LinearToSrgb();
            }
            surfaces.Add(new(node.GetActiveMaterial(s),vertices,normals,uv,colors,indices));
        }
        _proxySurfaces[node.Name]=surfaces.ToArray();
    }
    private static float Distance(float[] b,Vector3 p)
    {
        var nearest=new Vector3(Math.Clamp(p.X,b[0],b[0]+b[3]),Math.Clamp(p.Y,b[1],b[1]+b[4]),Math.Clamp(p.Z,b[2],b[2]+b[5]));
        return p.DistanceTo(nearest);
    }
    private void Stream(Vector3 p)
    {
        foreach(var district in Manifest.Districts)
        {
            // Ground bounds are deliberately excluded: long scenery must not pin every high-detail district.
            var units=Manifest.Units.Where(u=>u.District==district && u.Category!="ground").ToArray();
            var distance=units.Length==0?float.PositiveInfinity:units.Min(u=>Distance(u.Bounds,p));
            if(distance<VillageRenderPolicy.PreloadDistance)
            {
                _outsideSince.Remove(district);
                if(!_detail.ContainsKey(district) && !_pending.Contains(district) && !_failed.Contains(district))
                {
                    var path=Bundle(district+"_detail").Path;
                    // A previous exterior may have closed while the engine was loading this
                    // resource. Adopt its request instead of treating an in-flight load as failure.
                    var status=ResourceLoader.LoadThreadedGetStatus(path);
                    if(status is ResourceLoader.ThreadLoadStatus.InProgress or ResourceLoader.ThreadLoadStatus.Loaded){_pending.Add(district);continue;}
                    var error=ResourceLoader.LoadThreadedRequest(path,"PackedScene",true,ResourceLoader.CacheMode.Ignore);
                    if(error==Error.Ok)_pending.Add(district);else Fail(district,error.ToString());
                }
            }
            if(distance<=VillageRenderPolicy.UnloadDistance){_outsideSince.Remove(district);continue;}
            if(!_outsideSince.TryGetValue(district,out var since)){_outsideSince[district]=_clock;continue;}
            if(_clock-since>VillageRenderPolicy.UnloadDelaySeconds && _detail.Remove(district,out var root))
            {
                foreach(var mesh in Meshes(root))_meshes.Remove(mesh.Name);
                root.Free();_detailGeneration++;UnloadedDistricts++;
            }
        }
        // Poll without blocking. Instantiate at most one finished bundle in each frame.
        foreach(var district in _pending.ToArray())
        {
            var path=Bundle(district+"_detail").Path;var status=ResourceLoader.LoadThreadedGetStatus(path);
            if(status==ResourceLoader.ThreadLoadStatus.Loaded)
            {
                var start=Time.GetTicksUsec();var scene=(PackedScene)ResourceLoader.LoadThreadedGet(path);
                _detail[district]=Install(scene,district,true);_pending.Remove(district);
                _detailGeneration++;
                LastLoadMs=(Time.GetTicksUsec()-start)/1000d;break;
            }
            if(status is ResourceLoader.ThreadLoadStatus.Failed or ResourceLoader.ThreadLoadStatus.InvalidResource){_pending.Remove(district);Fail(district,status.ToString());}
        }
    }
    private void Fail(string district,string error)
    {
        _failed.Add(district);LoadFailures++;GD.PushError($"Village v7 detail stream failed: {district}: {error}. Resident proxy retained.");
    }
    public override void _Process(double delta)
    {
        if(Manifest is null)return;
        _clock+=delta;var start=Time.GetTicksUsec();Stream(_area.PlayerActor.GlobalPosition);UpdateVisuals();ControllerMs=(Time.GetTicksUsec()-start)/1000d;
        var rid=GetViewport().GetViewportRid();var gpu=RenderingServer.ViewportGetMeasuredRenderTimeGpu(rid);
        GpuMs=gpu>0 && double.IsFinite(gpu)?gpu:null;RenderCpuMs=RenderingServer.ViewportGetMeasuredRenderTimeCpu(rid);
        WholeFramePrimitives=(int)Performance.GetMonitor(Performance.Monitor.RenderTotalPrimitivesInFrame);
        WholeFrameDrawCalls=(int)Performance.GetMonitor(Performance.Monitor.RenderTotalDrawCallsInFrame);
        if(GpuMs is {} valid){_gpuSum+=valid;_gpuSamples++;}
        _pressureClock+=delta;
        if(_pressureClock>=2)
        {
            // Slow recovery and bounded pressure; missing GPU timers never mean a 0 ms pass.
            if(_gpuSamples>0)
            {
                var mean=_gpuSum/_gpuSamples;
                if(mean>16.7)Pressure=Math.Min(2,Pressure+1);
                else if(mean<12)Pressure=Math.Max(0,Pressure-1);
            }
            _pressureClock=0;_gpuSum=0;_gpuSamples=0;
        }
        if(_clock>=_nextLog)
        {
            _nextLog=_clock+.5;
            if(_hud is not null)_hud.Text=$"V7 {Profile.Id} / {BudgetTier}\nscenery {Selection.Cost.Triangles:N0} tris / {Selection.Cost.Meshes} meshes / {Selection.Cost.Batches} surfaces\nframe {WholeFramePrimitives:N0} primitives / {WholeFrameDrawCalls} draws / GPU {(GpuMs is {} ms?ms.ToString("F2"):"N/A")} ms\nstreams {LoadedDistricts}/7 +{PendingLoads} | pressure {Pressure} | {(Selection.Fits?"SCENERY OK":"BUDGET OVERFLOW")}";
            _log?.WriteLine(JsonSerializer.Serialize(Snapshot()));_log?.Flush();
        }
    }
    public object Snapshot()=>new
    {
        seconds=_clock,profile=Profile.Id,frameBudget=BudgetTier=="near"?Profile.FrameNear:Profile.FrameWide,
        scenery=Selection.Cost,sceneryFits=Selection.Fits,budgetTier=BudgetTier,loadedDistricts=LoadedDistricts,pendingLoads=PendingLoads,
        loadFailures=LoadFailures,unloadedDistricts=UnloadedDistricts,lastLoadMs=LastLoadMs,controllerMs=ControllerMs,gpuMs=GpuMs,renderCpuMs=RenderCpuMs,
        wholeFramePrimitives=WholeFramePrimitives,wholeFrameDrawCalls=WholeFrameDrawCalls,pressure=Pressure,
        frameNearTriangleTargetMet=WholeFrameTriangleTargetMet,frameNearDrawTargetMet=WholeFrameDrawCalls<=Profile.FrameNear.Batches,
        homeLod=SelectedLod("home"),shopLod=SelectedLod("shop"),
        groups=_candidates.Select((c,i)=>new {c.Unit.District,c.Unit.Category,lod=Selection.Lods[i],triangles=c.Unit.Lods[Selection.Lods[i]].Triangles})
            .GroupBy(x=>new{x.District,x.Category,x.lod}).Select(g=>new{g.Key,count=g.Count(),triangles=g.Sum(x=>x.triangles)}).ToArray()
    };
    public void UpdateVisuals()
    {
        var camera=GetViewport().GetCamera3D() ?? _area.CameraController.Camera;var position=camera.GlobalPosition;
        var direction=-camera.GlobalBasis.Z;
        if(_lastSelectionPosition.DistanceSquaredTo(position)<.1f*.1f && direction.Dot(_lastSelectionDirection)>.999999f && _lastDetailGeneration==_detailGeneration && _lastPressure==Pressure && _lastFov==camera.Fov)return;
        _lastSelectionPosition=position;_lastSelectionDirection=direction;_lastDetailGeneration=_detailGeneration;_lastPressure=Pressure;_lastFov=camera.Fov;
        var planes=camera.GetFrustum();
        // Recompute from the actual camera before rendering, with a 2 m conservative margin.
        // Nearby off-screen units remain candidates for the directional shadow volume.
        bool Candidate(VillageRenderUnit u)
        {
            if(Distance(u.Bounds,position)<Profile.ShadowDistance)return true;
            var b=u.Bounds;var box=new Aabb(new(b[0],b[1],b[2]),new(b[3],b[4],b[5])).Grow(2);
            foreach(var plane in planes)
                if(Enumerable.Range(0,8).All(i=>plane.DistanceTo(box.GetEndpoint(i))>0))return false;
            return true;
        }
        _candidates=Manifest.Units.Where(Candidate).Select(u=>
        {
            var distance=Distance(u.Bounds,position);
            var screen=Math.Max(u.Bounds[3],u.Bounds[4])/(Math.Max(1,distance)*2*Mathf.Tan(Mathf.DegToRad(camera.Fov)*.5f));
            return new VillageRenderCandidate(u,distance,screen,_detail.ContainsKey(u.District),_previous.GetValueOrDefault(u.Id,-1));
        }).ToArray();
        // Reserve room for the measured terrain/actor and repeated shadow submissions.
        Selection=VillageRenderPolicy.Select(_candidates,Profile.SceneryNear,Pressure,Profile);
        BudgetTier="near";
        // A long sightline can compete with a nearby landmark. Use the planned wide-view
        // allocation rather than silently removing the landmark's near materials.
        if(!Selection.Fits || _candidates.Where((c,i)=>c.DetailReady && Selection.Lods[i]>1 &&
            ((c.Unit.Important && c.Distance<18) || (Profile.PreserveNearbyTrees && c.Unit.Category=="building" && c.Distance<24))).Any())
        {
            Selection=VillageRenderPolicy.Select(_candidates,Profile.SceneryWide,Pressure,Profile);
            BudgetTier="wide";
        }
        foreach(var mesh in _meshes.Values)mesh.Visible=false;
        for(var i=0;i<_candidates.Length;i++)
        {
            var u=_candidates[i].Unit;var lod=Selection.Lods[i];_previous[u.Id]=lod;
            if(lod<2)
            {
                var mesh=_meshes[u.Lods[lod].Node];mesh.Visible=true;
                mesh.CastShadow=GeometryInstance3D.ShadowCastingSetting.Off;
                if(u.Category!="ground" && lod<=Profile.ShadowCasterMaxLod && _candidates[i].Distance<Profile.ShadowCasterDistance)
                {
                    // The visible facade keeps its authored materials; only the shadow pass
                    // uses the resident silhouette instead of resubmitting every tile/fitting.
                    var caster=_meshes[u.Lods[Profile.ShadowProxyLod].Node];caster.Visible=true;
                    caster.CastShadow=GeometryInstance3D.ShadowCastingSetting.ShadowsOnly;
                }
            }
        }
        foreach(var district in new[]{"resident"})
        {
            var members=Enumerable.Range(0,_candidates.Length).Where(i=>Selection.Lods[i]>=2)
                .Select(i=>_candidates[i].Unit.Lods[Selection.Lods[i]].Node).ToArray();
            var signature=string.Join('|',members);
            if(_signatures.GetValueOrDefault(district)==signature)continue;
            _signatures[district]=signature;
            RebuildProxy(district,members);
        }
    }
    public int SelectedLod(string id)
    {
        var index=Array.FindIndex(_candidates,c=>c.Unit.Id==id);return index<0?-1:Selection.Lods[index];
    }
    private void RebuildProxy(string district,string[] members)
    {
        if(_batches.Remove(district,out var old))
        {
            // Generated batches are exclusively owned here, unlike imported shared meshes.
            // Release GPU buffers now rather than waiting for a managed GC during a long walk.
            var owned=old.Mesh;old.Mesh=null;old.Free();owned?.Dispose();
        }
        if(members.Length==0)return;
        var mesh=new ArrayMesh();
        foreach(var group in members.SelectMany(name=>_proxySurfaces[name]).GroupBy(s=>s.Material))
        {
            var count=group.Sum(s=>s.Vertices.Length);var indexCount=group.Sum(s=>s.Indices.Length);
            var vertices=new Vector3[count];var normals=new Vector3[count];var uv=new Vector2[count];var colors=new Color[count];var indices=new int[indexCount];
            var v=0;var t=0;
            foreach(var part in group)
            {
                Array.Copy(part.Vertices,0,vertices,v,part.Vertices.Length);Array.Copy(part.Normals,0,normals,v,part.Normals.Length);
                Array.Copy(part.Uv,0,uv,v,part.Uv.Length);Array.Copy(part.Colors,0,colors,v,part.Colors.Length);
                foreach(var index in part.Indices)indices[t++]=index+v;
                v+=part.Vertices.Length;
            }
            using var arrays=new Godot.Collections.Array();arrays.Resize((int)Mesh.ArrayType.Max);
            arrays[(int)Mesh.ArrayType.Vertex]=vertices;arrays[(int)Mesh.ArrayType.Normal]=normals;
            arrays[(int)Mesh.ArrayType.TexUV]=uv;arrays[(int)Mesh.ArrayType.Color]=colors;arrays[(int)Mesh.ArrayType.Index]=indices;
            mesh.AddSurfaceFromArrays(Mesh.PrimitiveType.Triangles,arrays);
            mesh.SurfaceSetMaterial(mesh.GetSurfaceCount()-1,group.Key);
        }
        var node=new MeshInstance3D {Name=district+"_merged_proxy",Mesh=mesh,CastShadow=GeometryInstance3D.ShadowCastingSetting.Off};
        AddChild(node);_batches[district]=node;ProxyRebuilds++;
    }
    public override void _ExitTree()
    {
        _log?.Dispose();_log=null;
        // The exterior can be detached while visiting home. Resident proxies/details remain valid
        // for re-entry, and no async continuations ever touch a detached or freed scene node.
    }
}
