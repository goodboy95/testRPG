using Godot;
using System.Security.Cryptography;
using System.Text.Json;
using TownRpg.Application;
using TownRpg.Core.Services;

namespace TownRpg.Testing;

/// <summary>Standalone static baseline inspection. Never spawns a player or an NPC actor.</summary>
public partial class CharacterBaselineReview : Node3D
{
    private const string DefaultManifest = "res://assets/generated/characters/mpfb-male-v1/v001-game3/manifest.json";
    private readonly List<MeshInstance3D> _meshes = [];
    private readonly Dictionary<MeshInstance3D, Material?[]> _original = [];
    private readonly List<object> _samples = [];
    private Camera3D _camera = null!;
    private Label _label = null!;
    private Node3D _model = null!;
    private string _asset = "", _hash = "", _version = "";
    private int _triangles, _surfaces, _view;
    private float _height, _yaw, _pitch;
    private bool _orbit, _styled;
    private int _mode;
    private readonly Dictionary<MeshInstance3D,string> _roles = [];
    private string _beforeState = "", _savePath = "";
    private byte[]? _beforeSave;
    private GameSession _session = null!;
    private readonly record struct View(string Name, float Angle, bool Head);
    private View[] _views = [
        new("head-front",0,true),new("head-left45",-45,true),new("head-right45",45,true),
        new("head-left90",-90,true),new("head-right90",90,true),
        new("body-front",0,false),new("body-side",90,false),new("body-back",180,false)
    ];
    private static IEnumerable<Node> Descendants(Node parent)
    {
        foreach (var child in parent.GetChildren())
        { yield return child; foreach (var node in Descendants(child)) yield return node; }
    }

    public override void _Ready() => CallDeferred(MethodName.Initialize);

    private async void Initialize()
    {
        try
        {
            _session = GetNode<GameSession>("/root/GameSession");
            _beforeState = SaveGameSerializer.Serialize(_session.State);
            _savePath = ProjectSettings.GlobalizePath(GameSession.SavePath);
            _beforeSave = File.Exists(_savePath) ? File.ReadAllBytes(_savePath) : null;
            var args = OS.GetCmdlineUserArgs();
            var manifestPath = args.FirstOrDefault(a => a.StartsWith("--character-manifest="))?.Split('=',2)[1] ?? DefaultManifest;
            if (!manifestPath.StartsWith("res://assets/generated/characters/mpfb-male-v1/") || manifestPath.Contains(".."))
                throw new InvalidDataException("Only independent baseline manifests are accepted.");
            using var json = JsonDocument.Parse(File.ReadAllText(ProjectSettings.GlobalizePath(manifestPath)));
            var manifest = json.RootElement;
            _asset = "res://" + manifest.GetProperty("glb").GetString();
            if (!_asset.StartsWith("res://assets/generated/characters/mpfb-male-v1/") || _asset.Contains(".."))
                throw new InvalidDataException("Asset is outside baseline directory.");
            _version = manifest.GetProperty("recipe").GetProperty("version").GetString()!;
            _hash = Convert.ToHexString(SHA256.HashData(File.ReadAllBytes(ProjectSettings.GlobalizePath(_asset)))).ToLowerInvariant();
            if (_hash != manifest.GetProperty("glbSha256").GetString()) throw new InvalidDataException("Stale asset hash.");
            _height = manifest.GetProperty("geometry").GetProperty("body").GetProperty("bounds")[1][2].GetSingle();
            if (manifest.TryGetProperty("reviewBodyHeight",out var framing)) _height=framing.GetSingle();
            _styled=manifest.GetProperty("recipe").TryGetProperty("style",out _);
            if (manifest.GetProperty("recipe").TryGetProperty("style",out var style) &&
                style.TryGetProperty("reviewRearViews",out var rear) && rear.GetBoolean())
                _views=[.._views,new("head-back",180,true),new("head-ear-left",-135,true),new("head-ear-right",135,true)];
            var recipeAssets=manifest.GetProperty("recipe").GetProperty("assets").EnumerateArray().ToArray();
            _model = GD.Load<PackedScene>(_asset).Instantiate<Node3D>(); AddChild(_model);
            var nodes = Descendants(_model).Prepend(_model).ToArray();
            _meshes.AddRange(nodes.OfType<MeshInstance3D>());
            foreach (var mesh in _meshes)
            {
                mesh.LodBias = 1000;
                var role=mesh.HasMeta("workflow_role") ? mesh.GetMeta("workflow_role").AsString() : "";
                if(role.Length==0 && manifest.TryGetProperty("meshRoles",out var roles))
                {
                    static string Canonical(string value)=>new(value.Where(char.IsLetterOrDigit).ToArray());
                    foreach(var entry in roles.EnumerateObject())
                        if(Canonical(mesh.Mesh.ResourceName).EndsWith(Canonical(entry.Name),StringComparison.Ordinal))role=entry.Value.GetString()!;
                }
                if (role.Length==0)
                {
                    // Old GLBs predate extras. Resolve from their own recipe, not a hairstyle name list.
                    foreach(var part in recipeAssets)
                        if(mesh.Name.ToString().Contains(part[1].GetString()!,StringComparison.OrdinalIgnoreCase))
                            role=part[2].GetString()!.ToLowerInvariant();
                    if(role.Length==0 && mesh.Name.ToString().StartsWith("Male_Baseline"))role="body";
                }
                if(role.Length==0)throw new InvalidDataException("Mesh has no character role: "+mesh.Name+" resource="+mesh.Mesh.ResourceName);
                _roles.Add(mesh,role);
                var count = mesh.Mesh.GetSurfaceCount();
                var materials = new Material?[count];
                for (var i=0;i<count;i++)
                {
                    var arrays = mesh.Mesh.SurfaceGetArrays(i);
                    var indices = arrays[(int)Mesh.ArrayType.Index].AsInt32Array();
                    var vertices = arrays[(int)Mesh.ArrayType.Vertex].AsVector3Array();
                    if (vertices.Length == 0 || vertices.Any(v => !v.IsFinite())) throw new InvalidDataException("Invalid geometry.");
                    _triangles += (indices.Length > 0 ? indices.Length : vertices.Length)/3;
                    materials[i] = mesh.GetActiveMaterial(i);
                    if (materials[i] is not BaseMaterial3D) throw new InvalidDataException("Unsupported material.");
                }
                _surfaces += count; _original.Add(mesh,materials);
            }
            if (_triangles != manifest.GetProperty("triangles").GetInt32() ||
                _surfaces != manifest.GetProperty("surfaces").GetInt32() ||
                _meshes.Count != manifest.GetProperty("meshInstances").GetInt32())
                throw new InvalidDataException("Imported geometry differs from manifest.");
            GetViewport().Msaa3D = Viewport.Msaa.Msaa4X;
            var environment = new Godot.Environment {
                BackgroundMode=Godot.Environment.BGMode.Color, BackgroundColor=new Color(.29f,.29f,.29f),
                AmbientLightSource=Godot.Environment.AmbientSource.Color,AmbientLightColor=Colors.White,
                AmbientLightEnergy=.65f,ReflectedLightSource=Godot.Environment.ReflectionSource.Disabled,
                TonemapMode=Godot.Environment.ToneMapper.Linear };
            AddChild(new WorldEnvironment { Environment=environment });
            AddLight(new Vector3(3,4,4),.8f); AddLight(new Vector3(-3,3,2),.4f); AddLight(new Vector3(0,3,-4),.3f);
            _camera = new Camera3D { Current=true, Near=.01f, Far=30, Projection=Camera3D.ProjectionType.Orthogonal };
            AddChild(_camera);
            var canvas=new CanvasLayer(); AddChild(canvas);
            _label=new Label { Position=new Vector2(12,12), Text=$"MPFB 静态审图 · {_version}\n1–8 视角 · G 灰模/材质 · 右键旋转 · 滚轮缩放 · Esc 退出" };
            _label.AddThemeFontSizeOverride("font_size",10);canvas.AddChild(_label);
            SetView(0);
            if (args.Contains("--character-capture")) await Capture(nodes);
        }
        catch(Exception e) { GD.PushError(e.ToString()); GD.Print("CHARACTER_BASELINE_REVIEW_FAIL "+e.Message); GetTree().Quit(1); }
    }

    private void AddLight(Vector3 position,float energy)
    {
        var light=new DirectionalLight3D { Position=position, LightEnergy=energy, ShadowEnabled=false };
        AddChild(light);light.LookAt(new Vector3(0,1.4f,0));
    }
    private void SetView(int index)
    {
        _view=index;_yaw=Mathf.DegToRad(_views[index].Angle);_pitch=0;
        _camera.Size=_views[index].Head ? .42f : _height*1.15f; UpdateCamera();
    }
    private void UpdateCamera()
    {
        var center=new Vector3(0,_views[_view].Head ? _height-.125f : _height/2,0);
        _camera.Position=center+new Vector3(3*Mathf.Sin(_yaw)*Mathf.Cos(_pitch),3*Mathf.Sin(_pitch),3*Mathf.Cos(_yaw)*Mathf.Cos(_pitch));
        _camera.LookAt(center);
    }
    private void SetMode(int mode)
    {
        _mode=mode;
        var gray=mode!=0;
        var clay=new StandardMaterial3D { AlbedoColor=new Color(.7f,.7f,.7f),Roughness=.8f };
        foreach(var mesh in _meshes)
        {
            var role=_roles[mesh];
            mesh.Visible=!(gray && (role is "eyebrows" or "eyelashes" || (mode==1 && role.StartsWith("hair",StringComparison.Ordinal))));
            for(var i=0;i<mesh.Mesh.GetSurfaceCount();i++)mesh.SetSurfaceOverrideMaterial(i,gray ? clay : _original[mesh][i]);
        }
    }
    public override void _UnhandledInput(InputEvent input)
    {
        if (_camera is null) return;
        if(input is InputEventKey key && key.Pressed && !key.Echo)
        {
            if(key.Keycode is >= Key.Key1 and <= Key.Key8)SetView((int)(key.Keycode-Key.Key1));
            if(key.Keycode==Key.G)SetMode((_mode+1)%(_styled?3:2));
            if(key.Keycode==Key.Escape){ VerifyIsolation();GetTree().Quit(); }
        }
        if(input is InputEventMouseButton button)
        {
            if(button.ButtonIndex==MouseButton.Right)_orbit=button.Pressed;
            if(button.Pressed && button.ButtonIndex is MouseButton.WheelUp or MouseButton.WheelDown)
                _camera.Size=Mathf.Clamp(_camera.Size*(button.ButtonIndex==MouseButton.WheelUp ?.9f:1.1f),.2f,4);
        }
        if(input is InputEventMouseMotion motion && _orbit)
        { _yaw-=motion.Relative.X*.006f;_pitch=Mathf.Clamp(_pitch+motion.Relative.Y*.004f,-1,1);UpdateCamera(); }
    }
    private async Task Frames(int count)
    { for(var i=0;i<count;i++)await ToSignal(GetTree(),SceneTree.SignalName.ProcessFrame); }
    private void VerifyIsolation()
    {
        if(SaveGameSerializer.Serialize(_session.State)!=_beforeState ||
            (_beforeSave is null ? File.Exists(_savePath) : !File.Exists(_savePath) || !_beforeSave.SequenceEqual(File.ReadAllBytes(_savePath))))
            throw new InvalidOperationException("Baseline review changed formal session/save.");
    }
    private async Task Capture(Node[] nodes)
    {
        _label.Visible=false;
        var output=ProjectSettings.GlobalizePath($"res://outputs/character-workflow/{_version}/godot/{DateTime.Now:yyyyMMdd-HHmmss}");
        Directory.CreateDirectory(output);
        foreach(var size in new[]{new Vector2I(1280,720),new Vector2I(1600,900),new Vector2I(1920,1080)})
        {
            GetWindow().Size=size;await Frames(5);
            foreach(var mode in Enumerable.Range(0,_styled?3:2))
            {
                SetMode(mode);
                var modeName=mode==0 ? "material" : mode==1 ? "gray" : "gray-hair";
                for(var v=0;v<_views.Length;v++)
                {
                    SetView(v);await Frames(3);await ToSignal(RenderingServer.Singleton,RenderingServer.SignalName.FramePostDraw);
                    using var image=GetViewport().GetTexture().GetImage();
                    var name=$"{size.Y}-{modeName}-{_views[v].Name}.png";
                    if(image.GetSize()!=size || image.SavePng(Path.Combine(output,name))!=Error.Ok)throw new IOException("Capture failed.");
                    _samples.Add(new {image=name,width=size.X,height=size.Y,modelTriangles=_triangles,surfaces=_surfaces,
                        rendererPrimitives=Performance.GetMonitor(Performance.Monitor.RenderTotalPrimitivesInFrame),
                        rendererDrawCalls=Performance.GetMonitor(Performance.Monitor.RenderTotalDrawCallsInFrame)});
                }
            }
        }
        VerifyIsolation();
        File.WriteAllText(Path.Combine(output,"results.json"),JsonSerializer.Serialize(new {
            asset=_asset,assetSha256=_hash,triangles=_triangles,meshInstances=_meshes.Count,surfaces=_surfaces,
            skeletons=nodes.OfType<Skeleton3D>().Count(),animationPlayers=nodes.OfType<AnimationPlayer>().Count(),
            renderer=RenderingServer.GetCurrentRenderingMethod(),device=RenderingServer.GetVideoAdapterName(),
            images=_samples,formalStateUnchanged=true,formalSaveUnchanged=true,visualApproval=false,dynamicsTested=false,
            targetHardwarePerformanceAccepted=false },new JsonSerializerOptions {WriteIndented=true}));
        GD.Print($"CHARACTER_BASELINE_REVIEW_PASS triangles={_triangles} surfaces={_surfaces} images={_samples.Count} stateUnchanged=true output={output}");
        GetTree().Quit();
    }
}
