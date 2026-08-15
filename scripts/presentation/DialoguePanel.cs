using Godot;
using TownRpg.Application;

namespace TownRpg.Presentation;

public partial class DialoguePanel : Control
{
    private readonly List<string> _history = [];
    private Label _speaker = null!;
    private RichTextLabel _text = null!;
    private VBoxContainer _options = null!;
    private Button _continue = null!;
    private CheckButton _auto = null!;
    private CheckButton _skipRead = null!;
    private TaskCompletionSource<bool>? _advance;
    private TaskCompletionSource<int>? _choice;

    private GameSession Session => GetNode<GameSession>("/root/GameSession");

    public string HistoryText => string.Join("\n\n", _history.TakeLast(30));

    public override void _Ready()
    {
        SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect);
        MouseFilter = MouseFilterEnum.Ignore;

        var frame = new PanelContainer();
        frame.SetAnchorsPreset(LayoutPreset.BottomWide);
        frame.OffsetLeft = 16;
        frame.OffsetRight = -16;
        frame.OffsetTop = -230;
        frame.OffsetBottom = -10;
        frame.AddThemeStyleboxOverride("panel", UiStyles.Panel());
        frame.MouseFilter = MouseFilterEnum.Stop;
        AddChild(frame);

        var content = new VBoxContainer();
        content.AddThemeConstantOverride("separation", 5);
        frame.AddChild(content);

        _speaker = new Label();
        _speaker.AddThemeColorOverride("font_color", new Color("f2c66d"));
        _speaker.AddThemeFontSizeOverride("font_size", 16);
        content.AddChild(_speaker);

        _text = new RichTextLabel
        {
            BbcodeEnabled = true,
            FitContent = false,
            CustomMinimumSize = new Vector2(0, 54),
            ScrollActive = false,
            MouseFilter = MouseFilterEnum.Ignore
        };
        content.AddChild(_text);

        _options = new VBoxContainer();
        content.AddChild(_options);

        var toolbar = new HBoxContainer();
        content.AddChild(toolbar);
        _auto = new CheckButton { Text = "自动" };
        _skipRead = new CheckButton { Text = "跳已读" };
        toolbar.AddChild(_auto);
        toolbar.AddChild(_skipRead);
        toolbar.AddSpacer(false);
        _continue = UiStyles.Button("继续  ▶");
        _continue.CustomMinimumSize = new Vector2(110, 30);
        _continue.Pressed += () => _advance?.TrySetResult(true);
        toolbar.AddChild(_continue);

        Visible = false;
    }

    public async Task ShowLineAsync(string lineId, string speaker, string text)
    {
        CancelPending();
        ClearOptions();
        Visible = true;
        _speaker.Text = speaker;
        _text.Text = text;
        _continue.Visible = true;
        _history.Add($"[color=#f2c66d]{speaker}[/color]\n{text}");

        var wasRead = Session.State.ReadDialogueLines.Contains(lineId);
        Session.State.ReadDialogueLines.Add(lineId);
        if (_skipRead.ButtonPressed && wasRead)
        {
            await ToSignal(GetTree().CreateTimer(0.06), SceneTreeTimer.SignalName.Timeout);
            return;
        }

        if (_auto.ButtonPressed)
        {
            var speed = Math.Max(0.5f, Session.State.Settings.TextSpeed);
            var delay = Math.Clamp(text.Length * 0.045 / speed, 0.5, 3.8);
            await ToSignal(GetTree().CreateTimer(delay), SceneTreeTimer.SignalName.Timeout);
            return;
        }

        _advance = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
        _continue.GrabFocus();
        await _advance.Task;
        _advance = null;
    }

    public async Task<int> ShowChoiceAsync(
        string lineId,
        string speaker,
        string text,
        IReadOnlyList<string> choices)
    {
        CancelPending();
        ClearOptions();
        Visible = true;
        _speaker.Text = speaker;
        _text.Text = text;
        _continue.Visible = false;
        Session.State.ReadDialogueLines.Add(lineId);
        _history.Add($"[color=#f2c66d]{speaker}[/color]\n{text}");

        _choice = new TaskCompletionSource<int>(TaskCreationOptions.RunContinuationsAsynchronously);
        for (var index = 0; index < choices.Count; index++)
        {
            var captured = index;
            var button = UiStyles.Button(choices[index]);
            button.CustomMinimumSize = new Vector2(0, 27);
            button.TextOverrunBehavior = TextServer.OverrunBehavior.TrimEllipsis;
            button.Pressed += () => _choice?.TrySetResult(captured);
            _options.AddChild(button);
            if (index == 0)
            {
                button.GrabFocus();
            }
        }

        var selected = await _choice.Task;
        _choice = null;
        _history.Add($"[color=#9fc5d8]选择：{choices[selected]}[/color]");
        return selected;
    }

    public void CloseDialogue()
    {
        CancelPending();
        Visible = false;
        ClearOptions();
    }

    public override void _ExitTree() => CancelPending();

    private void CancelPending()
    {
        _advance?.TrySetCanceled();
        _advance = null;
        _choice?.TrySetCanceled();
        _choice = null;
    }

    private void ClearOptions()
    {
        if (_options is null)
        {
            return;
        }

        foreach (var child in _options.GetChildren())
        {
            child.QueueFree();
        }
    }
}
