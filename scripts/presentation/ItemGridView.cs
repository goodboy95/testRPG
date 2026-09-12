using Godot;

namespace TownRpg.Presentation;

public sealed record ItemGridEntry(string Id, string Name, string Description, Texture2D? Icon,
    string? ActionLabel = null, Action? Activate = null);

public partial class ItemGridView : VBoxContainer
{
    public event Action<string>? SelectionChanged;
    public IReadOnlyList<ItemGridEntry> Entries { get; set; } = [];
    public string SelectedId { get; set; } = "";
    private GridContainer _grid = null!;
    private VBoxContainer _details = null!;
    private VBoxContainer _actions = null!;
    private VBoxContainer _detailsHost = null!;
    private ScrollContainer _gridScroll = null!;
    private HBoxContainer _switch = null!;
    private Button _itemsButton = null!;
    private Button _detailButton = null!;
    private bool _showDetails;
    private readonly Dictionary<string, Button> _buttons = new(StringComparer.Ordinal);
    public Control? SelectionControl => _gridScroll?.Visible == true ? _buttons.GetValueOrDefault(SelectedId) : _itemsButton;
    public override void _Ready()
    {
        var cinematic = UiStyles.IsCinematic(this);
        AddThemeConstantOverride("separation", 8);
        _switch = new HBoxContainer(); AddChild(_switch);
        _itemsButton = UiStyles.Button(UiText.Get("UI_ITEMS")); _itemsButton.ToggleMode = true;
        _detailButton = UiStyles.Button(UiText.Get("UI_DETAILS")); _detailButton.ToggleMode = true;
        _switch.AddChild(_itemsButton); _switch.AddChild(_detailButton);
        _itemsButton.Pressed += () => { _showDetails = false; Layout(); SelectionControl?.GrabFocus(); };
        _detailButton.Pressed += () => { _showDetails = true; Layout(); _detailButton.GrabFocus(); };
        var row = new HBoxContainer { SizeFlagsVertical = SizeFlags.ExpandFill }; row.AddThemeConstantOverride("separation", 16); AddChild(row);
        _gridScroll = UiStyles.Scroll(row);
        _grid = new GridContainer { Columns = 4, SizeFlagsHorizontal = SizeFlags.ExpandFill };
        _grid.AddThemeConstantOverride("h_separation", 6); _grid.AddThemeConstantOverride("v_separation", 6); _gridScroll.AddChild(_grid);
        _detailsHost = new VBoxContainer { CustomMinimumSize = new Vector2(cinematic ? 170 : 236, 0) }; row.AddChild(_detailsHost);
        var detailScroll = UiStyles.Scroll(_detailsHost);
        _details = new VBoxContainer { SizeFlagsHorizontal = SizeFlags.ExpandFill };
        _details.AddThemeConstantOverride("separation", 10); detailScroll.AddChild(_details);
        _actions = new VBoxContainer(); _detailsHost.AddChild(_actions);
        foreach (var item in Entries)
        {
            var button = UiStyles.Button(item.Name);
            button.Name = "Item_" + item.Id.Replace('.', '_');
            button.Icon = item.Icon; button.ExpandIcon = true;
            button.IconAlignment = HorizontalAlignment.Center; button.VerticalIconAlignment = VerticalAlignment.Top;
            button.AddThemeConstantOverride("icon_max_width", cinematic ? 28 : 40);
            button.AddThemeStyleboxOverride("normal", UiStyles.Panel(cinematic ? new Color("242b2d", .55f) : UiStyles.Surface));
            button.AutowrapMode = TextServer.AutowrapMode.WordSmart;
            button.CustomMinimumSize = cinematic ? new Vector2(48, 58) : new Vector2(58, 82); UiStyles.Font(button, 11);
            button.SizeFlagsHorizontal = SizeFlags.ExpandFill; button.ToggleMode = true;
            button.Pressed += () => { Select(item.Id); if (_switch.Visible) { _showDetails = true; Layout(); _detailButton.GrabFocus(); } };
            button.FocusEntered += () => Select(item.Id);
            _grid.AddChild(button); _buttons.Add(item.Id, button);
        }
        if (Entries.Count == 0) _grid.AddChild(UiStyles.Wrapped(UiText.Get("UI_INVENTORY_EMPTY")));
        Select(_buttons.ContainsKey(SelectedId) ? SelectedId : Entries.FirstOrDefault()?.Id ?? "", true);
        Layout();
        Callable.From(() => _gridScroll.ScrollVertical = 0).CallDeferred();
    }
    public override void _Process(double delta) => Layout();
    private void Layout()
    {
        var compact = UiStyles.Scale(this) >= 1.4f;
        var empty = Entries.Count == 0;
        _grid.Columns = empty ? 1 : compact ? 5 : UiStyles.Scale(this) >= 1.2f ? 3 : 4;
        _switch.Visible = compact && !empty;
        _gridScroll.Visible = empty || !compact || !_showDetails;
        _detailsHost.Visible = !empty && (!compact || _showDetails);
        _detailsHost.SizeFlagsHorizontal = compact ? SizeFlags.ExpandFill : SizeFlags.Fill;
        _itemsButton.ButtonPressed = !_showDetails; _detailButton.ButtonPressed = _showDetails;
    }
    public void Select(string id) => Select(id, false);
    private void Select(string id, bool force)
    {
        if (!force && SelectedId == id) return;
        SelectedId = id;
        UiStyles.Clear(_details); UiStyles.Clear(_actions);
        foreach (var (key, button) in _buttons) button.ButtonPressed = key == id;
        if (!force && _buttons.TryGetValue(id, out var selected)) UiStyles.SelectionFeedback(selected);
        var item = Entries.FirstOrDefault(e => e.Id == id);
        if (item is null) { _details.AddChild(UiStyles.Wrapped(UiText.Get("UI_INVENTORY_EMPTY"))); return; }
        if (item.Icon is not null) _details.AddChild(new TextureRect { Texture = item.Icon,
            CustomMinimumSize = new Vector2(0, UiStyles.IsCinematic(this) ? 56 : 74), ExpandMode = TextureRect.ExpandModeEnum.IgnoreSize,
            StretchMode = TextureRect.StretchModeEnum.KeepAspectCentered });
        var title = UiStyles.Wrapped(item.Name); UiStyles.Heading(title, 19); _details.AddChild(title);
        _details.AddChild(new HSeparator()); _details.AddChild(UiStyles.Wrapped(item.Description));
        if (item.ActionLabel is not null && item.Activate is not null)
        {
            var action = UiStyles.Button(item.ActionLabel); action.Pressed += item.Activate; _actions.AddChild(action);
        }
        SelectionChanged?.Invoke(id);
    }
}
