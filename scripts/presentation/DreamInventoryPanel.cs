using Godot;
using TownRpg.Application;
using TownRpg.Core.Domain;

namespace TownRpg.Presentation;

public enum DreamInventoryPage
{
    Items,
    Clues,
    Xiaoyu
}

public partial class DreamInventoryPanel : Control
{
    public event Action<string>? ItemSelected;
    public event Action? PinUseRequested;
    public event Action<string>? HintRequested;

    private GameSession _session = null!;
    private IDisposable? _modalLease;
    private Button _itemsTab = null!;
    private Button _cluesTab = null!;
    private Button _xiaoyuTab = null!;
    private VBoxContainer _content = null!;
    private Label _selected = null!;
    private string _selectedItemId = string.Empty;
    private DreamInventoryPage _page = DreamInventoryPage.Items;

    public override void _Ready()
    {
        _session = GetNode<GameSession>("/root/GameSession");
        SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect);
        Visible = false;

        var shade = new ColorRect { Color = new Color("050b13df") };
        shade.SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect);
        AddChild(shade);
        var frame = new PanelContainer { Position = new Vector2(44, 22), Size = new Vector2(552, 316) };
        frame.AddThemeStyleboxOverride("panel", UiStyles.Panel(new Color("172837fb"), 9));
        AddChild(frame);
        var root = new VBoxContainer();
        root.AddThemeConstantOverride("separation", 6);
        frame.AddChild(root);

        var titleRow = new HBoxContainer();
        root.AddChild(titleRow);
        var title = new Label { Text = "随身物品与记录", SizeFlagsHorizontal = SizeFlags.ExpandFill };
        title.AddThemeFontSizeOverride("font_size", 20);
        title.AddThemeColorOverride("font_color", new Color("f2c66d"));
        titleRow.AddChild(title);
        var close = UiStyles.Button("关闭");
        close.Pressed += ClosePanel;
        titleRow.AddChild(close);

        var tabs = new HBoxContainer();
        tabs.AddThemeConstantOverride("separation", 6);
        root.AddChild(tabs);
        _itemsTab = AddTab(tabs, "物品", DreamInventoryPage.Items);
        _cluesTab = AddTab(tabs, "记录", DreamInventoryPage.Clues);
        _xiaoyuTab = AddTab(tabs, "与晓雨商量", DreamInventoryPage.Xiaoyu);

        _selected = new Label();
        _selected.AddThemeColorOverride("font_color", new Color("9bd3c9"));
        root.AddChild(_selected);

        var pagePanel = new PanelContainer { SizeFlagsVertical = SizeFlags.ExpandFill };
        pagePanel.AddThemeStyleboxOverride("panel", UiStyles.Panel(new Color("0f1d29dd"), 6));
        root.AddChild(pagePanel);
        var scroll = new ScrollContainer
        {
            SizeFlagsHorizontal = SizeFlags.ExpandFill,
            SizeFlagsVertical = SizeFlags.ExpandFill
        };
        pagePanel.AddChild(scroll);
        _content = new VBoxContainer { SizeFlagsHorizontal = SizeFlags.ExpandFill };
        _content.AddThemeConstantOverride("separation", 6);
        scroll.AddChild(_content);
    }

    public override void _UnhandledInput(InputEvent inputEvent)
    {
        if (!Visible) return;
        if (inputEvent.IsActionPressed("inventory") || inputEvent.IsActionPressed("ui_cancel"))
        {
            GetViewport().SetInputAsHandled();
            ClosePanel();
        }
    }

    public void Open(string selectedItemId, DreamInventoryPage page = DreamInventoryPage.Items)
    {
        _selectedItemId = selectedItemId;
        _page = page == DreamInventoryPage.Xiaoyu && _session.State.DreamEscape?.XiaoyuJoined != true
            ? DreamInventoryPage.Items
            : page;
        Refresh();
        _modalLease?.Dispose();
        _modalLease = _session.Modal.Acquire("dream-inventory");
        Visible = true;
        Callable.From(RestoreFocus).CallDeferred();
    }

    public void ClosePanel()
    {
        Visible = false;
        _modalLease?.Dispose();
        _modalLease = null;
    }

    public void Refresh()
    {
        if (_session.State.DreamEscape is not { } dream) return;
        if (!dream.XiaoyuJoined && _page == DreamInventoryPage.Xiaoyu) _page = DreamInventoryPage.Items;

        foreach (var child in _content.GetChildren()) child.QueueFree();
        _xiaoyuTab.Visible = dream.XiaoyuJoined;
        _itemsTab.ButtonPressed = _page == DreamInventoryPage.Items;
        _cluesTab.ButtonPressed = _page == DreamInventoryPage.Clues;
        _xiaoyuTab.ButtonPressed = _page == DreamInventoryPage.Xiaoyu;
        _selected.Text = string.IsNullOrEmpty(_selectedItemId)
            ? "当前未选用道具"
            : $"当前选用：{ItemName(_selectedItemId)}（再次选择可取消）";

        switch (_page)
        {
            case DreamInventoryPage.Items:
                BuildItems(dream);
                break;
            case DreamInventoryPage.Clues:
                BuildClues(dream);
                break;
            case DreamInventoryPage.Xiaoyu:
                BuildHints(dream);
                break;
        }
        if (Visible) Callable.From(RestoreFocus).CallDeferred();
    }

    private Button AddTab(Node parent, string text, DreamInventoryPage page)
    {
        var button = UiStyles.Button(text);
        button.ToggleMode = true;
        button.SizeFlagsHorizontal = SizeFlags.ExpandFill;
        button.Pressed += () =>
        {
            _page = page;
            Refresh();
        };
        parent.AddChild(button);
        return button;
    }

    private void BuildItems(DreamEscapeState dream)
    {
        var definitions = _session.Content.Items.Items.ToDictionary(item => item.Id, StringComparer.Ordinal);
        foreach (var itemId in dream.Items.Order(StringComparer.Ordinal))
        {
            if (!definitions.TryGetValue(itemId, out var definition)) continue;
            var card = new PanelContainer();
            card.AddThemeStyleboxOverride("panel", UiStyles.Panel(new Color("152735e8"), 5));
            _content.AddChild(card);
            var row = new HBoxContainer();
            row.AddThemeConstantOverride("separation", 8);
            card.AddChild(row);
            row.AddChild(new TextureRect
            {
                Texture = ResourceLoader.Load<Texture2D>(definition.IconPath),
                CustomMinimumSize = new Vector2(42, 42),
                ExpandMode = TextureRect.ExpandModeEnum.IgnoreSize,
                StretchMode = TextureRect.StretchModeEnum.KeepAspectCentered,
                MouseFilter = MouseFilterEnum.Ignore
            });
            row.AddChild(new Label
            {
                Text = $"{ItemName(itemId)}\n{ItemDescription(itemId)}",
                AutowrapMode = TextServer.AutowrapMode.WordSmart,
                SizeFlagsHorizontal = SizeFlags.ExpandFill,
                VerticalAlignment = VerticalAlignment.Center
            });
            var action = UiStyles.Button(itemId == DreamEscapeIds.ItemPin
                ? "对自己使用"
                : itemId == _selectedItemId ? "取消选用" : "选用");
            action.CustomMinimumSize = new Vector2(104, 40);
            action.Pressed += () =>
            {
                if (itemId == DreamEscapeIds.ItemPin)
                {
                    PinUseRequested?.Invoke();
                    return;
                }
                var next = itemId == _selectedItemId ? string.Empty : itemId;
                _selectedItemId = next;
                ItemSelected?.Invoke(next);
                ClosePanel();
            };
            row.AddChild(action);
        }
    }

    private void BuildClues(DreamEscapeState dream)
    {
        if (dream.DiscoveredClues.Count == 0)
        {
            AddWrapped("尚未记录到线索。靠近可疑物体并进行观察。", new Color("b9c7ca"));
            return;
        }
        foreach (var clue in dream.DiscoveredClues.Order(StringComparer.Ordinal))
            AddWrapped(ClueText(clue), new Color("e2e7df"));
    }

    private void BuildHints(DreamEscapeState dream)
    {
        var available = _session.DreamEscape.AvailableHintPuzzles();
        if (available.Count == 0)
        {
            AddWrapped("晓雨暂时想不到还有哪些已发现、未解决的机关。", new Color("b9c7ca"));
            return;
        }
        foreach (var puzzle in available)
        {
            var level = dream.HintLevels.GetValueOrDefault(puzzle);
            var button = UiStyles.Button($"和晓雨商量：{PuzzleName(puzzle)}　[{level}/3]");
            button.Disabled = level >= 3;
            button.TooltipText = "提示没有惩罚，会逐级变得更明确。";
            button.Pressed += () => HintRequested?.Invoke(puzzle);
            _content.AddChild(button);
        }
    }

    private void AddWrapped(string text, Color color)
    {
        var label = new Label
        {
            Text = text,
            AutowrapMode = TextServer.AutowrapMode.WordSmart,
            CustomMinimumSize = new Vector2(480, 42)
        };
        label.AddThemeColorOverride("font_color", color);
        _content.AddChild(label);
    }

    private void RestoreFocus()
    {
        if (!Visible) return;
        FindFirstButton(_content)?.GrabFocus();
        if (GetViewport().GuiGetFocusOwner() is null)
        {
            (_page switch
            {
                DreamInventoryPage.Items => _itemsTab,
                DreamInventoryPage.Clues => _cluesTab,
                _ => _xiaoyuTab
            }).GrabFocus();
        }
    }

    private static Button? FindFirstButton(Node node)
    {
        foreach (var child in node.GetChildren())
        {
            if (child is Button button && !button.Disabled) return button;
            var nested = FindFirstButton(child);
            if (nested is not null) return nested;
        }
        return null;
    }

    public static string ItemName(string itemId) => itemId switch
    {
        DreamEscapeIds.ItemPin => "大头针", DreamEscapeIds.ItemChalk => "粉笔",
        DreamEscapeIds.ItemRoomTwoKey => "第二室钥匙", DreamEscapeIds.ItemMaintenanceKey => "维护钥匙",
        DreamEscapeIds.ItemRedFilter => "红色透明尺", DreamEscapeIds.ItemClassPhoto => "全班合影",
        DreamEscapeIds.ItemTapeReel => "录音带", DreamEscapeIds.ItemRecorderCrank => "录音机摇柄",
        _ => itemId
    };

    private static string ItemDescription(string itemId) => itemId switch
    {
        DreamEscapeIds.ItemPin => "一枚尖锐的大头针。只能对自己使用。",
        DreamEscapeIds.ItemChalk => "还能写出字的白粉笔。",
        DreamEscapeIds.ItemRoomTwoKey => "黄铜钥匙，铭牌刻着“二”。",
        DreamEscapeIds.ItemMaintenanceKey => "带三角形匙头的工具钥匙。",
        DreamEscapeIds.ItemRedFilter => "透明的红色塑料尺，能滤掉部分颜色。",
        DreamEscapeIds.ItemClassPhoto => "旧校舍某一届学生的全班合影。",
        DreamEscapeIds.ItemTapeReel => "从点名柜里找到的旧录音带。",
        DreamEscapeIds.ItemRecorderCrank => "可以装在手摇录音机侧面的摇柄。",
        _ => string.Empty
    };

    private static string ClueText(string clueId) => clueId switch
    {
        DreamEscapeIds.ClueDeskSources => "黑板：粉笔字把窗、时钟和值日表连成观察顺序。",
        DreamEscapeIds.ClueClassroomWindow => "旧窗格：三扇窗格各有一道醒目的划痕。",
        DreamEscapeIds.ClueClassroomClock => "时钟：分针停在十二，时针停在七。",
        DreamEscapeIds.ClueClassroomDutyRoster => "值日表：第二项被红笔圈了起来。",
        DreamEscapeIds.ClueCageSequence => "乐谱：四条谱线上的编号对应器材间音板。",
        DreamEscapeIds.ClueListenWindow => "窗边：短促的矩形波，像木鱼敲击；同时写有“木鱼”。",
        DreamEscapeIds.ClueListenLectern => "讲台：连续起伏的键形波，像钢琴；同时写有“钢琴”。",
        DreamEscapeIds.ClueListenDoor => "门边：先有三角尖波，随后是圆环波；标记为三角铁、手铃。",
        DreamEscapeIds.ClueArchiveRules => "归档规则：四个社团应按当天最早使用教室的时间排列。",
        DreamEscapeIds.ClueMapTool => "教师桌便签：地图柜需要三角形维护钥匙。",
        DreamEscapeIds.ClueSeatCoordinates => "合影滤色：四名学生分别落在 B2、D1、A3、C4。",
        DreamEscapeIds.ClueBroadcastSequence => "录音波形下依次画着窗帘、时钟、校铃、点名册。",
        _ => clueId
    };

    private static string PuzzleName(string id) => id switch
    {
        DreamEscapeIds.PuzzleBlackboard => "黑板擦痕", DreamEscapeIds.PuzzleTeacherDesk => "教师桌",
        DreamEscapeIds.PuzzleRoomTwoDoor => "第二扇门", DreamEscapeIds.PuzzleMusicScore => "蒙尘乐谱",
        DreamEscapeIds.PuzzleMusicCage => "器材间音板", DreamEscapeIds.PuzzleListening => "辨听排列",
        DreamEscapeIds.PuzzleArchiveOrder => "社团档案", DreamEscapeIds.PuzzleMapCabinet => "地图柜",
        DreamEscapeIds.PuzzlePhotoFilter => "合影滤色", DreamEscapeIds.PuzzleAttendance => "点名柜",
        DreamEscapeIds.PuzzleRecorderTape or DreamEscapeIds.PuzzleRecorderCrank => "旧录音机",
        DreamEscapeIds.PuzzleRoomFourDoor => "第四扇门", DreamEscapeIds.PuzzleFinalDesk => "数字控制板",
        DreamEscapeIds.PuzzleFinalInstruments => "乐器控制板", DreamEscapeIds.PuzzleFinalBroadcast => "广播控制板",
        DreamEscapeIds.PuzzleLeftLever => "双侧机关", DreamEscapeIds.PuzzleExit => "出口总闸",
        _ => id
    };
}
