using Godot;
using TownRpg.Application;
using TownRpg.Core.Domain;

namespace TownRpg.Presentation;

public partial class DialoguePanel : Control
{
    private readonly List<string> _history = [];
    private SubtitleView _subtitle = null!;
    private VBoxContainer _options = null!;
    private PanelContainer _choiceFrame = null!;
    private PanelContainer _tools = null!;
    private HBoxContainer _footer = null!;
    private Button _toolsButton = null!;
    private Button _continue = null!;
    private CheckButton _auto = null!;
    private CheckButton _skipRead = null!;
    private ReadingPanel _historyPanel = null!;
    private TaskCompletionSource<bool>? _advance;
    private TaskCompletionSource<int>? _choice;
    private Tween? _fade;
    private double _autoRemaining = -1;
    private string _speaker = "";
    private string _source = "";
    private IReadOnlyList<string> _pages = [""];
    private int _pageIndex;
    private float _menuScale = -1;
    private float _subtitleScale = -1;
    private SubtitleBackground _background;
    private bool _gamepad;
    private Control? _returnFocus;
    private GameSession Session => GetNode<GameSession>("/root/GameSession");
    public string HistoryText => string.Join("\n\n", _history.TakeLast(30));

    public override void _Ready()
    {
        SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect); MouseFilter = MouseFilterEnum.Ignore;
        UiStyles.UseCinematic(this, Session.State.Settings.FontScale);
        _subtitle = new SubtitleView { Name = "DialogueSubtitle" }; AddChild(_subtitle);
        _footer = new HBoxContainer { Name = "DialogueFooter" }; AddChild(_footer);
        _continue = UiStyles.Button(UiText.Get("UI_CONTINUE")); _continue.Name = "DialogueContinue";
        UiStyles.Role(_continue, UiTextRole.Auxiliary);
        _continue.Pressed += () => { if (!_tools.Visible && !_historyPanel.Visible) _advance?.TrySetResult(true); };
        _footer.AddChild(_continue);
        _toolsButton = UiStyles.Button(UiText.Get("UI_DIALOGUE_TOOLS_KEYBOARD")); _toolsButton.Name = "DialogueTools";
        UiStyles.Role(_toolsButton, UiTextRole.Auxiliary);
        _toolsButton.Pressed += () => ToggleTools(); AddChild(_toolsButton);
        _tools = new PanelContainer { Name = "DialogueToolbar", Visible = false }; AddChild(_tools);
        _tools.AddThemeStyleboxOverride("panel", UiStyles.Panel(new Color("171c1ef2")));
        var toolbar = new HBoxContainer(); toolbar.AddThemeConstantOverride("separation", 5); _tools.AddChild(toolbar);
        _auto = new CheckButton { Name = "DialogueAuto", Text = UiText.Get("UI_AUTO") };
        _skipRead = new CheckButton { Name = "DialogueSkipRead", Text = UiText.Get("UI_SKIP_READ") };
        UiStyles.Role(_auto, UiTextRole.Auxiliary); UiStyles.Role(_skipRead, UiTextRole.Auxiliary);
        toolbar.AddChild(_auto); toolbar.AddChild(_skipRead);
        var history = UiStyles.Button(UiText.Get("UI_HISTORY")); history.Name = "DialogueHistory";
        history.Pressed += () => _historyPanel.Open(UiText.Get("UI_DIALOGUE_HISTORY"), HistoryText,
            returnTo: () => { if (history.IsVisibleInTree()) history.GrabFocus(); });
        toolbar.AddChild(history);
        var back = UiStyles.Button(UiText.Get("UI_BACK")); back.Name = "DialogueToolsBack";
        back.Pressed += () => ToggleTools(false); toolbar.AddChild(back);
        _choiceFrame = new PanelContainer { Name = "DialogueChoices", Visible = false }; AddChild(_choiceFrame);
        _choiceFrame.AddThemeStyleboxOverride("panel", UiStyles.Panel(Colors.Transparent));
        var scroll = UiStyles.Scroll(_choiceFrame);
        _options = new VBoxContainer { SizeFlagsHorizontal = SizeFlags.ExpandFill }; scroll.AddChild(_options);
        _historyPanel = new ReadingPanel { PlainText = true }; AddChild(_historyPanel);
        ApplyPreferences(); Visible = false;
    }

    public override void _Process(double delta)
    {
        if (!Visible) return;
        ApplyPreferences(); LayoutDialogue();
        if (_autoRemaining >= 0 && !_tools.Visible && !_historyPanel.Visible && _auto.ButtonPressed && GetWindow().HasFocus())
        {
            _autoRemaining -= delta;
            if (_autoRemaining <= 0) { _autoRemaining = -1; _advance?.TrySetResult(true); }
        }
    }

    public override void _Input(InputEvent e)
    {
        if (!Visible || _historyPanel.Visible) return;
        if (e is InputEventJoypadButton { Pressed: true }) SetGamepad(true);
        else if (e is InputEventKey { Pressed: true } or InputEventMouseButton { Pressed: true }) SetGamepad(false);
        if (e is InputEventKey { Pressed: true, Echo: false, Keycode: Key.H } or InputEventJoypadButton { Pressed: true, ButtonIndex: JoyButton.Y })
        { ToggleTools(); GetViewport().SetInputAsHandled(); }
        else if (e.IsActionPressed("ui_cancel"))
        {
            GetViewport().SetInputAsHandled();
            if (_tools.Visible) ToggleTools(false); else CloseDialogue();
        }
    }

    private void SetGamepad(bool gamepad)
    {
        if (_gamepad == gamepad) return;
        _gamepad = gamepad;
        _toolsButton.Text = UiText.Get(gamepad ? "UI_DIALOGUE_TOOLS_GAMEPAD" : "UI_DIALOGUE_TOOLS_KEYBOARD");
    }

    private void ToggleTools(bool? open = null)
    {
        var show = open ?? !_tools.Visible;
        if (show && !_tools.Visible) _returnFocus = GetViewport().GuiGetFocusOwner();
        _tools.Visible = show;
        _continue.Disabled = _tools.Visible;
        foreach (var option in _options.GetChildren().OfType<Button>()) option.Disabled = _tools.Visible;
        if (_tools.Visible) _auto.GrabFocus();
        else if (GodotObject.IsInstanceValid(_returnFocus) && _returnFocus!.IsVisibleInTree() &&
                 (_returnFocus == _continue || _returnFocus.GetParent() == _options)) _returnFocus.GrabFocus();
        else RestoreDialogueFocus();
        LayoutDialogue();
    }

    private void ApplyPreferences()
    {
        var settings = Session.State.Settings;
        var sizeChanged = _subtitleScale != settings.EffectiveSubtitleFontScale;
        var changed = sizeChanged || _background != settings.SubtitleBackground;
        if (_menuScale != settings.FontScale)
        { _menuScale = settings.FontScale; UiStyles.UseCinematic(this, _menuScale); }
        if (!changed) return;
        _subtitleScale = settings.EffectiveSubtitleFontScale; _background = settings.SubtitleBackground;
        _subtitle.Configure(_subtitleScale);
        // Re-read the active line if its size changes; never skip undisplayed text.
        if (sizeChanged)
        { _pages = _subtitle.Paginate(_source); _pageIndex = _choice is null ? 0 : _pages.Count - 1; }
        ShowCurrentPage(false);
    }

    private void LayoutDialogue()
    {
        _subtitle.Position = new Vector2((640 - _subtitle.Size.X) / 2, 324 - _subtitle.Size.Y);
        _footer.Size = _footer.GetCombinedMinimumSize();
        _footer.Position = new Vector2((640 - _footer.Size.X) / 2, Math.Min(336, 356 - _footer.Size.Y));
        _toolsButton.Size = _toolsButton.GetCombinedMinimumSize();
        _toolsButton.Position = new Vector2(620 - _toolsButton.Size.X, 356 - _toolsButton.Size.Y);
        _tools.Size = _tools.GetCombinedMinimumSize();
        _tools.Position = new Vector2(620 - _tools.Size.X, _subtitle.Position.Y - _tools.Size.Y - 10);
        var choiceHeight = Mathf.Clamp(_options.GetCombinedMinimumSize().Y + 12, 32, Math.Min(120, _subtitle.Position.Y - 28));
        var choiceWidth = Mathf.Clamp(_options.GetChildren().OfType<Button>().Select(b =>
            b.GetThemeDefaultFont().GetStringSize(b.Text, fontSize: b.GetThemeDefaultFontSize()).X + 40).DefaultIfEmpty(180).Max(), 180, 420);
        _choiceFrame.Size = new Vector2(choiceWidth, choiceHeight);
        _choiceFrame.Position = new Vector2((640 - choiceWidth) / 2, _subtitle.Position.Y - choiceHeight - 12);
        if (_tools.Visible && _choiceFrame.Visible) _tools.Position = new Vector2(620 - _tools.Size.X, 12);
    }

    public async Task ShowLineAsync(string lineId, string speaker, string text)
    {
        CancelPending(); ClearOptions(); Visible = true; ApplyPreferences();
        var wasRead = Session.State.ReadDialogueLines.Contains(lineId);
        _history.Add($"{speaker}\n{text}");
        await ShowPages(speaker, text, wasRead && _skipRead.ButtonPressed);
        Session.State.ReadDialogueLines.Add(lineId);
    }

    private async Task ShowPages(string speaker, string text, bool skipRead)
    {
        _speaker = speaker; _source = text; _pages = _subtitle.Paginate(text);
        _continue.Visible = true;
        for (_pageIndex = 0; _pageIndex < _pages.Count; _pageIndex++)
        {
            ShowCurrentPage(true);
            if (skipRead) continue;
            var pending = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
            _advance = pending;
            _autoRemaining = Math.Clamp(_pages[_pageIndex].Length * .06 / Math.Max(.5f, Session.State.Settings.TextSpeed), 1.2, 8);
            _continue.GrabFocus(); await pending.Task;
            _advance = null; _autoRemaining = -1;
        }
    }

    private void ShowCurrentPage(bool animate)
    {
        var index = Math.Clamp(_pageIndex, 0, _pages.Count - 1);
        _subtitle.ShowPage(_speaker, _pages[index], _subtitleScale, _background);
        _continue.Text = index + 1 < _pages.Count ? UiText.Get("UI_NEXT_PAGE", index + 1, _pages.Count) : UiText.Get("UI_CONTINUE");
        _fade?.Kill(); _subtitle.Modulate = Colors.White;
        if (animate && !UiStyles.ReducedMotion(this))
        {
            _subtitle.Modulate = new Color(1, 1, 1, 0);
            _fade = CreateTween(); _fade.TweenProperty(_subtitle, "modulate:a", 1f, .12);
        }
        LayoutDialogue();
    }

    public async Task<int> ShowChoiceAsync(string lineId, string speaker, string text, IReadOnlyList<string> choices)
    {
        if (choices.Count == 0) throw new ArgumentException("Dialogue choices must not be empty.", nameof(choices));
        CancelPending(); ClearOptions(); Visible = true; ApplyPreferences();
        _history.Add($"{speaker}\n{text}");
        _speaker = speaker; _source = text; _pages = _subtitle.Paginate(text); _pageIndex = 0;
        if (_pages.Count > 1) await ShowPages(speaker, text, false); else ShowCurrentPage(true);
        Session.State.ReadDialogueLines.Add(lineId);
        _continue.Visible = false; _choiceFrame.Visible = true;
        var pending = new TaskCompletionSource<int>(TaskCreationOptions.RunContinuationsAsynchronously); _choice = pending;
        for (var index = 0; index < choices.Count; index++)
        {
            var captured = index;
            var button = UiStyles.Button(choices[index]); button.AutowrapMode = TextServer.AutowrapMode.WordSmart;
            button.Alignment = HorizontalAlignment.Left;
            var rowStyle = UiStyles.Panel(new Color("171c1e", .65f)); rowStyle.ContentMarginTop = 3; rowStyle.ContentMarginBottom = 3;
            button.AddThemeStyleboxOverride("normal", rowStyle);
            button.Pressed += () => { if (!_tools.Visible && !_historyPanel.Visible) _choice?.TrySetResult(captured); };
            _options.AddChild(button);
        }
        LayoutDialogue(); RestoreDialogueFocus();
        var selected = await pending.Task; _choice = null;
        _history.Add("选择：" + choices[selected]); ClearOptions(); return selected;
    }

    private void RestoreDialogueFocus()
    {
        if (!IsInsideTree() || !Visible) return;
        if (_choiceFrame.Visible && _options.GetChildCount() > 0) ((Control)_options.GetChild(0)).GrabFocus();
        else _continue.GrabFocus();
    }
    public void CloseDialogue()
    {
        CancelPending(); if (_historyPanel.Visible) _historyPanel.ClosePanel();
        UiStyles.ReleaseFocus(this); Visible = false; ClearOptions();
    }
    public override void _ExitTree() { _fade?.Kill(); CancelPending(); }
    private void CancelPending()
    {
        _autoRemaining = -1; _advance?.TrySetCanceled(); _advance = null; _choice?.TrySetCanceled(); _choice = null;
        _returnFocus = null;
        if (_tools is not null) _tools.Visible = false;
        if (_continue is not null) _continue.Disabled = false;
    }
    private void ClearOptions()
    {
        if (_options is null) return;
        UiStyles.Clear(_options); _choiceFrame.Visible = false;
    }
}
