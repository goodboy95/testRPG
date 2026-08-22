using Godot;
using TownRpg.Core.Domain;

namespace TownRpg.Presentation;

public partial class WallCutawayController : Node3D
{
    private const string ShaderPath = "res://assets/shaders/wall_cutaway.gdshader";
    private static readonly string[] WallNames = ["Wall_North", "Wall_South", "Wall_East", "Wall_West"];
    private static readonly IReadOnlyDictionary<int, IReadOnlySet<string>> NearWalls =
        new Dictionary<int, IReadOnlySet<string>>
        {
            [0] = new HashSet<string>(["Wall_South", "Wall_East"], StringComparer.Ordinal),
            [1] = new HashSet<string>(["Wall_South", "Wall_West"], StringComparer.Ordinal),
            [2] = new HashSet<string>(["Wall_North", "Wall_West"], StringComparer.Ordinal),
            [3] = new HashSet<string>(["Wall_North", "Wall_East"], StringComparer.Ordinal)
        };

    private readonly Dictionary<string, Node3D> _walls = new(StringComparer.Ordinal);
    private readonly Dictionary<string, List<MeshInstance3D>> _wallMeshes = new(StringComparer.Ordinal);
    private readonly Dictionary<MeshInstance3D, List<ShaderMaterial>> _wallMaterials = new();
    private readonly Dictionary<string, float> _alphas = new(StringComparer.Ordinal);
    private readonly Dictionary<string, float> _startAlphas = new(StringComparer.Ordinal);
    private readonly Dictionary<string, float> _targetAlphas = new(StringComparer.Ordinal);
    private QuarterTurnCameraRig _cameraRig = null!;
    private bool _reduceFlashing = true;
    private bool _transitioning;
    private float _elapsed;

    public int TexturedSurfaceCount { get; private set; }
    public int NormalMappedSurfaceCount { get; private set; }
    public int OrmSurfaceCount { get; private set; }

    public override void _Ready()
    {
        var model = GetNode<Node3D>("../ClassroomModel");
        _cameraRig = GetNode<QuarterTurnCameraRig>("../QuarterTurnCameraRig");
        var shader = ResourceLoader.Load<Shader>(ShaderPath)
            ?? throw new FileNotFoundException($"Wall cutaway shader is missing: {ShaderPath}");
        foreach (var wallName in WallNames)
        {
            var wall = model.FindChild(wallName, true, false) as Node3D
                ?? throw new InvalidOperationException($"Classroom GLB is missing wall node '{wallName}'.");
            _walls.Add(wallName, wall);
            var meshes = Descendants<MeshInstance3D>(wall).ToList();
            if (meshes.Count == 0)
                throw new InvalidOperationException($"Classroom wall '{wallName}' has no mesh children.");
            foreach (var mesh in meshes)
            {
                var materials = new List<ShaderMaterial>();
                var surfaceCount = mesh.Mesh?.GetSurfaceCount() ?? 0;
                for (var surface = 0; surface < surfaceCount; surface++)
                {
                    var source = mesh.GetActiveMaterial(surface) as BaseMaterial3D;
                    if (source?.AlbedoTexture is not null) TexturedSurfaceCount++;
                    if (source?.NormalTexture is not null) NormalMappedSurfaceCount++;
                    if (source is not null && GetPackedSurfaceTexture(source) is not null) OrmSurfaceCount++;
                    var material = CreateWallMaterial(shader, source);
                    mesh.SetSurfaceOverrideMaterial(surface, material);
                    materials.Add(material);
                }
                _wallMaterials.Add(mesh, materials);
            }
            _wallMeshes.Add(wallName, meshes);
            _alphas.Add(wallName, 1f);
            _startAlphas.Add(wallName, 1f);
            _targetAlphas.Add(wallName, 1f);
        }
        _cameraRig.RotationStarted += OnRotationStarted;
        _cameraRig.RotationCompleted += OnRotationCompleted;
        ApplyViewImmediate(_cameraRig.CurrentViewIndex);
    }

    public override void _ExitTree()
    {
        if (_cameraRig is null) return;
        _cameraRig.RotationStarted -= OnRotationStarted;
        _cameraRig.RotationCompleted -= OnRotationCompleted;
    }

    public override void _Process(double delta)
    {
        if (!_transitioning || _reduceFlashing) return;
        _elapsed += (float)delta;
        var duration = Math.Max(_cameraRig.RotationDuration, 0.001f);
        var weight = Mathf.Clamp(_elapsed / duration, 0f, 1f);
        weight = weight * weight * (3f - 2f * weight);
        foreach (var wallName in WallNames)
            SetWallAlpha(wallName, Mathf.Lerp(_startAlphas[wallName], _targetAlphas[wallName], weight));
        if (weight >= 1f) _transitioning = false;
    }

    public void ApplySettings(PlayerSettings settings)
    {
        _reduceFlashing = settings.ReduceFlashing;
        ApplyViewImmediate(_cameraRig.CurrentViewIndex);
    }

    public void ApplyViewImmediate(int viewIndex)
    {
        _transitioning = false;
        foreach (var wallName in WallNames)
            SetWallAlpha(wallName, NearWalls[viewIndex].Contains(wallName) ? 0f : 1f);
    }

    public float GetWallAlpha(string wallName) => _alphas.GetValueOrDefault(wallName, -1f);

    public IReadOnlyList<string> VisibleWalls() => WallNames.Where(name => _alphas[name] > 0.99f).ToArray();

    private void OnRotationStarted(int from, int to)
    {
        if (_reduceFlashing)
        {
            var hidden = new HashSet<string>(NearWalls[from], StringComparer.Ordinal);
            hidden.UnionWith(NearWalls[to]);
            foreach (var wallName in WallNames)
                SetWallAlpha(wallName, hidden.Contains(wallName) ? 0f : 1f);
            return;
        }
        _elapsed = 0f;
        _transitioning = true;
        foreach (var wallName in WallNames)
        {
            _startAlphas[wallName] = _alphas[wallName];
            _targetAlphas[wallName] = NearWalls[to].Contains(wallName) ? 0f : 1f;
        }
    }

    private void OnRotationCompleted(int viewIndex) => ApplyViewImmediate(viewIndex);

    private void SetWallAlpha(string wallName, float alpha)
    {
        alpha = Mathf.Clamp(alpha, 0f, 1f);
        _alphas[wallName] = alpha;
        _walls[wallName].Visible = alpha > 0.001f;
        foreach (var mesh in _wallMeshes[wallName])
        {
            foreach (var material in _wallMaterials[mesh])
                material.SetShaderParameter("cutaway_alpha", alpha);
            mesh.CastShadow = alpha >= 0.5f
                ? GeometryInstance3D.ShadowCastingSetting.On
                : GeometryInstance3D.ShadowCastingSetting.Off;
        }
    }

    private static ShaderMaterial CreateWallMaterial(Shader shader, BaseMaterial3D? source)
    {
        var color = new Color("666c63");
        var roughness = 0.9f;
        var metallic = 0f;
        Texture2D? albedo = null;
        Texture2D? normal = null;
        Texture2D? orm = null;
        var normalScale = 1f;
        var uvScale = Vector2.One;
        var uvOffset = Vector2.Zero;
        if (source is not null)
        {
            color = source.AlbedoColor;
            roughness = source.Roughness;
            metallic = source.Metallic;
            albedo = source.AlbedoTexture;
            normal = source.NormalTexture;
            normalScale = source.NormalScale;
            orm = GetPackedSurfaceTexture(source);
            uvScale = new Vector2(source.Uv1Scale.X, source.Uv1Scale.Y);
            uvOffset = new Vector2(source.Uv1Offset.X, source.Uv1Offset.Y);
        }
        var result = new ShaderMaterial { Shader = shader };
        result.SetShaderParameter("base_color", color);
        result.SetShaderParameter("use_albedo_texture", albedo is not null);
        result.SetShaderParameter("use_normal_texture", normal is not null);
        result.SetShaderParameter("use_orm_texture", orm is not null);
        if (albedo is not null) result.SetShaderParameter("albedo_texture", albedo);
        if (normal is not null) result.SetShaderParameter("normal_texture", normal);
        if (orm is not null) result.SetShaderParameter("orm_texture", orm);
        result.SetShaderParameter("uv_scale", uvScale);
        result.SetShaderParameter("uv_offset", uvOffset);
        result.SetShaderParameter("roughness_value", roughness);
        result.SetShaderParameter("metallic_value", metallic);
        result.SetShaderParameter("normal_scale", normalScale);
        result.SetShaderParameter("cutaway_alpha", 1f);
        return result;
    }

    private static Texture2D? GetPackedSurfaceTexture(BaseMaterial3D source) =>
        source.OrmTexture
        ?? source.RoughnessTexture
        ?? source.MetallicTexture
        ?? source.AOTexture;

    private static IEnumerable<T> Descendants<T>(Node node) where T : Node
    {
        foreach (var child in node.GetChildren())
        {
            if (child is T match) yield return match;
            foreach (var descendant in Descendants<T>(child)) yield return descendant;
        }
    }
}
