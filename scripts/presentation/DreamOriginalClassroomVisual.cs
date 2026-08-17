using Godot;
using TownRpg.Application;
using TownRpg.Core.Domain;

namespace TownRpg.Presentation;

public partial class DreamOriginalClassroomVisual : Node2D, IDreamAreaVisual, IDreamCollisionLayout
{
    private const string BackgroundPath =
        "res://assets/generated/dream_escape/experimental/classroom-original-background-v1.png";
    private const string OccluderPath =
        "res://assets/generated/dream_escape/experimental/classroom-original-occluders-v1.png";

    private static readonly Rect2[] WallCollisions =
    [
        new Rect2(0, 0, 640, 192),
        new Rect2(0, 192, 16, 168),
        new Rect2(624, 192, 16, 18),
        new Rect2(624, 282, 16, 78),
        new Rect2(0, 350, 640, 10)
    ];

    private static readonly Rect2[] FurnitureCollisions =
    [
        new Rect2(126, 190, 105, 25),
        new Rect2(282, 220, 109, 31),
        new Rect2(68, 285, 89, 35),
        new Rect2(205, 285, 81, 35),
        new Rect2(313, 285, 93, 35),
        new Rect2(419, 285, 85, 35),
        new Rect2(525, 285, 85, 35),
        new Rect2(4, 335, 135, 25),
        new Rect2(192, 335, 105, 25),
        new Rect2(310, 335, 104, 25),
        new Rect2(421, 335, 110, 25),
        new Rect2(540, 335, 99, 25)
    ];

    private static readonly Vector2[] SafeWaypoints =
    [
        new Vector2(44, 232), new Vector2(105, 232), new Vector2(250, 188),
        new Vector2(350, 198), new Vector2(455, 198), new Vector2(590, 232),
        new Vector2(178, 260), new Vector2(300, 260), new Vector2(410, 260),
        new Vector2(515, 260), new Vector2(178, 314), new Vector2(300, 314),
        new Vector2(416, 314), new Vector2(535, 314)
    ];

    private static readonly Dictionary<string, Rect2> FocusRects = new(StringComparer.Ordinal)
    {
        ["pickup:dream.chalk"] = new Rect2(88, 191, 42, 30),
        ["target.classroom.blackboard"] = new Rect2(232, 58, 229, 103),
        ["observe:target.classroom.window"] = new Rect2(12, 67, 112, 193),
        ["target.classroom.window"] = new Rect2(12, 67, 112, 193),
        ["observe:target.classroom.clock"] = new Rect2(469, 43, 55, 55),
        ["target.classroom.clock"] = new Rect2(469, 43, 55, 55),
        ["observe:target.classroom.duty_roster"] = new Rect2(469, 89, 56, 59),
        ["target.classroom.duty_roster"] = new Rect2(469, 89, 56, 59),
        ["puzzle:teacher_desk"] = new Rect2(282, 174, 109, 77),
        ["target.classroom.map_cabinet"] = new Rect2(126, 65, 105, 149),
        ["target.classroom.class_photo"] = new Rect2(126, 65, 105, 149)
    };

    private Sprite2D _background = null!;
    private bool _chalkPickedUp;
    private bool _blackboardRevealed;
    private bool _teacherDeskOpen;
    private bool _mapCabinetOpen;
    private bool _reduceFlashing = true;
    private bool _highContrast;
    private bool _showCollisionDebug;
    private string? _focusedTargetId;
    private double _elapsed;

    public Rect2 MovementBounds => new(16, 178, 608, 152);
    public int CollisionBodyCount => WallCollisions.Length + FurnitureCollisions.Length;
    public int FurnitureCollisionCount => FurnitureCollisions.Length;

    public override void _Ready()
    {
        var session = GetNode<GameSession>("/root/GameSession");
        var backgroundTexture = ResourceLoader.Load<Texture2D>(BackgroundPath)
            ?? throw new FileNotFoundException($"Original classroom background is missing: {BackgroundPath}");
        var occluderTexture = ResourceLoader.Load<Texture2D>(OccluderPath)
            ?? throw new FileNotFoundException($"Original classroom occluders are missing: {OccluderPath}");

        ZIndex = 10;
        _background = new Sprite2D
        {
            Name = "OriginalBackground",
            Texture = backgroundTexture,
            Centered = false,
            TextureFilter = TextureFilterEnum.Linear,
            ZIndex = -40
        };
        AddChild(_background);
        BuildOccluders(occluderTexture);
        BuildCollisionBodies();
        ApplyState(session.State.DreamEscape
            ?? throw new InvalidOperationException("Original classroom visual requires dream state."));
        ApplySettings(session.State.Settings);
    }

    public override void _Process(double delta)
    {
        _elapsed += delta;
        var drift = _reduceFlashing ? 1f : 0.98f + 0.02f * Mathf.Sin((float)(_elapsed * Math.Tau / 8d));
        _background.Modulate = new Color(drift, drift, drift, 1f);
    }

    public override void _Draw()
    {
        DrawFixedClues();
        DrawPuzzleStates();
        DrawFocus();
    }

    public void ApplyState(DreamEscapeState state)
    {
        _chalkPickedUp = state.SolvedPuzzles.Contains("dream.pickup.chalk");
        _blackboardRevealed = state.SolvedPuzzles.Contains(DreamEscapeIds.PuzzleBlackboard);
        _teacherDeskOpen = state.SolvedPuzzles.Contains(DreamEscapeIds.PuzzleTeacherDesk);
        _mapCabinetOpen = state.SolvedPuzzles.Contains(DreamEscapeIds.PuzzleMapCabinet);
        QueueRedraw();
    }

    public void ApplySettings(PlayerSettings settings)
    {
        _reduceFlashing = settings.ReduceFlashing;
        _highContrast = settings.HighContrastPrompts;
        QueueRedraw();
    }

    public void SetFocusedTarget(string? targetId, bool highContrast)
    {
        if (_focusedTargetId == targetId && _highContrast == highContrast) return;
        _focusedTargetId = targetId;
        _highContrast = highContrast;
        QueueRedraw();
    }

    public bool IsBlocked(Vector2 playerPosition)
    {
        if (!MovementBounds.HasPoint(playerPosition)) return true;
        var feet = playerPosition + new Vector2(0, 16);
        return WallCollisions.Concat(FurnitureCollisions)
            .Any(rect => rect.Grow(5).HasPoint(feet));
    }

    public Vector2 ResolveSpawn(Vector2 requestedPosition)
    {
        if (!IsBlocked(requestedPosition)) return requestedPosition;
        return SafeWaypoints
            .Where(point => !IsBlocked(point))
            .OrderBy(point => point.DistanceSquaredTo(requestedPosition))
            .First();
    }

    public void SetCollisionDebug(bool visible)
    {
        _showCollisionDebug = visible;
        QueueRedraw();
    }

    private void BuildOccluders(Texture2D atlas)
    {
        if (GetParent() is Node2D parent) parent.YSortEnabled = true;
        var groups = new[]
        {
            ("MapCabinetOccluder", 0, 214),
            ("TeacherDeskOccluder", 1, 251),
            ("BackDeskOccluder", 2, 320),
            ("FrontDeskOccluder", 3, 360)
        };
        foreach (var (name, band, sortY) in groups)
        {
            var pivot = new Node2D { Name = name, Position = new Vector2(0, sortY) };
            pivot.AddChild(new Sprite2D
            {
                Texture = new AtlasTexture
                {
                    Atlas = atlas,
                    Region = new Rect2(0, band * 360, 640, 360)
                },
                Centered = false,
                Position = new Vector2(0, -sortY),
                TextureFilter = TextureFilterEnum.Linear
            });
            GetParent().CallDeferred(Node.MethodName.AddChild, pivot);
        }
    }

    private void BuildCollisionBodies()
    {
        var host = new Node2D { Name = "CollisionBodies" };
        AddChild(host);
        for (var index = 0; index < WallCollisions.Length; index++)
            AddCollisionBody(host, $"Wall{index + 1}", WallCollisions[index]);
        for (var index = 0; index < FurnitureCollisions.Length; index++)
            AddCollisionBody(host, $"Furniture{index + 1}", FurnitureCollisions[index]);
    }

    private static void AddCollisionBody(Node parent, string name, Rect2 rect)
    {
        var body = new StaticBody2D { Name = name };
        body.AddChild(new CollisionPolygon2D
        {
            Polygon =
            [
                rect.Position,
                new Vector2(rect.End.X, rect.Position.Y),
                rect.End,
                new Vector2(rect.Position.X, rect.End.Y)
            ]
        });
        parent.AddChild(body);
    }

    private void DrawFixedClues()
    {
        var dark = new Color("14191eea");
        var chalk = new Color("e8dfc7e8");
        DrawCircle(new Vector2(497, 69), 21, dark);
        DrawArc(new Vector2(497, 69), 21, 0, Mathf.Tau, 32, new Color("d4c9a8dd"), 1.5f);
        for (var index = 0; index < 12; index++)
        {
            var angle = index * Mathf.Tau / 12f;
            var from = new Vector2(497, 69) + Vector2.FromAngle(angle) * 16;
            var to = new Vector2(497, 69) + Vector2.FromAngle(angle) * 19;
            DrawLine(from, to, chalk, 1f);
        }
        DrawLine(new Vector2(497, 69), new Vector2(497, 53), chalk, 2f);
        DrawLine(new Vector2(497, 69), new Vector2(486, 80), new Color("e2ae55"), 2.5f);

        DrawCircle(new Vector2(481, 108), 5, new Color("9c3d3ddd"), false, 2f);
        foreach (var x in new[] { 65f, 82f, 99f })
            DrawLine(new Vector2(x, 142), new Vector2(x + 6, 159), new Color("e1b45ac8"), 1.5f);
    }

    private void DrawPuzzleStates()
    {
        var amber = new Color("efb95ce8");
        var chalk = new Color("e7dfcbe8");
        if (!_chalkPickedUp)
        {
            DrawLine(new Vector2(102, 210), new Vector2(116, 204), chalk, 3f);
        }
        if (_blackboardRevealed)
        {
            var y = 128f;
            DrawRect(new Rect2(285, y - 8, 16, 16), chalk, false, 1.5f);
            DrawLine(new Vector2(306, y), new Vector2(323, y), chalk, 1.5f);
            DrawLine(new Vector2(319, y - 4), new Vector2(323, y), chalk, 1.5f);
            DrawLine(new Vector2(319, y + 4), new Vector2(323, y), chalk, 1.5f);
            DrawCircle(new Vector2(337, y), 9, chalk, false, 1.5f);
            DrawLine(new Vector2(337, y), new Vector2(337, y - 7), chalk, 1.5f);
            DrawLine(new Vector2(337, y), new Vector2(331, y + 6), chalk, 1.5f);
            DrawLine(new Vector2(350, y), new Vector2(367, y), chalk, 1.5f);
            DrawLine(new Vector2(363, y - 4), new Vector2(367, y), chalk, 1.5f);
            DrawLine(new Vector2(363, y + 4), new Vector2(367, y), chalk, 1.5f);
            DrawRect(new Rect2(374, y - 10, 15, 20), chalk, false, 1.5f);
            DrawLine(new Vector2(377, y - 4), new Vector2(386, y - 4), chalk, 1f);
            DrawLine(new Vector2(377, y + 1), new Vector2(386, y + 1), chalk, 1f);
            DrawLine(new Vector2(377, y + 6), new Vector2(386, y + 6), chalk, 1f);
        }

        if (_teacherDeskOpen)
        {
            DrawRect(new Rect2(322, 205, 42, 15), new Color("101820ee"));
            DrawRect(new Rect2(325, 208, 36, 10), amber, false, 2f);
        }
        else
        {
            DrawRect(new Rect2(363, 191, 18, 12), new Color("111821ee"));
            DrawCircle(new Vector2(369, 197), 1.5f, amber);
            DrawCircle(new Vector2(375, 197), 1.5f, amber);
        }

        if (!_mapCabinetOpen)
        {
            DrawRect(new Rect2(126, 65, 105, 104), new Color("211c1af5"));
            DrawRect(new Rect2(132, 72, 93, 90), new Color("3b3128ff"));
            DrawRect(new Rect2(137, 77, 83, 80), new Color("6e5540ff"), false, 3f);
            DrawLine(new Vector2(178, 78), new Vector2(178, 156), new Color("211a18ff"), 2f);
            DrawCircle(new Vector2(184, 118), 2, amber);
        }
    }

    private void DrawFocus()
    {
        if (_focusedTargetId is not null && FocusRects.TryGetValue(_focusedTargetId, out var rect))
        {
            if (_highContrast)
            {
                DrawRect(rect.Grow(2), Colors.Black, false, 4f);
                DrawRect(rect, Colors.White, false, 2f);
            }
            else
            {
                DrawRect(rect, new Color("f2b85aff"), false, 2f);
            }
        }
        if (_showCollisionDebug)
        {
            DrawRect(MovementBounds, new Color("55dc9cff"), false, 1.5f);
            foreach (var collision in WallCollisions)
                DrawRect(collision, new Color("ff4c4c44"));
            foreach (var collision in FurnitureCollisions)
                DrawRect(collision, new Color("ffb13b66"));
        }
    }
}
