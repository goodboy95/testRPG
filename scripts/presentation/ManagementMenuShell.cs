using Godot;

namespace TownRpg.Presentation;

public sealed record ManagementPage(string Id, string Title, string Icon, Func<bool> Available,
    Func<Control> CreateContent, Func<Control, Control?>? RestoreFocus = null);

/// Navigation and content lifetime are shared; the caller owns domain data and its modal lease.
public partial class ManagementMenuShell : Control
{
    public event Action? CloseRequested;
    private readonly Dictionary<string, Control> _contents = new(StringComparer.Ordinal);
    private readonly Dictionary<string, Button> _tabs = new(StringComparer.Ordinal);
    private List<ManagementPage> _pages = [];
    private HBoxContainer _navigation = null!;
    private MarginContainer _host = null!;
    private Label _heading = null!;
    private Label _help = null!;
    private ScrollContainer _tabScroll = null!;
    private int _focusRequest;
    public string SelectedPageId { get; private set; } = "";
    public IReadOnlyList<string> PageIds => _pages.Select(p => p.Id).ToArray();
    public override void _Ready()
    {
        SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect);
        var cinematic = UiStyles.IsCinematic(this);
        var shade = new ColorRect { Color = new Color("101514", cinematic ? .3f : .85f) };
        shade.SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect); AddChild(shade);
        var frame = new PanelContainer { Name = "ManagementFrame",
            Position = cinematic ? new Vector2(64, 30) : new Vector2(18, 14),
            Size = cinematic ? new Vector2(512, 300) : new Vector2(604, 332) };
        if (cinematic) frame.AddThemeStyleboxOverride("panel", UiStyles.Panel(new Color("171c1e", .92f)));
        AddChild(frame);
        var column = new VBoxContainer(); column.AddThemeConstantOverride("separation", 6); frame.AddChild(column);
        _heading = new Label { Text = UiText.Get("UI_MANAGEMENT") }; UiStyles.Heading(_heading, 20); column.AddChild(_heading);
        var tabScroll = new ScrollContainer { HorizontalScrollMode = ScrollContainer.ScrollMode.Auto,
            VerticalScrollMode = ScrollContainer.ScrollMode.Disabled, FollowFocus = true };
        column.AddChild(tabScroll);
        _tabScroll = tabScroll;
        _navigation = new HBoxContainer(); _navigation.AddThemeConstantOverride("separation", 6); tabScroll.AddChild(_navigation);
        _host = new MarginContainer { SizeFlagsVertical = SizeFlags.ExpandFill }; column.AddChild(_host);
        var footer = new HBoxContainer(); column.AddChild(footer);
        _help = new Label { SizeFlagsHorizontal = SizeFlags.ExpandFill, AutowrapMode = TextServer.AutowrapMode.WordSmart }; UiStyles.Font(_help, 11); _help.AddThemeColorOverride("font_color", UiStyles.Muted); footer.AddChild(_help);
        var close = UiStyles.Button(UiText.Get("UI_BACK")); close.Name = "ManagementBack";
        close.Pressed += () => CloseRequested?.Invoke(); footer.AddChild(close);
        SetGamepad(false);
    }
    public void SetTitle(string title) => _heading.Text = title;
    public void SetGamepad(bool gamepad) => _help.Text = UiText.Get(gamepad ? "UI_NAV_GAMEPAD" : "UI_NAV_KEYBOARD");
    public void SetPages(IEnumerable<ManagementPage> pages, string? selected = null)
    {
        UiStyles.Clear(_navigation); UiStyles.Clear(_host); _contents.Clear(); _tabs.Clear();
        _pages = pages.Where(p => p.Available()).ToList();
        _tabScroll.Visible = _pages.Count > 1;
        foreach (var page in _pages)
        {
            var tab = UiStyles.Button(page.Title); tab.ToggleMode = true;
            tab.Pressed += () => Select(page.Id); _navigation.AddChild(tab); _tabs[page.Id] = tab;
        }
        Select(selected ?? SelectedPageId, false);
    }
    public void Select(string id, bool focus = true)
    {
        var page = _pages.FirstOrDefault(p => p.Id == id) ?? _pages.FirstOrDefault();
        if (page is null) return;
        foreach (var content in _contents.Values) content.Visible = false;
        SelectedPageId = page.Id;
        if (!_contents.TryGetValue(page.Id, out var body))
        {
            body = page.CreateContent(); body.SizeFlagsVertical = SizeFlags.ExpandFill;
            body.SizeFlagsHorizontal = SizeFlags.ExpandFill; _contents.Add(page.Id, body); _host.AddChild(body);
        }
        body.Visible = true;
        foreach (var (key, tab) in _tabs) tab.ButtonPressed = key == page.Id;
        if (focus) FocusContent();
    }
    public void FocusContent()
    {
        if (!_contents.TryGetValue(SelectedPageId, out var body)) return;
        var page = _pages.First(p => p.Id == SelectedPageId);
        var target = page.RestoreFocus?.Invoke(body) ?? _tabs[SelectedPageId];
        if (target.IsVisibleInTree()) target.GrabFocus();
        RestoreAfterLayout(target, ++_focusRequest);
    }
    private async void RestoreAfterLayout(Control target, int request)
    {
        var tree = GetTree();
        await ToSignal(tree, SceneTree.SignalName.ProcessFrame);
        await ToSignal(tree, SceneTree.SignalName.ProcessFrame);
        if (request != _focusRequest || !IsInsideTree() || !IsVisibleInTree() ||
            !GodotObject.IsInstanceValid(target) || !target.IsInsideTree() || !target.IsVisibleInTree() ||
            GetViewport().GuiGetFocusOwner() != target) return;
        for (var parent = target.GetParent(); parent is not null && parent != this; parent = parent.GetParent())
            if (parent is ScrollContainer scroll) scroll.EnsureControlVisible(target);
    }
    public override void _Input(InputEvent e)
    {
        if (!IsVisibleInTree() || GetViewport().GuiGetFocusOwner() is { } focus && !IsAncestorOf(focus)) return;
        var direction = UiStyles.PageDirection(e, this);
        if (direction != 0 && _pages.Count > 0)
        {
            var index = _pages.FindIndex(p => p.Id == SelectedPageId);
            Select(_pages[(index + direction + _pages.Count) % _pages.Count].Id);
            GetViewport().SetInputAsHandled();
        }
        else if (e.IsActionPressed("ui_cancel"))
        {
            GetViewport().SetInputAsHandled(); CloseRequested?.Invoke();
        }
    }
}
