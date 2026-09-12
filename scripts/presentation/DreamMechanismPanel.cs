using Godot;
using TownRpg.Application;
using TownRpg.Core.Domain;
using TownRpg.Core.Services;

namespace TownRpg.Presentation;

public enum DreamMechanismMode { WindowLatches, Clock, Roster, StageAlignment, PaperRoll, PhotoOverlay, ChannelCalibration }

public partial class DreamMechanismPanel : Control
{
    public event Action<string, string[]>? Submitted;
    public event Action<string, string[]>? PreviewChanged;
    public event Action<string>? Closed;
    private GameSession _session = null!;
    private IDisposable? _modalLease;
    private PuzzleWorkspace _workspace = null!;
    private DreamPuzzleDiagram _diagram = null!;
    private Label _feedback = null!;
    private Label _help = null!;
    private HBoxContainer _views = null!;
    private ScrollContainer _feedbackScroll = null!;
    private Button _submit = null!;
    private Button _undo = null!;
    private Button _front = null!;
    private Button _side = null!;
    private string _puzzleId = "";
    private DreamMechanismMode _mode;
    private readonly List<int[]> _history = [];
    private int[] _indices = [];
    private int[] _initial = [];
    private int[]? _gestureStart;
    private int _selectedPiece = -1;
    public IReadOnlyList<string> PreviewTokens => CurrentAnswer();
    public PuzzleWorkspace Workspace => _workspace;

    public override void _Ready()
    {
        _session = GetNode<GameSession>("/root/GameSession");
        SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect); Visible = false;
        _workspace = new PuzzleWorkspace(); AddChild(_workspace);
        _help = UiStyles.Wrapped(""); UiStyles.Font(_help, 11); _help.AddThemeColorOverride("font_color", UiStyles.Muted); _workspace.Work.AddChild(_help);
        _views = new HBoxContainer(); _workspace.Work.AddChild(_views);
        _front = UiStyles.Button(UiText.Get("UI_FRONT_VIEW")); _front.ToggleMode = true;
        _side = UiStyles.Button(UiText.Get("UI_SIDE_VIEW")); _side.ToggleMode = true;
        _views.AddChild(_front); _views.AddChild(_side); UiStyles.Font(_front, 11); UiStyles.Font(_side, 11);
        _front.Pressed += () => ShowSide(false); _side.Pressed += () => ShowSide(true);
        _diagram = new DreamPuzzleDiagram { SizeFlagsVertical = SizeFlags.ExpandFill, CustomMinimumSize = new Vector2(0, 90) };
        _workspace.Work.AddChild(_diagram);
        _diagram.Activated += Activate;
        _diagram.SwapRequested += SwapPieces;
        _diagram.ValueRequested += SetValue;
        _diagram.GestureStarted += () => _gestureStart = (int[])_indices.Clone();
        _diagram.GestureEnded += commit =>
        {
            if (_gestureStart is null) return;
            if (!commit) _indices = _gestureStart;
            else if (!_indices.SequenceEqual(_gestureStart)) _history.Add(_gestureStart);
            _gestureStart = null; Refresh();
        };
        _feedbackScroll = new ScrollContainer { HorizontalScrollMode = ScrollContainer.ScrollMode.Disabled, CustomMinimumSize = new Vector2(0, 44) };
        _workspace.Work.AddChild(_feedbackScroll);
        _feedback = UiStyles.Wrapped(""); UiStyles.Font(_feedback, 11); _feedbackScroll.AddChild(_feedback);
        AddAction(UiText.Get("UI_RESET"), ResetValues);
        _undo = AddAction(UiText.Get("UI_UNDO"), Undo);
        AddAction(UiText.Get("UI_BACK"), ClosePanel);
        _workspace.Footer.AddSpacer(false);
        _submit = UiStyles.Button(UiText.Get("UI_CONFIRM")); _submit.Name = "MechanismConfirm"; UiStyles.Primary(_submit);
        _submit.Pressed += Submit; _workspace.Footer.AddChild(_submit);
    }
    private Button AddAction(string label, Action action)
    {
        var button = UiStyles.Button(label); UiStyles.Font(button, 11); button.Pressed += action; _workspace.Footer.AddChild(button); return button;
    }
    public void Open(string puzzleId, DreamMechanismMode mode)
    {
        _puzzleId = puzzleId; _mode = mode; _selectedPiece = -1; _gestureStart = null; _history.Clear();
        _initial = mode switch
        {
            DreamMechanismMode.WindowLatches => [1, 1, 1], DreamMechanismMode.Clock => [0, 0], DreamMechanismMode.Roster => [0, 0],
            DreamMechanismMode.StageAlignment => [0, 0, 0, 0, 0, 0, 0], DreamMechanismMode.PaperRoll => [0, 1, 2, 3],
            DreamMechanismMode.PhotoOverlay => [0, 0, 0, 0], _ => []
        };
        _indices = (int[])_initial.Clone();
        _workspace.Heading.Text = DreamPrologueText.Puzzle(puzzleId);
        _help.Text = UiText.Get(mode switch
        {
            DreamMechanismMode.WindowLatches => "UI_LATCH_HELP", DreamMechanismMode.Clock => "UI_CLOCK_HELP",
            DreamMechanismMode.Roster => "UI_ROSTER_HELP", DreamMechanismMode.PaperRoll => "UI_DRAG_SWAP",
            DreamMechanismMode.PhotoOverlay => "UI_PHOTO_HELP", _ => "UI_STAGE_HELP"
        });
        _diagram.Mode = mode; _diagram.Values = _indices;
        _diagram.SideObserved = _session.State.DreamEscape!.DiscoveredClues.Contains(DreamEscapeIds.ClueStageSideFrame);
        _diagram.ShowSideView = false; _diagram.Configure();
        _views.Visible = mode == DreamMechanismMode.StageAlignment; _front.ButtonPressed = true; _side.ButtonPressed = false;
        _feedbackScroll.Visible = mode is DreamMechanismMode.StageAlignment or DreamMechanismMode.PhotoOverlay;
        _modalLease?.Dispose(); _modalLease = _session.Modal.Acquire("dream-mechanism");
        Visible = true; UiStyles.FadeIn(this); Refresh(); _diagram.FirstControl?.GrabFocus();
    }
    private void ShowSide(bool side)
    {
        _diagram.ShowSideView = side; _front.ButtonPressed = !side; _side.ButtonPressed = side; _diagram.Refresh();
    }
    public override void _Process(double delta)
    {
        if (Visible) _help.Visible = _mode != DreamMechanismMode.StageAlignment || UiStyles.Scale(this) < 1.4f;
    }
    private void Activate(int index)
    {
        if (_mode == DreamMechanismMode.PaperRoll)
        {
            if (_selectedPiece < 0) { _selectedPiece = index; Refresh(); }
            else { SwapPieces(_selectedPiece, index); _selectedPiece = -1; Refresh(); }
            return;
        }
        if (_mode == DreamMechanismMode.Roster && index > 0) { SetValue(1, index - 1); return; }
        if (_mode == DreamMechanismMode.PhotoOverlay && index >= 3) { SetValue(3, index - 3); return; }
        var length = _mode switch
        {
            DreamMechanismMode.WindowLatches => 3, DreamMechanismMode.Clock => index == 0 ? 12 : 4,
            DreamMechanismMode.Roster => 5, DreamMechanismMode.StageAlignment => index < 4 ? 4 : 3,
            DreamMechanismMode.PhotoOverlay => 2, _ => 1
        };
        SetValue(index, (_indices[index] + 1) % length);
    }
    private void SetValue(int index, int value)
    {
        if (!Visible || index < 0 || index >= _indices.Length || _indices[index] == value) return;
        if (_gestureStart is null) _history.Add((int[])_indices.Clone());
        _indices[index] = value; Refresh();
    }
    private void SwapPieces(int first, int second)
    {
        if (_mode != DreamMechanismMode.PaperRoll || first == second || first < 0 || second < 0 || first >= 4 || second >= 4) return;
        _history.Add((int[])_indices.Clone()); (_indices[first], _indices[second]) = (_indices[second], _indices[first]);
        _selectedPiece = -1; _diagram.AnimateSnap(); Refresh();
    }
    private void ResetValues()
    {
        _diagram.CancelGesture(); if (_indices.SequenceEqual(_initial)) return;
        _history.Add((int[])_indices.Clone()); _indices = (int[])_initial.Clone(); _selectedPiece = -1; Refresh();
    }
    private void Undo()
    {
        _diagram.CancelGesture(); _selectedPiece = -1;
        if (_history.Count > 0) { _indices = _history[^1]; _history.RemoveAt(_history.Count - 1); }
        Refresh();
    }
    private void Refresh()
    {
        _diagram.Values = _indices.ToArray(); _diagram.SelectedPiece = _selectedPiece; _diagram.Refresh();
        _undo.Disabled = _history.Count == 0;
        _submit.Disabled = _mode == DreamMechanismMode.PhotoOverlay && _indices.Take(3).Any(value => value == 0);
        _feedback.Text = "";
        if (_mode == DreamMechanismMode.StageAlignment)
        {
            var observation = DreamStageRules.Observe(CurrentAnswer());
            _feedback.Text = DreamPrologueText.Format("DREAM_V6_UI_MECHANISMPANEL_053", Mark(observation.UpperFrame), Mark(observation.LowerFrame), Mark(observation.Rails), Mark(observation.Image));
            _feedback.Text += _diagram.SideObserved ? DreamPrologueText.Format("DREAM_V6_UI_MECHANISMPANEL_054", Mark(observation.SideMark)) : DreamPrologueText.Get("DREAM_V6_UI_MECHANISMPANEL_055");
        }
        else if (_mode == DreamMechanismMode.PhotoOverlay)
            _feedback.Text = DreamPrologueText.Get(_indices.Take(3).All(value => value == 1) ? "DREAM_V6_UI_MECHANISMPANEL_056" : "DREAM_V6_UI_MECHANISMPANEL_057");
        if (!string.IsNullOrEmpty(_puzzleId)) PreviewChanged?.Invoke(_puzzleId, CurrentAnswer());
    }
    private static string Mark(bool value) => DreamPrologueText.Get(value ? "DREAM_V6_UI_MECHANISMPANEL_059" : "DREAM_V6_UI_MECHANISMPANEL_060");
    private string[] CurrentAnswer() => _mode switch
    {
        DreamMechanismMode.WindowLatches => _indices.Select(i => new[] { "up", "middle", "down" }[i]).ToArray(),
        DreamMechanismMode.Clock when _indices.Length == 2 => [(_indices[0] + 1).ToString(), (_indices[1] * 15).ToString("00")],
        DreamMechanismMode.Roster when _indices.Length == 2 => [new[] { "left2", "left", "center", "right", "right2" }[_indices[0]], (_indices[1] + 1).ToString()],
        DreamMechanismMode.StageAlignment => _indices.Take(4).Select(v => (v + 1).ToString()).Concat(_indices.Skip(4).Select(v => new[] { "window", "figure", "bell" }[v])).ToArray(),
        DreamMechanismMode.PaperRoll => _indices.Select(v => (v + 1).ToString()).ToArray(),
        DreamMechanismMode.PhotoOverlay when _indices.Length == 4 => [new[] { "A3", "B2", "C4", "D1" }[_indices[3]]],
        _ => []
    };
    private void Submit()
    {
        if (_submit.Disabled) return; var answer = CurrentAnswer(); var puzzle = _puzzleId; ClosePanel(); Submitted?.Invoke(puzzle, answer);
    }
    public void ClosePanel()
    {
        _diagram.CancelGesture(); var puzzle = _puzzleId;
        UiStyles.ReleaseFocus(this); Visible = false; _puzzleId = ""; _selectedPiece = -1; _history.Clear();
        _modalLease?.Dispose(); _modalLease = null; if (puzzle.Length > 0) Closed?.Invoke(puzzle);
    }
    public override void _Input(InputEvent input)
    {
        if (!Visible) return;
        if (input.IsActionPressed("ui_cancel"))
        {
            GetViewport().SetInputAsHandled();
            if (_selectedPiece >= 0) { _selectedPiece = -1; Refresh(); } else ClosePanel();
        }
        else if (input is InputEventKey { Pressed: true, Echo: false, Keycode: Key.Backspace } ||
                 input is InputEventJoypadButton { Pressed: true, ButtonIndex: JoyButton.X })
        { GetViewport().SetInputAsHandled(); Undo(); }
    }
    public override void _ExitTree() { _modalLease?.Dispose(); _modalLease = null; }
}
