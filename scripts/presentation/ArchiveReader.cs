using Godot;

namespace TownRpg.Presentation;

public sealed record ArchiveEntry(string Id, string Group, string Title, string Text, string? ImagePath = null);

/// <summary>Index and document share one surface. Compact reading never shrinks the font.</summary>
public partial class ArchiveReader : VBoxContainer
{
    public bool UsePaper { get; set; } = true;
    public IReadOnlyList<ArchiveEntry> Entries { get; set; } = [];
    public string SelectedId { get; private set; } = "";
    public Control? SelectionControl => _compact && !_showIndex ? _indexButton : _buttons.GetValueOrDefault(SelectedId);
    public event Action<string>? SelectionChanged;
    private readonly Dictionary<string, Button> _buttons = new(StringComparer.Ordinal);
    private HBoxContainer _switch = null!;
    private ScrollContainer _indexScroll = null!;
    private PaperDocument _document = null!;
    private Button _indexButton = null!;
    private Button _documentButton = null!;
    private bool _showIndex;
    private bool _compact;

    public override void _Ready()
    {
        SizeFlagsVertical = SizeFlags.ExpandFill;
        AddThemeConstantOverride("separation", 8);
        _switch = new HBoxContainer(); AddChild(_switch);
        _indexButton = UiStyles.Button(UiText.Get("UI_INDEX")); _indexButton.ToggleMode = true;
        _documentButton = UiStyles.Button(UiText.Get("UI_DOCUMENT")); _documentButton.ToggleMode = true;
        _switch.AddChild(_indexButton); _switch.AddChild(_documentButton);
        _indexButton.Pressed += () => { _showIndex = true; Layout(); SelectionControl?.GrabFocus(); };
        _documentButton.Pressed += () => { _showIndex = false; Layout(); _document.Scroll.GrabFocus(); };
        var row = new HBoxContainer { SizeFlagsVertical = SizeFlags.ExpandFill }; row.AddThemeConstantOverride("separation", 16); AddChild(row);
        _indexScroll = new ScrollContainer { CustomMinimumSize = new Vector2(UiStyles.IsCinematic(this) ? 130 : 168, 0), FollowFocus = true,
            HorizontalScrollMode = ScrollContainer.ScrollMode.Disabled }; row.AddChild(_indexScroll);
        var index = new VBoxContainer { SizeFlagsHorizontal = SizeFlags.ExpandFill }; index.AddThemeConstantOverride("separation", 4); _indexScroll.AddChild(index);
        _document = new PaperDocument { UsePaper = UsePaper, SizeFlagsHorizontal = SizeFlags.ExpandFill }; row.AddChild(_document);
        foreach (var group in Entries.GroupBy(e => e.Group))
        {
            var heading = UiStyles.Wrapped(group.Key); UiStyles.Font(heading, 11); heading.AddThemeColorOverride("font_color", UiStyles.Accent); index.AddChild(heading);
            foreach (var entry in group)
            {
                var button = UiStyles.Button(entry.Title); button.ToggleMode = true; button.Alignment = HorizontalAlignment.Left;
                button.AutowrapMode = TextServer.AutowrapMode.WordSmart; button.SizeFlagsHorizontal = SizeFlags.ExpandFill;
                UiStyles.Font(button, 13); button.Pressed += () => Select(entry.Id, true);
                index.AddChild(button); _buttons.Add(entry.Id, button);
            }
        }
        Select(Entries.FirstOrDefault()?.Id ?? "", false); Layout();
    }
    public override void _Process(double delta) => Layout();
    private void Layout()
    {
        _compact = UiStyles.Scale(this) >= 1.4f || Size.X < 390;
        _switch.Visible = _compact;
        _indexScroll.Visible = !_compact || _showIndex;
        _indexScroll.SizeFlagsHorizontal = _compact ? SizeFlags.ExpandFill : SizeFlags.Fill;
        _document.Visible = !_compact || !_showIndex;
        _indexButton.ButtonPressed = _showIndex; _documentButton.ButtonPressed = !_showIndex;
    }
    public void Select(string id, bool read = true)
    {
        var entry = Entries.FirstOrDefault(e => e.Id == id);
        var changed = SelectedId != id || SelectedId.Length == 0;
        SelectedId = id;
        foreach (var (key, button) in _buttons) button.ButtonPressed = key == id;
        if (changed && _buttons.TryGetValue(id, out var selected)) UiStyles.SelectionFeedback(selected);
        if (changed) _document.ShowDocument(entry?.Title ?? UiText.Get("UI_RECORDS"), entry?.Text ?? UiText.Get("UI_NO_RECORDS"), entry?.Group, entry?.ImagePath);
        if (read && _compact) { _showIndex = false; Layout(); _document.Scroll.GrabFocus(); }
        SelectionChanged?.Invoke(id);
    }
    public void FocusSelection()
    {
        if (_compact) (_showIndex ? SelectionControl : _indexButton)?.GrabFocus(); else SelectionControl?.GrabFocus();
    }
}
