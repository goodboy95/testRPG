using Godot;
using System.Text.Json;
using TownRpg.Application;
using TownRpg.Core.Services;

namespace TownRpg.Testing;

/// <summary>Static, unrestricted art inspection. Does not create a player or an NPC actor.</summary>
public partial class ShenYanStaticReview : Node3D
{
    private const string Asset = "res://assets/generated/characters/shen-yan-v2/shen-yan-static-v2.glb";
    private Camera3D _camera = null!;
    private Godot.Environment _environment = null!;
    private DirectionalLight3D _key = null!, _fill = null!, _rim = null!;
    private Node3D _model = null!;
    private MeshInstance3D _floor = null!;
    private Label _hint = null!;
    private int _view, _lighting, _triangles, _surfaces;
    private bool _capture, _orbit;
    private float _yaw, _pitch = .06f, _distance = 3.5f;
    private readonly List<object> _samples = [];
    private readonly record struct View(string Name, Vector3 Position, Vector3 Target, float Size, bool Perspective = false);
    private static readonly View[] Views =
    [
        new("front", new(0, .92f, 3.5f), new(0, .88f, 0), 2.1f),
        new("threequarter", new(2.5f, 1.2f, 3.5f), new(0, .88f, 0), 2.1f),
        new("side", new(3.5f, .95f, 0), new(0, .88f, 0), 2.1f),
        new("back", new(0, .95f, -3.5f), new(0, .88f, 0), 2.1f),
        new("face", new(0, 1.58f, .85f), new(0, 1.558f, .035f), .43f),
        new("face-threequarter", new(.42f, 1.60f, .85f), new(0, 1.558f, .035f), .45f),
        new("third-person", new(2.8f, 2.0f, 5.5f), new(0, .95f, 0), 55, true),
    ];
    private static readonly string[] LightingNames = ["neutral", "day", "shade", "night"];

    public override void _Ready()
    {
        _capture = OS.GetCmdlineUserArgs().Contains("--shen-review-capture");
        CallDeferred(MethodName.Initialize);
    }

    private static IEnumerable<Node> Descendants(Node parent)
    {
        foreach (var child in parent.GetChildren())
        { yield return child; foreach (var next in Descendants(child)) yield return next; }
    }

    private async void Initialize()
    {
        try
        {
            GetViewport().Msaa3D = Viewport.Msaa.Msaa4X;
            _environment = new Godot.Environment
            {
                BackgroundMode = Godot.Environment.BGMode.Color,
                BackgroundColor = new Color("30383e"),
                AmbientLightSource = Godot.Environment.AmbientSource.Color,
                AmbientLightSkyContribution = 0,
                ReflectedLightSource = Godot.Environment.ReflectionSource.Disabled,
                TonemapMode = Godot.Environment.ToneMapper.Filmic,
                TonemapExposure = .95f,
            };
            AddChild(new WorldEnvironment { Environment = _environment });
            _key = Light("Key", new(3, 4, 4), true);
            _fill = Light("Fill", new(-3, 2, 3), false);
            _rim = Light("Rim", new(2, 3, -4), false);
            _floor = new MeshInstance3D
            {
                Name = "ReviewFloor",
                Mesh = new PlaneMesh { Size = new Vector2(200, 200) },
                MaterialOverride = new StandardMaterial3D { AlbedoColor = new Color("3a4249"), Roughness = .9f },
                CastShadow = GeometryInstance3D.ShadowCastingSetting.Off,
            };
            AddChild(_floor);
            _model = GD.Load<PackedScene>(Asset).Instantiate<Node3D>();
            AddChild(_model);
            var children = Descendants(_model).ToArray();
            var meshes = children.OfType<MeshInstance3D>().ToArray();
            if (meshes.Length != 1 || children.Any(n => n is Skeleton3D or AnimationPlayer))
                throw new InvalidDataException("Expected one static mesh with no skeleton or animations.");
            foreach (var mesh in meshes)
            {
                _surfaces += mesh.Mesh.GetSurfaceCount();
                for (var i = 0; i < mesh.Mesh.GetSurfaceCount(); i++)
                {
                    var arrays = mesh.Mesh.SurfaceGetArrays(i);
                    var indices = arrays[(int)Mesh.ArrayType.Index].AsInt32Array();
                    _triangles += indices.Length > 0 ? indices.Length / 3 : arrays[(int)Mesh.ArrayType.Vertex].AsVector3Array().Length / 3;
                    if (mesh.Mesh.SurfaceGetMaterial(i) is not BaseMaterial3D material)
                        throw new InvalidDataException("Unexpected imported material type.");
                    // glTF may import as either ORMMaterial3D or StandardMaterial3D
                    // with the same packed image assigned to separate channels.
                    var packedOrm = material.OrmTexture is not null;
                    var separateOrm = material.RoughnessTexture is not null && material.AOTexture is not null &&
                        material.RoughnessTextureChannel == BaseMaterial3D.TextureChannel.Green &&
                        material.AOTextureChannel == BaseMaterial3D.TextureChannel.Red &&
                        (material.MetallicTexture is not null && material.MetallicTextureChannel == BaseMaterial3D.TextureChannel.Blue || material.Metallic == 0);
                    if (material.AlbedoTexture is null || material.NormalTexture is null || !material.NormalEnabled || !(packedOrm || separateOrm))
                        throw new InvalidDataException($"Incomplete PBR material {material.ResourceName}: albedo={material.AlbedoTexture is not null}, normal={material.NormalTexture is not null}/{material.NormalEnabled}, ORM={packedOrm}/{separateOrm}.");
                }
            }
            if (_triangles is <= 0 or > 32_000 || _surfaces != 3)
                throw new InvalidDataException($"Static budget mismatch: {_triangles} triangles / {_surfaces} surfaces.");
            _camera = new Camera3D { Current = true, Near = .02f, Far = 60 };
            AddChild(_camera);
            var canvas = new CanvasLayer(); AddChild(canvas);
            _hint = new Label
            {
                Position = new Vector2(12, 12),
                Text = "沈砚 v2 · 静态候选（未通过美术确认）\n1—7 切换视角 · L 切换灯光 · 右键拖动旋转 · 滚轮缩放 · Esc 退出",
                Visible = !_capture,
            };
            _hint.AddThemeFontSizeOverride("font_size", 10); canvas.AddChild(_hint);
            SetLighting(0); SetView(0);
            if (_capture) await CaptureMatrix();
        }
        catch (Exception error)
        {
            GD.PushError(error.ToString());
            GD.Print("SHEN_STATIC_REVIEW_FAIL " + error.Message);
            GetTree().Quit(1);
        }
    }

    private DirectionalLight3D Light(string name, Vector3 position, bool shadows)
    {
        var light = new DirectionalLight3D
        {
            Name = name, Position = position, ShadowEnabled = shadows,
            DirectionalShadowMode = DirectionalLight3D.ShadowMode.Orthogonal,
            DirectionalShadowMaxDistance = 12,
        };
        AddChild(light); light.LookAt(new Vector3(0, 1, 0)); return light;
    }

    private void SetLighting(int index)
    {
        _lighting = index;
        // Neutral studio/shade are diffuse review conditions; day/night also
        // expose hard-shadow artefacts. They are all recorded separately.
        _key.ShadowEnabled = index is 1 or 3;
        (_environment.AmbientLightColor, _environment.AmbientLightEnergy) = index switch
        {
            1 => (new Color("809199"), .45f),
            2 => (new Color("a0b7d2"), .60f),
            3 => (new Color("617b9e"), .22f),
            _ => (new Color("d0d4da"), .30f),
        };
        _key.LightColor = new Color(index == 3 ? "ffd7a3" : index == 1 ? "fff0d7" : "fffaf1");
        _key.LightEnergy = index switch { 1 => .55f, 2 => .12f, 3 => .38f, _ => .68f };
        _fill.LightEnergy = index switch { 2 => .12f, 3 => .05f, _ => .22f };
        _fill.LightColor = new Color("d4e2f4");
        _rim.LightEnergy = index == 3 ? .18f : .30f;
        _rim.LightColor = new Color(index == 3 ? "6f8fbe" : "eee5dc");
    }

    private void SetView(int index)
    {
        _view = index; var v = Views[index];
        _floor.Visible = index is not (4 or 5);
        _camera.Projection = v.Perspective ? Camera3D.ProjectionType.Perspective : Camera3D.ProjectionType.Orthogonal;
        if (v.Perspective) _camera.Fov = v.Size; else _camera.Size = v.Size;
        _camera.Position = v.Position; _camera.LookAt(v.Target);
        _key.DirectionalShadowMaxDistance = v.Position.DistanceTo(v.Target)+1.8f;
        var offset = v.Position-v.Target;
        _distance = offset.Length(); _yaw = Mathf.Atan2(offset.X,offset.Z); _pitch = Mathf.Asin(offset.Y/_distance);
    }

    public override void _UnhandledInput(InputEvent input)
    {
        if (_capture || _camera is null) return;
        if (input is InputEventKey { Pressed: true, Echo: false } key)
        {
            if (key.Keycode is >= Key.Key1 and <= Key.Key7) SetView((int)(key.Keycode-Key.Key1));
            else if (key.Keycode == Key.L) SetLighting((_lighting+1)%LightingNames.Length);
            else if (key.Keycode == Key.Escape) GetTree().Quit();
        }
        if (input is InputEventMouseButton mouse)
        {
            if (mouse.ButtonIndex == MouseButton.Right) _orbit = mouse.Pressed;
            if (mouse.Pressed && mouse.ButtonIndex is MouseButton.WheelUp or MouseButton.WheelDown)
            {
                if (_camera.Projection == Camera3D.ProjectionType.Orthogonal)
                    _camera.Size = Mathf.Clamp(_camera.Size*(mouse.ButtonIndex == MouseButton.WheelUp ? .90f : 1.1f), .28f, 4);
                else
                    _camera.Fov = Mathf.Clamp(_camera.Fov*(mouse.ButtonIndex == MouseButton.WheelUp ? .90f : 1.1f),20,80);
            }
        }
        if (_orbit && input is InputEventMouseMotion motion)
        {
            _yaw -= motion.Relative.X*.008f; _pitch = Mathf.Clamp(_pitch+motion.Relative.Y*.005f, -.25f, .6f);
            var center = Views[_view].Target;
            _camera.Position = center + new Vector3(Mathf.Sin(_yaw)*Mathf.Cos(_pitch), Mathf.Sin(_pitch), Mathf.Cos(_yaw)*Mathf.Cos(_pitch))*_distance;
            _camera.LookAt(center);
        }
    }

    private async Task Frames(int count)
    { for (var i = 0; i < count; i++) await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame); }

    private async Task CaptureMatrix()
    {
        var session = GetNode<GameSession>("/root/GameSession");
        var before = SaveGameSerializer.Serialize(session.State);
        var save = ProjectSettings.GlobalizePath(GameSession.SavePath);
        var bytes = File.Exists(save) ? File.ReadAllBytes(save) : null;
        var output = ProjectSettings.GlobalizePath("res://outputs/shen-yan-v2/godot");
        Directory.CreateDirectory(output);
        var cases = Enumerable.Range(0, Views.Length).Select(v => (view: v, light: 0)).Concat(
            Enumerable.Range(1, 3).SelectMany(l => new[] { (view: 1, light: l), (view: 4, light: l) })).ToArray();
        foreach (var size in new[] { new Vector2I(1280,720), new Vector2I(1600,900), new Vector2I(1920,1080) })
        {
            GetWindow().Size = size; await Frames(5);
            foreach (var item in cases)
            {
                SetView(item.view); SetLighting(item.light); await Frames(4);
                await ToSignal(RenderingServer.Singleton, RenderingServer.SignalName.FramePostDraw);
                using var image = GetViewport().GetTexture().GetImage();
                var name = $"{size.Y}-{LightingNames[item.light]}-{Views[item.view].Name}.png";
                if (image.GetSize()!=size || image.SavePng(Path.Combine(output,name)) != Error.Ok)
                    throw new IOException("Review capture failed: "+name);
                _samples.Add(new { image=name, width=size.X, height=size.Y, modelTriangles=_triangles, materialSurfaces=_surfaces,
                    rendererPrimitives=Performance.GetMonitor(Performance.Monitor.RenderTotalPrimitivesInFrame),
                    rendererDrawCalls=Performance.GetMonitor(Performance.Monitor.RenderTotalDrawCallsInFrame) });
            }
        }
        if (SaveGameSerializer.Serialize(session.State)!=before ||
            (bytes is null ? File.Exists(save) : !bytes.SequenceEqual(File.ReadAllBytes(save))))
            throw new InvalidOperationException("Static review changed formal state or save bytes.");
        File.WriteAllText(Path.Combine(output,"results.json"),JsonSerializer.Serialize(new
        {
            asset=Asset, assetSha256=Convert.ToHexString(System.Security.Cryptography.SHA256.HashData(File.ReadAllBytes(ProjectSettings.GlobalizePath(Asset)))).ToLowerInvariant(),
            triangles=_triangles, materialSurfaces=_surfaces, meshInstances=1, skeletons=0, animations=0,
            device=RenderingServer.GetVideoAdapterName(), renderer=RenderingServer.GetCurrentRenderingMethod(),
            images=_samples, formalStateUnchanged=true, formalSaveUnchanged=true,
            visualApproved=false, targetHardwarePerformanceAccepted=false,
        },new JsonSerializerOptions { WriteIndented=true }));
        GD.Print($"SHEN_STATIC_REVIEW_PASS triangles={_triangles} surfaces={_surfaces} images={_samples.Count} stateUnchanged=true");
        GetTree().Quit();
    }
}
