using Godot;
using TownRpg.Application;
using TownRpg.Core.Domain;

namespace TownRpg.Presentation;

public partial class DreamPuzzlePanel : Control
{
    public event Action<string, string[]>? Submitted;
    public event Action<string, string[]>? PreviewChanged;
    public event Action<string>? Closed;
    private GameSession _session = null!;
    private IDisposable? _modalLease;
    private PuzzleWorkspace _workspace = null!;
    private Label _answer = null!;
    private Label _description = null!;
    private DreamPuzzleDiagram _diagram = null!;
    private GridContainer _choices = null!;
    private GridContainer _slots = null!;
    private ScrollContainer _choicesScroll = null!;
    private HBoxContainer _viewTabs = null!;
    private Button _observe = null!;
    private Button _arrange = null!;
    private Button _submit = null!;
    private Button _undo = null!;
    private string _puzzleId = "";
    private int _expectedLength;
    private bool _allowRepeats;
    private bool _numericInput;
    private bool _showObservation;
    private int _selectedSlot = -1;
    private readonly List<string> _tokens = [];
    private readonly List<string[]> _history = [];
    private readonly List<PuzzleHitButton> _slotButtons = [];
    private readonly Dictionary<string, Button> _choiceButtons = new(StringComparer.Ordinal);
    public IReadOnlyList<string> PreviewTokens => _tokens.ToArray();
    public PuzzleWorkspace Workspace => _workspace;
    public override void _Ready()
    {
        _session = GetNode<GameSession>("/root/GameSession");
        SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect); Visible = false;
        _workspace = new PuzzleWorkspace(); AddChild(_workspace);
        _description = UiStyles.Wrapped(""); UiStyles.Font(_description, 11); _workspace.Work.AddChild(_description);
        _answer = UiStyles.Wrapped(""); UiStyles.Font(_answer, 11); _answer.AddThemeColorOverride("font_color", UiStyles.Muted); _workspace.Work.AddChild(_answer);
        _viewTabs = new HBoxContainer(); _workspace.Work.AddChild(_viewTabs);
        _observe = UiStyles.Button(UiText.Get("UI_COOP_OBSERVATIONS")); _arrange = UiStyles.Button(UiText.Get("UI_ALIGNMENT"));
        foreach (var b in new[] { _observe, _arrange }) { b.ToggleMode = true; UiStyles.Font(b, 11); _viewTabs.AddChild(b); }
        _observe.Pressed += () => { _showObservation = true; Layout(); };
        _arrange.Pressed += () => { _showObservation = false; Layout(); _choiceButtons.Values.FirstOrDefault(b => !b.Disabled)?.GrabFocus(); };
        _diagram = new DreamPuzzleDiagram { Mode = DreamMechanismMode.ChannelCalibration, Values = [0], CustomMinimumSize = new Vector2(0, 70) };
        _workspace.Work.AddChild(_diagram);
        _slots = new GridContainer { Columns = 4 }; _slots.AddThemeConstantOverride("h_separation", 6); _workspace.Work.AddChild(_slots);
        _choicesScroll = UiStyles.Scroll(_workspace.Work);
        _choices = new GridContainer { Columns = 4, SizeFlagsHorizontal = SizeFlags.ExpandFill };
        _choices.AddThemeConstantOverride("h_separation", 6); _choices.AddThemeConstantOverride("v_separation", 4); _choicesScroll.AddChild(_choices);
        AddAction(UiText.Get("UI_RESET"), ClearAnswer); _undo = AddAction(UiText.Get("UI_UNDO"), UndoLast); AddAction(UiText.Get("UI_BACK"), ClosePanel);
        _workspace.Footer.AddSpacer(false);
        _submit = UiStyles.Button(UiText.Get("UI_CONFIRM")); _submit.Name = "PuzzleConfirm"; UiStyles.Primary(_submit);
        _submit.Pressed += SubmitAnswer; _workspace.Footer.AddChild(_submit);
    }
    private Button AddAction(string label, Action action)
    {
        var button = UiStyles.Button(label); UiStyles.Font(button, 11); button.Pressed += action; _workspace.Footer.AddChild(button); return button;
    }
    public void Open(string puzzleId, string title, string prompt, IReadOnlyList<(string Token, string Label)> choices, int expectedLength, bool allowRepeats = false)
    {
        _puzzleId = puzzleId; _expectedLength = expectedLength; _allowRepeats = allowRepeats;
        _numericInput = choices.All(c => c.Token.Length == 1 && char.IsAsciiDigit(c.Token[0]));
        _workspace.Heading.Text = title; _selectedSlot = -1; _showObservation = false; _tokens.Clear(); _history.Clear();
        // This is the girl's live observation, not a reference-document shortcut.
        _description.Text = puzzleId == DreamEscapeIds.PuzzleMusicCage &&
            _session.State.DreamEscape!.DiscoveredClues.Contains(DreamEscapeIds.ClueDoorMapping)
            ? DreamPrologueText.Clue(DreamEscapeIds.ClueDoorMapping) : prompt;
        _diagram.SideObserved = _session.State.DreamEscape!.DiscoveredClues.Contains(DreamEscapeIds.ClueMusicSideWave);
        _diagram.Configure(false); _diagram.QueueRedraw();
        _choiceButtons.Clear(); UiStyles.Clear(_choices); UiStyles.Clear(_slots); _slotButtons.Clear();
        for (var i = 0; i < expectedLength; i++)
        {
            var slot = i;
            var b = new PuzzleHitButton { Name = "AnswerSlot" + i, Slot = i, DragOwner = this, ToggleMode = true,
                SizeFlagsHorizontal = SizeFlags.ExpandFill, CustomMinimumSize = new Vector2(0, 48),
                AutowrapMode = TextServer.AutowrapMode.WordSmart };
            UiStyles.Font(b, 11); b.AddThemeStyleboxOverride("normal", UiStyles.Panel(UiStyles.Surface));
            b.Pressed += () => SelectSlot(slot); b.SwapRequested += SwapSlots; _slots.AddChild(b); _slotButtons.Add(b);
        }
        foreach (var choice in choices)
        {
            var button = UiStyles.Button(choice.Label); button.Name = "Choice_" + choice.Token.Replace('.', '_');
            button.CustomMinimumSize = new Vector2(0, 30); button.AutowrapMode = TextServer.AutowrapMode.WordSmart;
            button.SizeFlagsHorizontal = SizeFlags.ExpandFill; UiStyles.Font(button, _numericInput ? 14 : 12);
            var token = choice.Token; button.Pressed += () => AddToken(token); _choices.AddChild(button); _choiceButtons.Add(token, button);
        }
        _modalLease?.Dispose(); _modalLease = _session.Modal.Acquire("dream-puzzle");
        Visible = true; UpdateAnswer(); Layout(); UiStyles.FadeIn(this); _choiceButtons.Values.FirstOrDefault()?.GrabFocus();
    }
    public override void _Process(double delta) { if (Visible) Layout(); }
    private void Layout()
    {
        var calibration = _puzzleId == DreamEscapeIds.PuzzleMusicChannelCalibration;
        var separate = calibration && UiStyles.Scale(this) >= 1.2f;
        _viewTabs.Visible = separate;
        _diagram.Visible = calibration && (!separate || _showObservation);
        _diagram.SizeFlagsVertical = separate ? SizeFlags.ExpandFill : SizeFlags.Fill;
        _slots.Visible = !separate || !_showObservation; _choicesScroll.Visible = _slots.Visible;
        _observe.ButtonPressed = _showObservation; _arrange.ButtonPressed = !_showObservation;
        _slots.Columns = Math.Min(4, _expectedLength);
        _choices.Columns = _numericInput ? 5 : _workspace.Work.Size.X < 400 ? 2 : 4;
    }
    private void SelectSlot(int slot)
    {
        if (slot >= _tokens.Count) { _selectedSlot = -1; UpdateAnswer(); return; }
        if (_selectedSlot < 0) _selectedSlot = slot;
        else { SwapSlots(_selectedSlot, slot); _selectedSlot = -1; }
        UpdateAnswer();
    }
    private void SwapSlots(int first, int second)
    {
        if (first < 0 || second < 0 || first >= _tokens.Count || second >= _tokens.Count || first == second) return;
        _history.Add(_tokens.ToArray()); (_tokens[first], _tokens[second]) = (_tokens[second], _tokens[first]); _selectedSlot = -1; UpdateAnswer();
        UiStyles.FadeIn(_slots);
    }
    private void AddToken(string token)
    {
        if (_tokens.Count >= _expectedLength || (!_allowRepeats && _tokens.Contains(token, StringComparer.Ordinal))) return;
        _history.Add(_tokens.ToArray()); _tokens.Add(token); _selectedSlot = -1; UpdateAnswer();
        if (_tokens.Count == _expectedLength) _submit.GrabFocus();
    }
    private void ClearAnswer()
    {
        if (_tokens.Count == 0) return; _history.Add(_tokens.ToArray()); _tokens.Clear(); _selectedSlot = -1; UpdateAnswer();
    }
    private void UndoLast()
    {
        if (_history.Count == 0) return;
        _tokens.Clear(); _tokens.AddRange(_history[^1]); _history.RemoveAt(_history.Count - 1); _selectedSlot = -1; UpdateAnswer();
        _choiceButtons.Values.FirstOrDefault(b => !b.Disabled)?.GrabFocus();
    }
    private void UpdateAnswer()
    {
        _answer.Text = _tokens.Count == 0 ? UiText.Get(_numericInput ? "UI_NUMBER_HELP" : "UI_SEQUENCE_HELP") :
            UiText.Get("UI_SEQUENCE_PROGRESS", _tokens.Count, _expectedLength);
        for (var i = 0; i < _slotButtons.Count; i++)
        {
            var b = _slotButtons[i]; b.Text = (i + 1).ToString("00") + "\n" + (i < _tokens.Count ? _choiceButtons[_tokens[i]].Text : "—");
            b.TooltipText = UiText.Get("UI_SLOT", i + 1) + (i < _tokens.Count ? " · " + _choiceButtons[_tokens[i]].Text : "");
            b.Draggable = i < _tokens.Count; b.ButtonPressed = _selectedSlot == i;
        }
        _submit.Disabled = _tokens.Count != _expectedLength; _undo.Disabled = _history.Count == 0;
        foreach (var (token, button) in _choiceButtons) button.Disabled = !_allowRepeats && _tokens.Contains(token, StringComparer.Ordinal);
        if (_puzzleId.Length > 0) PreviewChanged?.Invoke(_puzzleId, _tokens.ToArray());
    }
    private void SubmitAnswer()
    {
        if (_tokens.Count != _expectedLength) return;
        var puzzleId = _puzzleId; var answer = _tokens.ToArray(); ClosePanel(); Submitted?.Invoke(puzzleId, answer);
    }
    public void ClosePanel()
    {
        var puzzleId = _puzzleId; UiStyles.ReleaseFocus(this);
        Visible = false; _puzzleId = ""; _tokens.Clear(); _history.Clear(); _selectedSlot = -1;
        _modalLease?.Dispose(); _modalLease = null; if (puzzleId.Length > 0) Closed?.Invoke(puzzleId);
    }
    public override void _Input(InputEvent e)
    {
        if (!Visible) return;
        if (e.IsActionPressed("ui_cancel"))
        {
            GetViewport().SetInputAsHandled();
            if (_selectedSlot >= 0) { _selectedSlot = -1; UpdateAnswer(); } else ClosePanel();
            return;
        }
        if (_showObservation) return;
        if (e is InputEventKey { Pressed: true, Echo: false } key)
        {
            if (key.Keycode == Key.Backspace) { GetViewport().SetInputAsHandled(); UndoLast(); return; }
            var digit = key.Unicode is >= (uint)'0' and <= (uint)'9' ? ((char)key.Unicode).ToString() : "";
            if (_numericInput && _choiceButtons.ContainsKey(digit)) { GetViewport().SetInputAsHandled(); AddToken(digit); }
        }
        else if (e is InputEventJoypadButton { Pressed: true, ButtonIndex: JoyButton.X })
        { GetViewport().SetInputAsHandled(); UndoLast(); }
    }
    public override void _ExitTree() { _modalLease?.Dispose(); _modalLease = null; }
}
