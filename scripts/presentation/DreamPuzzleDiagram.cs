using Godot;

namespace TownRpg.Presentation;

/// <summary>Inspectable paper/optical schematics, with native keyboard focus and local pointer gestures.</summary>
public partial class DreamPuzzleDiagram : Control
{
    public DreamMechanismMode Mode { get; set; }
    public int[] Values { get; set; } = [];
    public bool SideObserved { get; set; }
    public bool ShowSideView { get; set; }
    public int SelectedPiece { get; set; } = -1;
    public event Action<int>? Activated;
    public event Action<int, int>? SwapRequested;
    public event Action<int, int>? ValueRequested;
    public event Action? GestureStarted;
    public event Action<bool>? GestureEnded;
    private readonly List<PuzzleHitButton> _hits = [];
    private bool _dragging;
    private int _dragIndex;
    private bool _interactive;
    public Control? FirstControl => _hits.FirstOrDefault(b => b.Visible);
    private int FontSize => Mathf.RoundToInt(11 * UiStyles.Scale(this));
    private Color Text => UiStyles.Ink;
    private Font Font => GetThemeDefaultFont();
    private void Caption(Vector2 p, string text, Color? color = null, float width = -1) =>
        DrawString(Font, p, text, HorizontalAlignment.Left, width, FontSize, color ?? Text);
    public override void _Ready() { MouseFilter = MouseFilterEnum.Stop; Resized += LayoutHits; }
    public void Configure(bool interactive = true)
    {
        CancelGesture(); _interactive = interactive;
        UiStyles.Clear(this); _hits.Clear();
        var count = !interactive ? 0 : Mode switch
        {
            DreamMechanismMode.WindowLatches => 3, DreamMechanismMode.Clock => 2,
            DreamMechanismMode.Roster => 6, DreamMechanismMode.PaperRoll => 4,
            DreamMechanismMode.PhotoOverlay => 7, DreamMechanismMode.StageAlignment => 7, _ => 0
        };
        for (var i = 0; i < count; i++)
        {
            var slot = i;
            var hit = new PuzzleHitButton { Name = Mode == DreamMechanismMode.PhotoOverlay && i >= 3 ? "PhotoCandidate" + (i - 3) : "MechanismValue" + i,
                Slot = i, DragOwner = this, Draggable = Mode == DreamMechanismMode.PaperRoll, FocusMode = FocusModeEnum.All,
                MouseDefaultCursorShape = CursorShape.PointingHand };
            if (Mode == DreamMechanismMode.Roster && i == 0) hit.MouseFilter = MouseFilterEnum.Ignore;
            var clear = new StyleBoxFlat { BgColor = Colors.Transparent };
            foreach (var state in new[] { "normal", "pressed", "hover_pressed" }) hit.AddThemeStyleboxOverride(state, clear);
            hit.AddThemeStyleboxOverride("hover", new StyleBoxFlat { BgColor = new Color(1, 1, 1, .06f) });
            hit.AddThemeStyleboxOverride("focus", new StyleBoxFlat { BgColor = Colors.Transparent, BorderColor = UiStyles.Accent,
                BorderWidthLeft = 2, BorderWidthRight = 2, BorderWidthTop = 2, BorderWidthBottom = 2 });
            hit.Pressed += () => Activated?.Invoke(slot);
            hit.SwapRequested += (from, to) => { SwapRequested?.Invoke(from, to); AnimateSnap(); };
            hit.GuiInput += e =>
            {
                if (e is InputEventMouseButton { Pressed: true, ButtonIndex: MouseButton.WheelDown or MouseButton.WheelUp } wheel &&
                    Mode is DreamMechanismMode.WindowLatches or DreamMechanismMode.Clock or DreamMechanismMode.StageAlignment)
                {
                    var length = Mode == DreamMechanismMode.Clock ? slot == 0 ? 12 : 4 : Mode == DreamMechanismMode.WindowLatches || slot >= 4 ? 3 : 4;
                    var value = (Values[slot] + (wheel.ButtonIndex == MouseButton.WheelUp ? 1 : length - 1)) % length;
                    ValueRequested?.Invoke(slot, value); hit.AcceptEvent();
                }
            };
            AddChild(hit); _hits.Add(hit);
        }
        LayoutHits(); QueueRedraw();
    }
    public void AnimateSnap()
    {
        if (UiStyles.ReducedMotion(this)) return;
        UiStyles.FadeIn(this);
    }
    public void Refresh()
    {
        for (var i = 0; i < _hits.Count; i++) _hits[i].TooltipText = Mode switch
        {
            DreamMechanismMode.PaperRoll => UiText.Get("UI_PIECE", Values[i] + 1),
            DreamMechanismMode.Clock => UiText.Get(i == 0 ? "UI_HOUR" : "UI_MINUTE"),
            DreamMechanismMode.WindowLatches => UiText.Get("UI_LATCH", i + 1),
            DreamMechanismMode.PhotoOverlay when i >= 3 => new[] { "A3", "B2", "C4", "D1" }[i - 3],
            _ => UiText.Get("UI_POSITION") + " " + (i + 1)
        };
        LayoutHits(); QueueRedraw();
    }
    public override void _Process(double delta) { if (IsVisibleInTree()) LayoutHits(); }
    private Rect2 PaperRect(int index) => new(8 + index * (Size.X - 16) / 4, 20, (Size.X - 16) / 4 - 6, Mathf.Max(65, Size.Y - 48));
    private Rect2 WeightRect(int i) => new(8 + i * Size.X * .52f / 4, 10, Size.X * .52f / 4 - 4, Mathf.Max(40, Size.Y - 64));
    private Rect2 DiscRect(int i) => new(8 + i * (Size.X - 16) / 3, Size.Y - 43, (Size.X - 16) / 3 - 6, 38);
    private Rect2 PhotoRect => new(12, 40, Size.X - 24, Mathf.Max(62, Size.Y - 78));
    private Vector2 PhotoPoint(int i)
    {
        Vector2[] points = [new(.42f, .22f), new(.13f, .65f), new(.73f, .65f), new(.32f, .88f)];
        return PhotoRect.Position + points[i] * PhotoRect.Size;
    }
    private void LayoutHits()
    {
        for (var i = 0; i < _hits.Count; i++)
        {
            var r = Mode switch
            {
                DreamMechanismMode.PaperRoll => PaperRect(i),
                DreamMechanismMode.WindowLatches => new Rect2(10 + i * (Size.X - 20) / 3, 10, (Size.X - 20) / 3 - 8, Size.Y - 20),
                DreamMechanismMode.Clock => new Rect2(Size.X * .65f, 12 + i * (Size.Y - 24) / 2, Size.X * .32f, (Size.Y - 24) / 2 - 6),
                DreamMechanismMode.Roster when i == 0 => new Rect2(8, 8, Size.X - 16, Size.Y * .58f),
                DreamMechanismMode.Roster => new Rect2(8 + (i - 1) * (Size.X - 16) / 5, Size.Y * .62f, (Size.X - 16) / 5 - 4, Size.Y * .32f),
                DreamMechanismMode.PhotoOverlay when i < 3 => new Rect2(8 + i * (Size.X - 16) / 3, 3, (Size.X - 16) / 3 - 4, 32),
                DreamMechanismMode.PhotoOverlay => new Rect2(PhotoPoint(i - 3) - new Vector2(23, 18), new Vector2(46, 36)),
                DreamMechanismMode.StageAlignment when i < 4 => WeightRect(i),
                DreamMechanismMode.StageAlignment => DiscRect(i - 4),
                _ => new Rect2()
            };
            var hit = _hits[i]; hit.Position = r.Position; hit.Size = r.Size;
            hit.Visible = !(Mode == DreamMechanismMode.StageAlignment && ShowSideView) &&
                !(Mode == DreamMechanismMode.PhotoOverlay && i >= 3 && (Values.Length == 0 || Values[1] == 0 || Values[0] == 0));
        }
    }
    public override void _Draw()
    {
        DrawRect(new Rect2(Vector2.Zero, Size), UiStyles.Surface);
        if (Values.Length == 0) return;
        switch (Mode)
        {
            case DreamMechanismMode.WindowLatches: DrawLatches(); break;
            case DreamMechanismMode.Clock: DrawClock(); break;
            case DreamMechanismMode.StageAlignment: DrawStage(); break;
            case DreamMechanismMode.PaperRoll: DrawPaper(); break;
            case DreamMechanismMode.PhotoOverlay: DrawPhoto(); break;
            case DreamMechanismMode.Roster: DrawRoster(); break;
            case DreamMechanismMode.ChannelCalibration: DrawWaves(); break;
        }
    }
    private void DrawLatches()
    {
        for (var i = 0; i < 3; i++)
        {
            var x = 10 + (i + .5f) * (Size.X - 20) / 3;
            Caption(new Vector2(x - 18, 28), (i + 1).ToString(), UiStyles.Muted);
            for (var j = 0; j < 3; j++)
            {
                var y = 47 + j * (Size.Y - 84) / 2;
                DrawLine(new Vector2(x - 20, y), new Vector2(x + 20, y), UiStyles.Rule, 2);
                Caption(new Vector2(x + 28, y + 4), new[] { "上", "中", "下" }[j], UiStyles.Muted);
            }
            DrawLine(new Vector2(x, 47), new Vector2(x, Size.Y - 37), UiStyles.Muted, 3);
            DrawRect(new Rect2(x - 17, 40 + Values[i] * (Size.Y - 84) / 2, 34, 14), UiStyles.Accent);
        }
    }
    private void DrawClock()
    {
        var center = new Vector2(Size.X * .31f, Size.Y * .5f);
        var radius = Mathf.Min(Size.Y * .43f, Size.X * .28f);
        DrawCircle(center, radius, UiStyles.Paper);
        for (var i = 0; i < 12; i++)
        {
            var angle = i * Mathf.Tau / 12 - Mathf.Pi / 2;
            var direction = new Vector2(Mathf.Cos(angle), Mathf.Sin(angle));
            DrawLine(center + direction * radius * .85f, center + direction * radius * .95f, UiStyles.PaperAccent, 2);
        }
        var hour = (Values[0] + 1) % 12; var minute = Values[1];
        var hourAngle = (hour + minute / 4f) * Mathf.Tau / 12 - Mathf.Pi / 2;
        var minuteAngle = minute * Mathf.Tau / 4 - Mathf.Pi / 2;
        DrawLine(center, center + new Vector2(Mathf.Cos(hourAngle), Mathf.Sin(hourAngle)) * radius * .52f, UiStyles.PaperInk, 4);
        DrawLine(center, center + new Vector2(Mathf.Cos(minuteAngle), Mathf.Sin(minuteAngle)) * radius * .8f, UiStyles.PaperAccent, 2);
        DrawCircle(center, 4, UiStyles.PaperInk);
        Caption(new Vector2(Size.X * .67f, Size.Y * .25f), UiText.Get("UI_HOUR"), UiStyles.Muted);
        Caption(new Vector2(Size.X * .67f, Size.Y * .25f + 28), (Values[0] + 1).ToString());
        Caption(new Vector2(Size.X * .67f, Size.Y * .7f), UiText.Get("UI_MINUTE"), UiStyles.Muted);
        Caption(new Vector2(Size.X * .67f, Size.Y * .7f + 28), (Values[1] * 15).ToString("00"));
    }
    private void DrawPaper()
    {
        (int left, int right)[] seams = [(2, 3), (0, 1), (3, 4), (1, 2)];
        int[] holes = [24, 38, 18, 42, 26];
        for (var i = 0; i < 4; i++)
        {
            var r = PaperRect(i); var piece = Values[i]; var (left, right) = seams[piece];
            DrawRect(r, UiStyles.Paper);
            if (SelectedPiece == i) DrawRect(r.Grow(2), UiStyles.Accent, false, 2);
            Caption(r.Position + new Vector2(8, 23), (piece + 1).ToString("00"), UiStyles.PaperAccent);
            var top = r.Position.Y + 40; var factor = (r.Size.Y - 48) / 55;
            for (var j = 0; j < 5; j++)
                DrawCircle(new Vector2(r.Position.X + 3 + j * (r.Size.X - 6) / 4, top + Mathf.Lerp(holes[left], holes[right], j / 4f) * factor), 2.6f, UiStyles.PaperInk);
            Edge(r.Position.X, left); Edge(r.End.X, right);
            void Edge(float x, int type)
            {
                if (type is 0 or 4) return;
                for (var j = 0; j < type; j++) DrawCircle(new Vector2(x, r.Position.Y + 34 + j * 18), 4.5f, UiStyles.Surface);
            }
        }
    }
    private void DrawRoster()
    {
        var x = 18 + (Values[0] - 2) * (Size.X - 36) / 10;
        var y = 18f; var w = Size.X - 36;
        DrawRect(new Rect2(18, y, w, Size.Y * .47f), UiStyles.Paper);
        DrawCircle(new Vector2(18, y + 16), 8, UiStyles.Surface);
        DrawRect(new Rect2(x, y, w, Size.Y * .47f), new Color(.79f, .59f, .44f, .14f));
        DrawArc(new Vector2(x, y + 16), 8, -Mathf.Pi / 2, Mathf.Pi / 2, 20, UiStyles.Accent, 2);
        DrawArc(new Vector2(x + w * .3f, y + 40), 20, 0, Mathf.Tau, 40, UiStyles.PaperAccent, 2);
        for (var i = 0; i < 5; i++)
        {
            var r = new Rect2(8 + i * (Size.X - 16) / 5, Size.Y * .62f, (Size.X - 16) / 5 - 4, Size.Y * .32f);
            DrawRect(r, Values[1] == i ? UiStyles.Accent : UiStyles.Rule);
            Caption(r.Position + new Vector2(r.Size.X / 2 - 5, r.Size.Y / 2 + 5), (i + 1).ToString(), Values[1] == i ? UiStyles.Background : Text);
        }
    }
    private void DrawPhoto()
    {
        var r = PhotoRect;
        for (var i = 0; i < 3; i++)
        {
            var x = 14 + i * (Size.X - 16) / 3;
            Caption(new Vector2(x, 25), (Values[i] == 1 ? "● " : "○ ") + UiText.Get(new[] { "UI_LAYER_NEGATIVE", "UI_LAYER_CARD", "UI_LAYER_FILTER" }[i]), Values[i] == 1 ? UiStyles.Accent : UiStyles.Muted);
        }
        DrawRect(r, UiStyles.Paper); if (Values[0] == 0) return;
        DrawRect(new Rect2(r.Position + r.Size * new Vector2(.32f, .04f), r.Size * new Vector2(.28f, .28f)), UiStyles.Rule);
        DrawRect(new Rect2(r.Position + r.Size * new Vector2(.02f, .6f), r.Size * new Vector2(.22f, .3f)), UiStyles.Muted);
        DrawRect(new Rect2(r.Position + r.Size * new Vector2(.83f, .14f), r.Size * new Vector2(.1f, .24f)), UiStyles.Accent);
        for (var i = 0; i < 4; i++)
        {
            var p = PhotoPoint(i); DrawCircle(p, 5, UiStyles.PaperInk);
            if (Values[1] == 1)
            {
                DrawArc(p, 12, 0, Mathf.Tau, 28, UiStyles.PaperAccent, 2);
                Caption(p + new Vector2(12, 8), new[] { "A3", "B2", "C4", "D1" }[i], UiStyles.PaperInk);
                if (Values[3] == i) DrawArc(p, 16, 0, Mathf.Tau, 30, UiStyles.PaperInk, 2);
            }
        }
        if (Values[2] == 1)
        {
            DrawLine(PhotoPoint(2), r.Position + r.Size * new Vector2(.85f, .3f), UiStyles.PaperAccent, 2);
            DrawLine(PhotoPoint(2), r.Position + r.Size * new Vector2(.58f, .2f), UiStyles.PaperAccent, 2);
        }
    }
    private void DrawStage()
    {
        if (ShowSideView)
        {
            Caption(new Vector2(16, 25), SideObserved ? UiText.Get("UI_SIDE_OBSERVATION") : DreamPrologueText.Get("DREAM_V6_UI_PUZZLEDIAGRAM_003"), UiStyles.Muted);
            if (!SideObserved) return;
            var a = Values[0] + 1; var c = Values[2] + 1;
            var baseline = Size.Y - 28; var step = (Size.Y - 70) / 5;
            DrawLine(new Vector2(20, baseline), new Vector2(Size.X - 20, baseline), UiStyles.Muted, 2);
            DrawRect(new Rect2(Size.X * .25f, baseline - a * step, 30, a * step), UiStyles.Muted);
            DrawRect(new Rect2(Size.X * .65f, baseline - c * step, 30, c * step), UiStyles.Accent);
            DrawLine(new Vector2(20, baseline - (a + 1) * step), new Vector2(Size.X - 20, baseline - (a + 1) * step), Text, 1);
            Caption(new Vector2(Size.X * .25f, Size.Y - 9), "A"); Caption(new Vector2(Size.X * .65f, Size.Y - 9), "C");
            return;
        }
        for (var i = 0; i < 4; i++)
        {
            var r = WeightRect(i); var center = r.Position.X + r.Size.X / 2;
            DrawLine(new Vector2(center, r.Position.Y + 12), new Vector2(center, r.End.Y - 22), UiStyles.Muted, 2);
            for (var j = 0; j < 4; j++)
            {
                var y = r.Position.Y + 12 + j * (r.Size.Y - 38) / 3;
                DrawLine(new Vector2(center - 8, y), new Vector2(center + 8, y), UiStyles.Muted, 1);
            }
            var height = Values[i] + 1; var knob = r.End.Y - 27 - Values[i] * (r.Size.Y - 38) / 3;
            DrawRect(new Rect2(center - 16, knob, 32, 10), UiStyles.Accent);
            Caption(new Vector2(center - 15, r.End.Y - 2), $"{(char)('A' + i)} {height}");
        }
        var projection = new Rect2(Size.X * .56f, 12, Size.X * .41f, Mathf.Max(60, Size.Y - 64));
        DrawRect(projection, UiStyles.Paper);
        var frontA = Values[0] + 1; var b = Values[1] + 1; var frontC = Values[2] + 1; var d = Values[3] + 1;
        var upper = projection.Position.Y + 10 + (frontA + frontC - 7) * 3;
        var lower = projection.End.Y - 10 + (b + d - 3) * 3;
        DrawLine(new Vector2(projection.Position.X + 5, upper), new Vector2(projection.End.X - 5, upper), UiStyles.PaperAccent, 2);
        DrawLine(new Vector2(projection.Position.X + 5, lower), new Vector2(projection.End.X - 5, lower), UiStyles.PaperAccent, 2);
        for (var i = 0; i < 3; i++)
        {
            var center = new Vector2(projection.Position.X + projection.Size.X * (i + .5f) / 3, projection.GetCenter().Y);
            if (Values[i + 4] == 0)
            {
                DrawRect(new Rect2(center - new Vector2(12, 16), new Vector2(24, 32)), UiStyles.PaperInk, false, 2);
                DrawLine(center - new Vector2(12, 0), center + new Vector2(12, 0), UiStyles.PaperInk);
                DrawLine(center - new Vector2(0, 16), center + new Vector2(0, 16), UiStyles.PaperInk);
            }
            else if (Values[i + 4] == 1)
            {
                DrawCircle(center - new Vector2(0, 10), 6, UiStyles.PaperInk);
                DrawLine(center - new Vector2(0, 3), center + new Vector2(0, 17), UiStyles.PaperInk, 4);
                DrawLine(center, center + new Vector2(12, 8), UiStyles.PaperInk, 3);
            }
            else
            {
                DrawArc(center, 12, Mathf.Pi, Mathf.Tau, 24, UiStyles.PaperInk, 2);
                DrawLine(center - new Vector2(12, 0), center - new Vector2(15, -10), UiStyles.PaperInk, 2);
                DrawLine(center + new Vector2(12, 0), center + new Vector2(15, 10), UiStyles.PaperInk, 2);
                DrawLine(center + new Vector2(-15, 10), center + new Vector2(15, 10), UiStyles.PaperInk, 2);
                DrawCircle(center + new Vector2(0, 13), 3, UiStyles.PaperInk);
            }
        }
        string[] names = ["DREAM_V6_UI_MECHANISMPANEL_020", "DREAM_V6_UI_MECHANISMPANEL_021", "DREAM_V6_UI_MECHANISMPANEL_022"];
        for (var i = 0; i < 3; i++)
        {
            var r = DiscRect(i); DrawRect(r, UiStyles.Rule);
            Caption(r.Position + new Vector2(10, 26), DreamPrologueText.Get(names[Values[i + 4]]), Text, r.Size.X - 16);
        }
    }
    private void DrawWaves()
    {
        float[][] waves = [[0, 0, 1, 0, 0, 1, 0, 0], [0, .25f, 0, .55f, 0, 1, 0, 0],
            [0, 1, .4f, .15f, 0, 0, 0, 0], [0, 1, .65f, .5f, .4f, .3f, .2f, .1f]];
        string[] glyphs = ["○", "◇", "□", "△"];
        for (var row = 0; row < 4; row++)
        {
            var y = 16 + row * (Size.Y - 24) / 4;
            Caption(new Vector2(10, y + 7), glyphs[row]);
            DrawLine(new Vector2(40, y), new Vector2(Size.X - 12, y), UiStyles.Rule);
            if (row == 3 && !SideObserved) continue;
            for (var i = 0; i < 7; i++)
                DrawLine(new Vector2(40 + i * (Size.X - 56) / 7, y - waves[row][i] * 12),
                    new Vector2(40 + (i + 1) * (Size.X - 56) / 7, y - waves[row][i + 1] * 12), UiStyles.Accent, 2);
        }
    }
    public override void _GuiInput(InputEvent input)
    {
        if (!_interactive) return;
        if (Mode == DreamMechanismMode.Roster && input is InputEventMouseButton { ButtonIndex: MouseButton.Left, Pressed: true } roster && roster.Position.Y < Size.Y * .6f)
        {
            _dragging = true; _dragIndex = -1; GestureStarted?.Invoke(); SetDial(roster.Position); AcceptEvent(); return;
        }
        if (Mode != DreamMechanismMode.Clock) return;
        if (input is InputEventMouseButton { ButtonIndex: MouseButton.Left, Pressed: true } mouse && mouse.Position.X < Size.X * .62f)
        {
            _dragging = true; GestureStarted?.Invoke(); var center = new Vector2(Size.X * .31f, Size.Y * .5f);
            var radius = Mathf.Min(Size.Y * .43f, Size.X * .28f);
            _dragIndex = mouse.Position.DistanceTo(center) < radius * .7f ? 0 : 1; SetDial(mouse.Position); AcceptEvent();
        }
    }
    public override void _Input(InputEvent input)
    {
        if (!_dragging) return;
        if (input is InputEventMouseMotion) { SetDial(GetLocalMousePosition()); GetViewport().SetInputAsHandled(); }
        else if (input is InputEventMouseButton { ButtonIndex: MouseButton.Left, Pressed: false })
        { _dragging = false; GestureEnded?.Invoke(true); GetViewport().SetInputAsHandled(); }
        else if (input.IsActionPressed("ui_cancel")) { CancelGesture(); GetViewport().SetInputAsHandled(); }
    }
    private void SetDial(Vector2 position)
    {
        if (_dragIndex == -1)
        {
            ValueRequested?.Invoke(0, Mathf.Clamp(Mathf.RoundToInt((position.X - Size.X / 2) / ((Size.X - 36) / 10)) + 2, 0, 4));
            return;
        }
        var offset = position - new Vector2(Size.X * .31f, Size.Y * .5f);
        var angle = Mathf.PosMod(Mathf.Atan2(offset.X, -offset.Y), Mathf.Tau);
        var value = _dragIndex == 0 ? ((int)Mathf.Round(angle / Mathf.Tau * 12) + 11) % 12 : (int)Mathf.Round(angle / Mathf.Tau * 4) % 4;
        ValueRequested?.Invoke(_dragIndex, value);
    }
    public void CancelGesture()
    {
        if (!_dragging) return; _dragging = false; GestureEnded?.Invoke(false);
    }
    public override void _Notification(int what)
    {
        if (what == NotificationWMWindowFocusOut || what == NotificationVisibilityChanged && !IsVisibleInTree()) CancelGesture();
    }
}
