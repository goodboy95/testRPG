using Godot;
using TownRpg.Application;
using TownRpg.Core.Domain;

namespace TownRpg.Presentation;

public enum DreamMechanismMode
{
    WindowLatches,
    Clock,
    Roster,
    LineSetBalance
}

public partial class DreamMechanismPanel : Control
{
    public event Action<string, string[]>? Submitted;
    public event Action<string, string[]>? PreviewChanged;
    public event Action<string>? Closed;

    private GameSession _session = null!;
    private IDisposable? _modalLease;
    private Label _title = null!;
    private Label _prompt = null!;
    private HBoxContainer _controls = null!;
    private Button _submit = null!;
    private string _puzzleId = string.Empty;
    private DreamMechanismMode _mode;
    private readonly List<Button> _valueButtons = [];
    private readonly List<int[]> _history = [];
    private int[] _indices = [];

    private static readonly string[][] WindowValues =
    [
        ["上", "中", "下"], ["上", "中", "下"], ["上", "中", "下"]
    ];
    private static readonly string[][] ClockValues =
    [
        Enumerable.Range(1, 12).Select(value => value.ToString()).ToArray(),
        ["00", "15", "30", "45"]
    ];
    private static readonly string[][] RosterValues =
    [
        ["左二档", "左一档", "中央", "右一档", "右二档"],
        ["第1项", "第2项", "第3项", "第4项", "第5项"]
    ];
    private static readonly string[][] LineSetValues =
    [
        ["1", "2", "3", "4"], ["1", "2", "3", "4"],
        ["1", "2", "3", "4"], ["1", "2", "3", "4"]
    ];

    public override void _Ready()
    {
        _session = GetNode<GameSession>("/root/GameSession");
        SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect);
        Visible = false;
        AddChild(new ColorRect { Color = new Color("050b13d9"), LayoutMode = 1,
            AnchorsPreset = (int)LayoutPreset.FullRect, AnchorRight = 1, AnchorBottom = 1 });

        var frame = new PanelContainer { Position = new Vector2(82, 47), Size = new Vector2(476, 266) };
        frame.AddThemeStyleboxOverride("panel", UiStyles.Panel(new Color("192b38fa"), 9));
        AddChild(frame);
        var column = new VBoxContainer();
        column.AddThemeConstantOverride("separation", 10);
        frame.AddChild(column);
        _title = new Label();
        _title.AddThemeFontSizeOverride("font_size", 21);
        _title.AddThemeColorOverride("font_color", new Color("f2c66d"));
        column.AddChild(_title);
        _prompt = new Label { AutowrapMode = TextServer.AutowrapMode.WordSmart,
            CustomMinimumSize = new Vector2(440, 44) };
        column.AddChild(_prompt);
        _controls = new HBoxContainer { Alignment = BoxContainer.AlignmentMode.Center,
            CustomMinimumSize = new Vector2(440, 74) };
        _controls.AddThemeConstantOverride("separation", 12);
        column.AddChild(_controls);

        var actions = new HBoxContainer();
        actions.AddThemeConstantOverride("separation", 8);
        column.AddChild(actions);
        foreach (var (label, action) in new (string, Action)[]
                 { ("重置", ResetValues), ("撤销", Undo), ("取消", ClosePanel) })
        {
            var button = UiStyles.Button(label);
            button.SizeFlagsHorizontal = SizeFlags.ExpandFill;
            button.Pressed += action;
            actions.AddChild(button);
        }
        _submit = UiStyles.Button("确认");
        _submit.SizeFlagsHorizontal = SizeFlags.ExpandFill;
        _submit.Pressed += Submit;
        actions.AddChild(_submit);
    }

    public override void _UnhandledInput(InputEvent inputEvent)
    {
        if (!Visible) return;
        if (inputEvent.IsActionPressed("ui_cancel"))
        {
            GetViewport().SetInputAsHandled();
            ClosePanel();
        }
        else if (inputEvent is InputEventKey { Pressed: true, Echo: false, Keycode: Key.Backspace } ||
                 inputEvent is InputEventJoypadButton { Pressed: true, ButtonIndex: JoyButton.X })
        {
            GetViewport().SetInputAsHandled();
            Undo();
        }
    }

    public void Open(string puzzleId, DreamMechanismMode mode)
    {
        _puzzleId = puzzleId;
        _mode = mode;
        var windowPrompt = _session.State.DreamEscape!.DiscoveredClues
            .Contains(DreamEscapeIds.ClueClassroomWindow)
                ? "维护卡记录：从左到右，磨损示意位于上段—下段—中段。逐个调整三只窗扣，使位置与记录对应。"
                : "逐个调整三只窗扣。关联记录尚未收集；可在教室另一处的课桌区域寻找维护资料。";
        (_title.Text, _prompt.Text) = mode switch
        {
            DreamMechanismMode.WindowLatches => ("窗扣暗格", windowPrompt),
            DreamMechanismMode.Clock => ("旧时钟", "调整时针和分钟刻度，使时钟恢复到观察到的时刻。"),
            DreamMechanismMode.Roster => ("值日表滑片", "先让透明滑片与破损边对齐，再确认红圈落在哪一项。"),
            DreamMechanismMode.LineSetBalance => ("幕布吊杆配平",
                "依次调整 A、B、C、D 四组配重档位，使它们与后台磨损刻度一致。"),
            _ => throw new ArgumentOutOfRangeException(nameof(mode))
        };
        _indices = mode switch
        {
            DreamMechanismMode.WindowLatches => [1, 1, 1],
            DreamMechanismMode.Clock => [0, 0],
            DreamMechanismMode.Roster => [0, 0],
            DreamMechanismMode.LineSetBalance => [0, 0, 0, 0],
            _ => []
        };
        _history.Clear();
        RebuildControls();
        _modalLease?.Dispose();
        _modalLease = _session.Modal.Acquire("dream-mechanism");
        Visible = true;
        _valueButtons.FirstOrDefault()?.GrabFocus();
    }

    public void ClosePanel()
    {
        var closedPuzzle = _puzzleId;
        Visible = false;
        _puzzleId = string.Empty;
        _history.Clear();
        _modalLease?.Dispose();
        _modalLease = null;
        if (!string.IsNullOrEmpty(closedPuzzle)) Closed?.Invoke(closedPuzzle);
    }

    public override void _ExitTree()
    {
        _modalLease?.Dispose();
        _modalLease = null;
    }

    private string[][] Values => _mode switch
    {
        DreamMechanismMode.WindowLatches => WindowValues,
        DreamMechanismMode.Clock => ClockValues,
        DreamMechanismMode.Roster => RosterValues,
        DreamMechanismMode.LineSetBalance => LineSetValues,
        _ => []
    };

    private void RebuildControls()
    {
        foreach (var child in _controls.GetChildren()) child.QueueFree();
        _valueButtons.Clear();
        for (var index = 0; index < _indices.Length; index++)
        {
            var controlIndex = index;
            var button = UiStyles.Button(string.Empty);
            button.CustomMinimumSize = new Vector2(
                _mode is DreamMechanismMode.WindowLatches or DreamMechanismMode.LineSetBalance ? 96 : 160, 58);
            button.Pressed += () => Cycle(controlIndex);
            _controls.AddChild(button);
            _valueButtons.Add(button);
        }
        UpdateLabels();
    }

    private void Cycle(int index)
    {
        _history.Add((int[])_indices.Clone());
        _indices[index] = (_indices[index] + 1) % Values[index].Length;
        UpdateLabels();
    }

    private void ResetValues()
    {
        _history.Add((int[])_indices.Clone());
        Array.Fill(_indices, 0);
        UpdateLabels();
    }

    private void Undo()
    {
        if (_history.Count == 0) return;
        _indices = _history[^1];
        _history.RemoveAt(_history.Count - 1);
        UpdateLabels();
    }

    private void UpdateLabels()
    {
        for (var index = 0; index < _valueButtons.Count; index++)
        {
            var prefix = _mode switch
            {
                DreamMechanismMode.WindowLatches => $"窗扣 {index + 1}",
                DreamMechanismMode.Clock when index == 0 => "小时",
                DreamMechanismMode.Clock => "分钟",
                DreamMechanismMode.Roster when index == 0 => "滑片位置",
                DreamMechanismMode.LineSetBalance => $"{(char)('A' + index)} 组",
                _ => "红圈项目"
            };
            _valueButtons[index].Text = $"{prefix}\n{Values[index][_indices[index]]}";
        }
        if (!string.IsNullOrEmpty(_puzzleId)) PreviewChanged?.Invoke(_puzzleId, CurrentAnswer());
    }

    private void Submit()
    {
        var answer = CurrentAnswer();
        var puzzle = _puzzleId;
        ClosePanel();
        Submitted?.Invoke(puzzle, answer);
    }

    private string[] CurrentAnswer() => _mode switch
        {
            DreamMechanismMode.WindowLatches => _indices.Select(index => new[] { "up", "middle", "down" }[index]).ToArray(),
            DreamMechanismMode.Clock => [Values[0][_indices[0]], Values[1][_indices[1]]],
            DreamMechanismMode.Roster => [new[] { "left2", "left", "center", "right", "right2" }[_indices[0]],
                (_indices[1] + 1).ToString()],
            DreamMechanismMode.LineSetBalance => _indices.Select((value, index) => Values[index][value]).ToArray(),
            _ => []
        };
}
