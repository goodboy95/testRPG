using Godot;
using TownRpg.Application;
using TownRpg.Core.Domain;

namespace TownRpg.Presentation;

public partial class DreamClassroomVisual : Node2D
{
    private const string TilePath =
        "res://assets/generated/dream_escape/pixel/school-interior-tiles-v1.png";
    private const string PropPath =
        "res://assets/generated/dream_escape/pixel/classroom-props-v1.png";
    private const string AmbientPath =
        "res://assets/generated/dream_escape/pixel/dream-ambient-v1.png";
    private const string OutlineShaderPath = "res://assets/shaders/pixel_outline.gdshader";

    private readonly Dictionary<string, Sprite2D> _focusSprites = new(StringComparer.Ordinal);
    private Sprite2D _chalk = null!;
    private Sprite2D _blackboard = null!;
    private Sprite2D _mapCabinet = null!;
    private Sprite2D _teacherDesk = null!;
    private Sprite2D _light = null!;
    private Sprite2D? _focusedSprite;
    private Shader _outlineShader = null!;
    private bool _reduceFlashing = true;
    private double _elapsed;

    public override void _Ready()
    {
        var session = GetNode<GameSession>("/root/GameSession");
        var tiles = ResourceLoader.Load<Texture2D>(TilePath)
            ?? throw new FileNotFoundException($"Dream school tile atlas is missing: {TilePath}");
        var props = ResourceLoader.Load<Texture2D>(PropPath)
            ?? throw new FileNotFoundException($"Dream classroom prop atlas is missing: {PropPath}");
        var ambient = ResourceLoader.Load<Texture2D>(AmbientPath)
            ?? throw new FileNotFoundException($"Dream ambient atlas is missing: {AmbientPath}");
        _outlineShader = ResourceLoader.Load<Shader>(OutlineShaderPath)
            ?? throw new FileNotFoundException($"Dream interaction outline shader is missing: {OutlineShaderPath}");

        BuildTileLayers(tiles);
        BuildProps(props);
        BuildAmbient(ambient);
        ApplyState(session.State.DreamEscape
            ?? throw new InvalidOperationException("Classroom visual requires dream escape state."));
        ApplySettings(session.State.Settings);
    }

    public override void _Process(double delta)
    {
        _elapsed += delta;
        if (_light is null) return;
        var drift = _reduceFlashing ? 1f : 0.98f + 0.02f * Mathf.Sin((float)(_elapsed * Math.Tau / 8d));
        _light.Modulate = new Color(drift, drift, drift, 1f);
    }

    public void ApplyState(DreamEscapeState state)
    {
        _chalk.Visible = !state.SolvedPuzzles.Contains("dream.pickup.chalk");
        _blackboard.RegionRect = state.SolvedPuzzles.Contains(DreamEscapeIds.PuzzleBlackboard)
            ? new Rect2(216, 0, 212, 58)
            : new Rect2(0, 0, 212, 58);
        _teacherDesk.RegionRect = state.SolvedPuzzles.Contains(DreamEscapeIds.PuzzleTeacherDesk)
            ? new Rect2(112, 160, 112, 72)
            : new Rect2(0, 160, 112, 72);
        _mapCabinet.RegionRect = state.SolvedPuzzles.Contains(DreamEscapeIds.PuzzleMapCabinet)
            ? new Rect2(96, 64, 96, 96)
            : new Rect2(0, 64, 96, 96);
    }

    public void ApplySettings(PlayerSettings settings)
    {
        _reduceFlashing = settings.ReduceFlashing;
        if (_focusedSprite is not null)
        {
            ApplyOutline(_focusedSprite, settings.HighContrastPrompts);
        }
    }

    public void SetFocusedTarget(string? targetId, bool highContrast)
    {
        var next = targetId is not null && _focusSprites.TryGetValue(targetId, out var sprite)
            ? sprite
            : null;
        if (ReferenceEquals(next, _focusedSprite)) return;
        if (_focusedSprite is not null) _focusedSprite.Material = null;
        _focusedSprite = next;
        if (_focusedSprite is not null) ApplyOutline(_focusedSprite, highContrast);
    }

    private void BuildTileLayers(Texture2D texture)
    {
        var tileSet = new TileSet { TileSize = new Vector2I(32, 32) };
        var atlas = new TileSetAtlasSource
        {
            Texture = texture,
            TextureRegionSize = new Vector2I(32, 32)
        };
        for (var y = 0; y < 8; y++)
        for (var x = 0; x < 8; x++)
        {
            atlas.CreateTile(new Vector2I(x, y));
        }
        var sourceId = tileSet.AddSource(atlas);

        var background = new TileMapLayer
        {
            Name = "BackgroundTiles",
            TileSet = tileSet,
            ZIndex = -3,
            CollisionEnabled = false,
            NavigationEnabled = false
        };
        AddChild(background);
        for (var y = 0; y < 12; y++)
        for (var x = 0; x < 20; x++)
        {
            var variant = x % 4;
            var atlasRow = y switch
            {
                <= 1 => 0,
                <= 3 => 1,
                4 => 4,
                >= 10 => 3,
                _ => 2
            };
            background.SetCell(new Vector2I(x, y), sourceId, new Vector2I(variant, atlasRow));
        }

        var details = new TileMapLayer
        {
            Name = "DetailTiles",
            TileSet = tileSet,
            ZIndex = -2,
            CollisionEnabled = false,
            NavigationEnabled = false
        };
        AddChild(details);
        for (var x = 0; x < 20; x++)
        {
            if (x is 0 or 19) details.SetCell(new Vector2I(x, 5), sourceId, new Vector2I(x == 0 ? 1 : 2, 5));
        }
        foreach (var cell in new[]
                 {
                     new Vector2I(3, 7), new Vector2I(8, 9), new Vector2I(14, 6), new Vector2I(17, 9)
                 })
        {
            details.SetCell(cell, sourceId, new Vector2I((cell.X + cell.Y) % 8, 6 + cell.X % 2));
        }
        details.SetCell(new Vector2I(9, 10), sourceId, new Vector2I(0, 5));
        details.SetCell(new Vector2I(10, 10), sourceId, new Vector2I(0, 5));
    }

    private void BuildProps(Texture2D atlas)
    {
        _blackboard = AddProp(atlas, "Blackboard", new Rect2(0, 0, 212, 58), new Vector2(214, 58));
        _mapCabinet = AddProp(atlas, "MapCabinet", new Rect2(0, 64, 96, 96), new Vector2(57, 64));
        var window = AddProp(atlas, "Window", new Rect2(192, 64, 96, 96), new Vector2(57, 174));
        var clock = AddProp(atlas, "Clock", new Rect2(288, 64, 64, 64), new Vector2(436, 60));
        var roster = AddProp(atlas, "DutyRoster", new Rect2(352, 64, 80, 96), new Vector2(515, 64));
        _teacherDesk = AddProp(atlas, "TeacherDesk", new Rect2(0, 160, 112, 72), new Vector2(464, 179));
        _chalk = AddProp(atlas, "Chalk", new Rect2(224, 160, 32, 16), new Vector2(189, 127), 3);

        for (var row = 0; row < 2; row++)
        for (var column = 0; column < 4; column++)
        {
            var variant = (row * 4 + column) % 4;
            AddProp(
                atlas,
                $"StudentDesk{row}{column}",
                new Rect2(256 + variant * 64, 160, 56, 48),
                new Vector2(199 + column * 65, 143 + row * 62));
        }

        MapFocus(_chalk, "pickup:dream.chalk");
        MapFocus(_blackboard, "target.classroom.blackboard");
        MapFocus(window, "observe:target.classroom.window", "target.classroom.window");
        MapFocus(clock, "observe:target.classroom.clock", "target.classroom.clock");
        MapFocus(roster, "observe:target.classroom.duty_roster", "target.classroom.duty_roster");
        MapFocus(_teacherDesk, "puzzle:teacher_desk");
        MapFocus(_mapCabinet, "target.classroom.map_cabinet", "target.classroom.class_photo");
    }

    private void BuildAmbient(Texture2D atlas)
    {
        _light = new Sprite2D
        {
            Name = "WindowLight",
            Texture = atlas,
            RegionEnabled = true,
            RegionRect = new Rect2(0, 32, 128, 32),
            Centered = false,
            Position = new Vector2(62, 250),
            Scale = new Vector2(1.65f, 1.2f),
            ZIndex = 1
        };
        AddChild(_light);

        var dustFrames = new SpriteFrames();
        dustFrames.RemoveAnimation("default");
        dustFrames.AddAnimation("drift");
        dustFrames.SetAnimationLoop("drift", true);
        dustFrames.SetAnimationSpeed("drift", 1.4d);
        for (var frame = 0; frame < 4; frame++)
        {
            dustFrames.AddFrame("drift", new AtlasTexture
            {
                Atlas = atlas,
                Region = new Rect2(frame * 32, 0, 32, 32)
            });
        }
        foreach (var position in new[] { new Vector2(165, 205), new Vector2(390, 255) })
        {
            var dust = new AnimatedSprite2D
            {
                SpriteFrames = dustFrames,
                Animation = "drift",
                Position = position,
                ZIndex = 3
            };
            AddChild(dust);
            dust.Play();
        }
    }

    private Sprite2D AddProp(
        Texture2D atlas,
        string name,
        Rect2 region,
        Vector2 position,
        int zIndex = 2)
    {
        var sprite = new Sprite2D
        {
            Name = name,
            Texture = atlas,
            RegionEnabled = true,
            RegionFilterClipEnabled = true,
            RegionRect = region,
            Centered = false,
            Position = position,
            ZIndex = zIndex
        };
        AddChild(sprite);
        return sprite;
    }

    private void MapFocus(Sprite2D sprite, params string[] targetIds)
    {
        foreach (var targetId in targetIds) _focusSprites[targetId] = sprite;
    }

    private void ApplyOutline(Sprite2D sprite, bool highContrast)
    {
        var material = new ShaderMaterial { Shader = _outlineShader };
        material.SetShaderParameter("outline_color",
            highContrast ? Colors.White : new Color("f2b85aff"));
        material.SetShaderParameter("high_contrast", highContrast);
        sprite.Material = material;
    }
}
