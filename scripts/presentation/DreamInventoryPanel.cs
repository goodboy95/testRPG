using Godot;
using TownRpg.Application;
using TownRpg.Core.Domain;

namespace TownRpg.Presentation;

public enum DreamInventoryPage { Items, Clues, Xiaoyu }

public partial class DreamInventoryPanel : Control
{
    public event Action? PinUseRequested;
    public event Action<string>? HintRequested;
    private GameSession _session = null!;
    private ModalNavigation _navigation = null!;
    private ManagementMenuShell _shell = null!;
    private DreamEscapeState? _state;
    private string _selectedItem = "";
    private string _selectedHint = "";
    private readonly Dictionary<string, int> _scrollPositions = new(StringComparer.Ordinal);
    public string SelectedItemId => _selectedItem;
    public override void _Ready()
    {
        _session = GetNode<GameSession>("/root/GameSession");
        UiStyles.UseCinematic(this, _session.State.Settings.FontScale);
        SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect);
        _shell = new ManagementMenuShell(); AddChild(_shell);
        _shell.CloseRequested += ClosePanel;
        _navigation = new ModalNavigation(this, _session.Modal, "dream-inventory");
        Visible = false;
    }
    public void Open(DreamInventoryPage page = DreamInventoryPage.Items, Action? returnTo = null)
    {
        if (!ReferenceEquals(_state, _session.State.DreamEscape))
        {
            _state = _session.State.DreamEscape; _selectedItem = ""; _selectedHint = ""; _scrollPositions.Clear();
            Refresh();
        }
        _shell.Select(page switch { DreamInventoryPage.Clues => "records", DreamInventoryPage.Xiaoyu => "xiaoyu", _ => "inventory" }, false);
        _navigation.Open(returnTo); _shell.FocusContent();
    }
    public void ClosePanel() => _navigation.Close();
    public void SetGamepad(bool gamepad) => _shell.SetGamepad(gamepad);
    public override void _ExitTree() => _navigation.Dispose();
    public void Refresh()
    {
        if (_session.State.DreamEscape is not { } dream) return;
        _shell.SetPages([
            new("inventory", DreamPrologueText.Format("DREAM_V6_UI_INVENTORYPANEL_001"), "▦", () => true, () => BuildItems(dream), c => ((ItemGridView)c).SelectionControl),
            new("records", DreamPrologueText.Format("DREAM_V6_UI_INVENTORYPANEL_002"), "▤", () => true, () => BuildClues(dream), c => ((ArchiveReader)c).SelectionControl),
            new("xiaoyu", DreamPrologueText.Format("DREAM_V6_UI_INVENTORYPANEL_003"), "◇", () => dream.XiaoyuJoined, () => BuildHints(dream))
        ]);
        if (Visible) _shell.FocusContent();
    }
    private Control BuildItems(DreamEscapeState dream)
    {
        var grid = new ItemGridView { SelectedId = _selectedItem,
            Entries = _session.Content.Items.Items.Where(i => dream.Items.Contains(i.Id)).Select(i =>
                new ItemGridEntry(i.Id, ItemName(i.Id), ItemDescription(i.Id), ResourceLoader.Load<Texture2D>(i.IconPath),
                    i.Id == DreamEscapeIds.ItemPin ? DreamPrologueText.Format("DREAM_V6_UI_INVENTORYPANEL_004") : null,
                    i.Id == DreamEscapeIds.ItemPin ? () => PinUseRequested?.Invoke() : null)).ToArray() };
        grid.SelectionChanged += id => _selectedItem = id;
        return grid;
    }
    private ScrollContainer PageScroll(string id, out VBoxContainer column)
    {
        var scroll = new ScrollContainer { HorizontalScrollMode = ScrollContainer.ScrollMode.Disabled, FollowFocus = true };
        column = new VBoxContainer { SizeFlagsHorizontal = SizeFlags.ExpandFill };
        column.AddThemeConstantOverride("separation", 8); scroll.AddChild(column);
        scroll.Ready += () => Callable.From(() => scroll.ScrollVertical = _scrollPositions.GetValueOrDefault(id)).CallDeferred();
        scroll.GetVScrollBar().ValueChanged += value => _scrollPositions[id] = (int)value;
        return scroll;
    }
    private Control BuildClues(DreamEscapeState dream)
    {
        return new ArchiveReader { UsePaper = false, Entries = dream.DiscoveredClues.OrderBy(ClueArea).ThenBy(id => id, StringComparer.Ordinal)
            .Select(id => new ArchiveEntry(id, ClueArea(id), ClueTitle(id), ClueText(id), ClueImage(id))).ToArray() };
    }
    private Control BuildHints(DreamEscapeState dream)
    {
        var scroll = PageScroll("xiaoyu", out var column);
        var observationsTitle = UiStyles.Wrapped(UiText.Get("UI_COOP_OBSERVATIONS")); UiStyles.Heading(observationsTitle, 18); column.AddChild(observationsTitle);
        var observations = dream.DiscoveredClues.Where(id => id == DreamEscapeIds.ClueDoorMapping || id == DreamEscapeIds.ClueMusicSideWave || id == DreamEscapeIds.ClueStageSideFrame).ToArray();
        if (observations.Length == 0) column.AddChild(UiStyles.Wrapped(UiText.Get("UI_COOP_EMPTY")));
        foreach (var id in observations) column.AddChild(UiStyles.Wrapped(ClueText(id)));
        column.AddChild(new HSeparator());
        var hintsTitle = UiStyles.Wrapped(UiText.Get("UI_COOP_HINTS")); UiStyles.Heading(hintsTitle, 18); column.AddChild(hintsTitle);
        var puzzles = _session.DreamEscape.AvailableHintPuzzles().Concat(dream.HintLevels.Keys).Distinct().Order(StringComparer.Ordinal).ToArray();
        if (puzzles.Length == 0) column.AddChild(UiStyles.Wrapped(DreamPrologueText.Format("DREAM_V6_UI_INVENTORYPANEL_007")));
        foreach (var puzzle in puzzles)
        {
            var level = dream.HintLevels.GetValueOrDefault(puzzle);
            var heading = UiStyles.Wrapped(PuzzleName(puzzle) + (dream.SolvedPuzzles.Contains(puzzle) ? DreamPrologueText.Format("DREAM_V6_UI_INVENTORYPANEL_008") : ""));
            UiStyles.Font(heading, 15); heading.AddThemeColorOverride("font_color", UiStyles.Accent); column.AddChild(heading);
            for (var i = 1; i <= level; i++) column.AddChild(UiStyles.Wrapped(DreamEscapeController.HintText(
                "DREAM_HINT_" + puzzle.Replace("dream.", "", StringComparison.Ordinal).Replace('.', '_').ToUpperInvariant() + "_" + i)));
            var next = UiStyles.Button(level >= 3 ? DreamPrologueText.Format("DREAM_V6_UI_INVENTORYPANEL_009") : DreamPrologueText.Format("DREAM_V6_UI_INVENTORYPANEL_010" ,level));
            next.Name = "Hint_" + puzzle.Replace('.', '_');
            next.Disabled = level >= 3 || dream.SolvedPuzzles.Contains(puzzle);
            next.Pressed += () => { _selectedHint = puzzle; HintRequested?.Invoke(puzzle); };
            column.AddChild(next);
        }
        return scroll;
    }
    private static string ClueArea(string id) => id switch
    {
        DreamEscapeIds.ClueStageFrontFrame or DreamEscapeIds.ClueStageSideFrame or DreamEscapeIds.ClueTapeSeams or DreamEscapeIds.ClueHandoffCircuit => DreamPrologueText.Format("DREAM_V6_UI_INVENTORYPANEL_011"),
        DreamEscapeIds.ClueWorkTickets or DreamEscapeIds.ClueWorkTimeline or DreamEscapeIds.ClueMapTool => DreamPrologueText.Format("DREAM_V6_UI_INVENTORYPANEL_012"),
        DreamEscapeIds.ClueDoorMapping or DreamEscapeIds.ClueCageSequence or DreamEscapeIds.ClueBackstagePosition => DreamPrologueText.Format("DREAM_V6_UI_INVENTORYPANEL_013"),
        _ when id.Contains("music_", StringComparison.Ordinal) => DreamPrologueText.Format("DREAM_V6_UI_INVENTORYPANEL_014"),
        _ when id.Contains("archive_", StringComparison.Ordinal) => DreamPrologueText.Format("DREAM_V6_UI_INVENTORYPANEL_015"),
        _ => DreamPrologueText.Format("DREAM_V6_UI_INVENTORYPANEL_016")
    };
    public static string? ClueImage(string id) => null;
    public static string ClueTitle(string id)
    {
        var text = ClueText(id);
        var separator = text.IndexOfAny(['：', ':', '\n']);
        if (separator is > 0 and <= 22) return text[..separator].Trim('【', '】', '「', '」');
        return text.Length > 18 ? text[..18] + "…" : text;
    }

    public static string ItemName(string itemId) => DreamPrologueText.Get("ITEM_" + itemId.Replace('.', '_').ToUpperInvariant() + "_NAME");
    private static string ItemDescription(string itemId) => DreamPrologueText.Get("ITEM_" + itemId.Replace('.', '_').ToUpperInvariant() + "_DESCRIPTION");

    public static string ClueText(string clueId) => DreamPrologueText.Clue(clueId);
    private static string PuzzleName(string id) => DreamPrologueText.Puzzle(id);
}
