using Godot;
using TownRpg.Application;

namespace TownRpg.Presentation;

public partial class DreamPuzzlePanel : Control
{
    public event Action<string, string[]>? Submitted;

    private GameSession _session = null!;
    private IDisposable? _modalLease;
    private Label _title = null!;
    private Label _prompt = null!;
    private Label _answer = null!;
    private GridContainer _choices = null!;
    private Button _submit = null!;
    private string _puzzleId = string.Empty;

    private int _expectedLength;
    private bool _allowRepeats;
    private bool _numericInput;
    private readonly List<string> _tokens = [];
    private readonly Dictionary<string, Button> _choiceButtons = new(StringComparer.Ordinal);

    public override void _ExitTree()
    {
        _modalLease?.Dispose();
        _modalLease = null;
    }

    public override void _Ready()
    {
        _session = GetNode<GameSession>("/root/GameSession");
        SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect);
        Visible = false;

        var shade = new ColorRect { Color = new Color("050b13d9") };
        shade.SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect);
        AddChild(shade);

        var frame = new PanelContainer
        {
            Position = new Vector2(72, 38),
            Size = new Vector2(496, 284)
        };
        frame.AddThemeStyleboxOverride("panel", UiStyles.Panel(new Color("192b38fa"), 9));
        AddChild(frame);

        var column = new VBoxContainer();
        column.AddThemeConstantOverride("separation", 7);
        frame.AddChild(column);
        _title = new Label();
        _title.AddThemeFontSizeOverride("font_size", 21);
        _title.AddThemeColorOverride("font_color", new Color("f2c66d"));
        column.AddChild(_title);
        _prompt = new Label
        {
            AutowrapMode = TextServer.AutowrapMode.WordSmart,
            CustomMinimumSize = new Vector2(450, 42)
        };
        column.AddChild(_prompt);
        _answer = new Label
        {
            Text = "尚未输入",
            HorizontalAlignment = HorizontalAlignment.Center,
            CustomMinimumSize = new Vector2(450, 32)
        };
        _answer.AddThemeStyleboxOverride("normal", UiStyles.Panel(new Color("0e1924e8"), 5));
        column.AddChild(_answer);
        _choices = new GridContainer { Columns = 5 };
        column.AddChild(_choices);

        var actions = new HBoxContainer();
        actions.AddThemeConstantOverride("separation", 8);
        column.AddChild(actions);
        var clear = UiStyles.Button("清除");
        clear.SizeFlagsHorizontal = SizeFlags.ExpandFill;
        clear.Pressed += ClearAnswer;
        actions.AddChild(clear);
        var undo = UiStyles.Button("撤销一项");
        undo.SizeFlagsHorizontal = SizeFlags.ExpandFill;
        undo.Pressed += UndoLast;
        actions.AddChild(undo);
        var cancel = UiStyles.Button("取消");
        cancel.SizeFlagsHorizontal = SizeFlags.ExpandFill;
        cancel.Pressed += ClosePanel;
        actions.AddChild(cancel);
        _submit = UiStyles.Button("确认排列");
        _submit.SizeFlagsHorizontal = SizeFlags.ExpandFill;
        _submit.Pressed += SubmitAnswer;
        actions.AddChild(_submit);
    }

    public override void _UnhandledInput(InputEvent inputEvent)
    {
        if (!Visible) return;
        if (inputEvent.IsActionPressed("ui_cancel"))
        {
            GetViewport().SetInputAsHandled();
            ClosePanel();
            return;
        }
        if (inputEvent is InputEventKey { Pressed: true, Echo: false } keyEvent)
        {
            if (keyEvent.Keycode == Key.Backspace)
            {
                GetViewport().SetInputAsHandled();
                UndoLast();
                return;
            }
            var digit = keyEvent.Unicode is >= (uint)'0' and <= (uint)'9'
                ? ((char)keyEvent.Unicode).ToString()
                : string.Empty;
            if (_numericInput && !string.IsNullOrEmpty(digit) && _choiceButtons.ContainsKey(digit))
            {
                GetViewport().SetInputAsHandled();
                AddToken(digit);
            }
            return;
        }
        if (inputEvent is InputEventJoypadButton { Pressed: true, ButtonIndex: JoyButton.X })
        {
            GetViewport().SetInputAsHandled();
            UndoLast();
        }
    }

    public void Open(
        string puzzleId,
        string title,
        string prompt,
        IReadOnlyList<(string Token, string Label)> choices,
        int expectedLength,
        bool allowRepeats = false)
    {
        _puzzleId = puzzleId;
        _expectedLength = expectedLength;
        _allowRepeats = allowRepeats;
        _numericInput = choices.All(choice => choice.Token.Length == 1 && char.IsAsciiDigit(choice.Token[0]));
        _title.Text = title;
        _prompt.Text = prompt;
        _tokens.Clear();
        _choiceButtons.Clear();
        foreach (var child in _choices.GetChildren())
        {
            child.QueueFree();
        }

        Button? first = null;
        foreach (var choice in choices)
        {
            var button = UiStyles.Button(choice.Label);
            button.CustomMinimumSize = new Vector2(82, 32);
            var token = choice.Token;
            button.Pressed += () => AddToken(token);
            _choices.AddChild(button);
            _choiceButtons[token] = button;
            first ??= button;
        }

        UpdateAnswer();
        _modalLease?.Dispose();
        _modalLease = _session.Modal.Acquire("dream-puzzle");
        Visible = true;
        first?.GrabFocus();
    }

    public void ClosePanel()
    {
        Visible = false;
        _puzzleId = string.Empty;
        _tokens.Clear();
        _modalLease?.Dispose();
        _modalLease = null;
    }

    private void AddToken(string token)
    {
        if (_tokens.Count >= _expectedLength)
        {
            return;
        }
        _tokens.Add(token);
        UpdateAnswer();
        if (_tokens.Count == _expectedLength) _submit.GrabFocus();
    }

    private void ClearAnswer()
    {
        _tokens.Clear();
        UpdateAnswer();
    }

    private void UndoLast()
    {
        if (_tokens.Count == 0) return;
        var removed = _tokens[^1];
        _tokens.RemoveAt(_tokens.Count - 1);
        UpdateAnswer();
        if (_choiceButtons.TryGetValue(removed, out var button) && !button.Disabled)
            Callable.From(button.GrabFocus).CallDeferred();
    }

    private void UpdateAnswer()
    {
        _answer.Text = _tokens.Count == 0
            ? _numericInput
                ? $"请选择 {_expectedLength} 位数字（可直接按数字键）"
                : $"请选择 {_expectedLength} 项"
            : string.Join("　→　", _tokens.Select(DisplayToken));
        _submit.Disabled = _tokens.Count != _expectedLength;
        foreach (var (token, button) in _choiceButtons)
            button.Disabled = !_allowRepeats && _tokens.Contains(token, StringComparer.Ordinal);
    }

    private static string DisplayToken(string token) => token switch
    {
        "woodblock" => "木鱼", "piano" => "钢琴", "triangle" => "三角铁", "handbell" => "手铃",
        "choir" => "合唱", "broadcast" => "广播", "art" => "美术", "track" => "田径",
        "curtain" => "窗帘", "clock" => "时钟", "bell" => "校铃", "register" => "点名册",
        _ => token
    };

    private void SubmitAnswer()
    {
        if (_tokens.Count != _expectedLength)
        {
            return;
        }
        var puzzleId = _puzzleId;
        var answer = _tokens.ToArray();
        ClosePanel();
        Submitted?.Invoke(puzzleId, answer);
    }
}
