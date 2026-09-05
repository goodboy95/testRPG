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
    public event Action? PinUseRequested;
    public event Action<string>? HintRequested;

    private GameSession _session = null!;
    private IDisposable? _modalLease;
    private Button _itemsTab = null!;
    private Button _cluesTab = null!;
    private Button _xiaoyuTab = null!;
    private VBoxContainer _content = null!;
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

    public override void _ExitTree()
    {
        _modalLease?.Dispose();
        _modalLease = null;
    }

    public override void _UnhandledInput(InputEvent inputEvent)
    {
        if (!Visible) return;
        if (inputEvent.IsActionPressed("ui_cancel"))
        {
            GetViewport().SetInputAsHandled();
            ClosePanel();
        }
    }

    public void Open(DreamInventoryPage page = DreamInventoryPage.Items)
    {
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
        var focusOwner = GetViewport().GuiGetFocusOwner();
        if (focusOwner is not null && IsAncestorOf(focusOwner))
        {
            focusOwner.ReleaseFocus();
        }
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
            if (itemId == DreamEscapeIds.ItemPin)
            {
                var action = UiStyles.Button("对自己使用");
                action.CustomMinimumSize = new Vector2(104, 40);
                action.Pressed += () =>
                {
                    PinUseRequested?.Invoke();
                };
                row.AddChild(action);
            }
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
        var contentButton = FindFirstButton(_content);
        if (contentButton is not null)
        {
            contentButton.GrabFocus();
            return;
        }

        (_page switch
        {
            DreamInventoryPage.Items => _itemsTab,
            DreamInventoryPage.Clues => _cluesTab,
            _ => _xiaoyuTab
        }).GrabFocus();
    }

    private static Button? FindFirstButton(Node node)
    {
        foreach (var child in node.GetChildren())
        {
            if (child.IsQueuedForDeletion()) continue;
            if (child is Button button && !button.Disabled) return button;
            var nested = FindFirstButton(child);
            if (nested is not null) return nested;
        }
        return null;
    }

    public static string ItemName(string itemId) => itemId switch
    {
        DreamEscapeIds.ItemPin => "大头针", DreamEscapeIds.ItemChalk => "粉笔",
        DreamEscapeIds.ItemClockHand => "黄铜时针", DreamEscapeIds.ItemClockKey => "时钟上弦钥匙",
        DreamEscapeIds.ItemRosterSlider => "值日表滑片",
        DreamEscapeIds.ItemRoomTwoKey => "第二室钥匙", DreamEscapeIds.ItemMaintenanceKey => "维护钥匙",
        DreamEscapeIds.ItemRedFilter => "红色透明尺", DreamEscapeIds.ItemClassPhotoNegative => "全班合影底片",
        DreamEscapeIds.ItemAttendanceOverlay => "点名孔卡", DreamEscapeIds.ItemTapeFragmentClassroom => "教室剪带",
        DreamEscapeIds.ItemCalibrationReel => "校准带盘", DreamEscapeIds.ItemRecorderCrank => "录音机摇柄",
        DreamEscapeIds.ItemStageHook => "舞台钩杆",
        DreamEscapeIds.ItemRubberMallet => "橡胶音槌", DreamEscapeIds.ItemListeningCone => "共鸣听筒",
        DreamEscapeIds.ItemMetronomeWeight => "节拍器摆锤", DreamEscapeIds.ItemTornPianoRoll => "破损钢琴纸带",
        DreamEscapeIds.ItemWaxPatch => "蜡纸修补条", DreamEscapeIds.ItemRoomThreeKey => "第三室钥匙",
        DreamEscapeIds.ItemBenchClampKey => "琴凳夹钥", DreamEscapeIds.ItemTapeFragmentMusic => "音乐室剪带",
        DreamEscapeIds.ItemArchiveIndexStrip => "档案索引条", DreamEscapeIds.ItemFuseTube => "陶瓷保险管",
        DreamEscapeIds.ItemCopperWire => "漆包铜线", DreamEscapeIds.ItemRepairedFuse => "修复保险丝",
        DreamEscapeIds.ItemSplicingTape => "接带胶", DreamEscapeIds.ItemRoomFourKey => "第四室钥匙",
        DreamEscapeIds.ItemTapeFragmentArchive => "档案室剪带",
        DreamEscapeIds.ItemInspectionMirror => "折角检视镜",
        DreamEscapeIds.ItemCurtainCounterweight => "幕布配重",
        DreamEscapeIds.ItemProjectorLens => "放映镜头", DreamEscapeIds.ItemMasterReelCore => "空母带盘芯",
        DreamEscapeIds.ItemRestoredMasterTape => "复原母带",
        _ => itemId
    };

    private static string ItemDescription(string itemId) => itemId switch
    {
        DreamEscapeIds.ItemPin => "一枚尖锐的大头针。只能对自己使用。",
        DreamEscapeIds.ItemChalk => "还能写出字的白粉笔。",
        DreamEscapeIds.ItemClockHand => "从时钟下方找到的黄铜时针。",
        DreamEscapeIds.ItemClockKey => "短柄黄铜钥匙，尺寸与旧时钟侧孔吻合。",
        DreamEscapeIds.ItemRosterSlider => "透明滑片，边缘与值日表破损处相合。",
        DreamEscapeIds.ItemRoomTwoKey => "黄铜钥匙，铭牌刻着“二”。",
        DreamEscapeIds.ItemMaintenanceKey => "带三角形匙头的工具钥匙。",
        DreamEscapeIds.ItemRedFilter => "透明的红色塑料尺，能滤掉部分颜色。",
        DreamEscapeIds.ItemClassPhotoNegative => "无法直接辨清座位的旧合影底片。",
        DreamEscapeIds.ItemAttendanceOverlay => "带有规则孔位的透明点名卡。",
        DreamEscapeIds.ItemTapeFragmentClassroom => "标着“教室”的剪带，单独无法播放。",
        DreamEscapeIds.ItemCalibrationReel => "校准机械停点的短带盘，不是母带碎片。",
        DreamEscapeIds.ItemRecorderCrank => "可以装在手摇录音机侧面的摇柄。",
        DreamEscapeIds.ItemStageHook => "细长钩杆，适合拉动高处带环的机关。",
        DreamEscapeIds.ItemRubberMallet => "不会损伤音叉表面的软头音槌。",
        DreamEscapeIds.ItemListeningCone => "能放大音叉衰减并显示对应波形的金属听筒。",
        DreamEscapeIds.ItemMetronomeWeight => "刻着配重刻度，尺寸与钢琴上的节拍器吻合。",
        DreamEscapeIds.ItemTornPianoRoll => "分成四段的旧纸带，裂口边缘仍能彼此吻合。",
        DreamEscapeIds.ItemWaxPatch => "只能固定正确接缝的半透明蜡纸条。",
        DreamEscapeIds.ItemRoomThreeKey => "黄铜钥匙，铭牌刻着“三”。",
        DreamEscapeIds.ItemBenchClampKey => "扁平工具钥匙，前端适合松开隐藏夹锁。",
        DreamEscapeIds.ItemTapeFragmentMusic => "标着“音乐”的剪带，单独无法播放。",
        DreamEscapeIds.ItemArchiveIndexStrip => "标着四个抽屉节点的档案索引条。",
        DreamEscapeIds.ItemFuseTube => "烧断的旧保险管，陶瓷壳仍然完整。",
        DreamEscapeIds.ItemCopperWire => "可用于修补保险管的细漆包铜线。",
        DreamEscapeIds.ItemRepairedFuse => "已经桥接完成、等待安装的保险丝。",
        DreamEscapeIds.ItemSplicingTape => "广播室使用的窄幅接带胶。",
        DreamEscapeIds.ItemRoomFourKey => "黄铜钥匙，铭牌刻着“四”。",
        DreamEscapeIds.ItemTapeFragmentArchive => "标着“档案”的剪带，单独无法播放。",
        DreamEscapeIds.ItemInspectionMirror => "能看见并拨动高柜背锁的折角长柄镜。",
        DreamEscapeIds.ItemCurtainCounterweight => "礼堂飞索架缺失的铸铁配重。",
        DreamEscapeIds.ItemProjectorLens => "旧式放映机的镜头组件。",
        DreamEscapeIds.ItemMasterReelCore => "可以承载三段剪带的空盘芯。",
        DreamEscapeIds.ItemRestoredMasterTape => "三段剪带拼成的母带；安装后只输出白噪声。",
        _ => string.Empty
    };

    private static string ClueText(string clueId) => clueId switch
    {
        DreamEscapeIds.ClueDeskSources => "黑板：粉笔字把窗、时钟和值日表连成观察顺序。",
        DreamEscapeIds.ClueClassroomWindow => "后排左课桌维护卡：三幅窗扣磨损示意从左到右是上段、下段、中段。",
        DreamEscapeIds.ClueClassroomClock => "讲台晨间作息表：时钟图标旁写着 7:00。",
        DreamEscapeIds.ClueClassroomDutyRoster => "后排右课桌便签：值日表第 2 项被红笔圈出。",
        DreamEscapeIds.ClueCageSequence => "乐谱：四条谱线上的编号对应器材间音板。",
        DreamEscapeIds.ClueMusicTempoNotice => "入口公告板：排练通知把 72 BPM 圈了出来。",
        DreamEscapeIds.ClueMusicForkReference => "器材间对照表：四支音叉的衰减、波形与 A3—C4—E4—A4 对应。",
        DreamEscapeIds.ClueMusicRollTemplate => "纸带柜裂口模板：四段编号按 2—4—1—3 拼合。",
        DreamEscapeIds.ClueMusicBeatGlyphs => "节拍器：72 BPM 下依次显出 ○—◇—□—△；第四拍的低频由晓雨确认，界面同时显示方形波。",
        DreamEscapeIds.ClueMusicGlyphPitchMap => "音叉架：○=A3、◇=C4、□=E4、△=A4；每项同时记录音高文字和波形。",
        DreamEscapeIds.ClueMusicPitchInstrumentMap => "钢琴纸带：A3=木鱼、C4=钢琴、E4=三角铁、A4=手铃。",
        DreamEscapeIds.ClueMusicAcousticSequence => "声学墙：○—◇—□—△ 逐层换算为木鱼—钢琴—三角铁—手铃。",
        DreamEscapeIds.ClueArchiveRegistryRule => "登记规则：先上午后下午；同一时段先北侧后南侧。",
        DreamEscapeIds.ClueArchiveRegistryChoir => "合唱档案：上午、北侧。",
        DreamEscapeIds.ClueArchiveRegistryBroadcast => "广播档案：上午、南侧。",
        DreamEscapeIds.ClueArchiveRegistryArt => "美术档案：下午、北侧。",
        DreamEscapeIds.ClueArchiveRegistryTrack => "田径档案：下午、南侧。",
        DreamEscapeIds.ClueArchiveDrawerRoute => "档案索引条：抽屉线路依次连接 C1—A4—D2—B3。",
        DreamEscapeIds.ClueArchiveFuseRoute => "奖励柜线路图：铜线应依次经过左—中—右。",
        DreamEscapeIds.ClueArchiveEditingStops => "录音机机械停点：12—24—31—45。",
        DreamEscapeIds.ClueMapTool => "教师桌便签：地图柜需要三角形维护钥匙。",
        DreamEscapeIds.ClueSeatCoordinates => "合影滤色：四名学生分别落在 B2、D1、A3、C4。",
        DreamEscapeIds.ClueBroadcastSequence => "录音波形下依次画着窗帘、时钟、校铃、点名册。",
        DreamEscapeIds.ClueAuditoriumLineSetNotches => "幕布配重磨损尺：A3—B1—C4—D2。",
        DreamEscapeIds.ClueAuditoriumProjectionOrder => "升幕后投影轮廓：窗格—人物—校铃。",
        DreamEscapeIds.ClueAuditoriumTapeOrder => "母带登记卡：剪带顺序为教室—音乐—档案。",
        _ => clueId
    };

    private static string PuzzleName(string id) => id switch
    {
        DreamEscapeIds.PuzzleWindowLatches => "窗扣暗格", DreamEscapeIds.PuzzleBlackboard => "黑板擦痕",
        DreamEscapeIds.PuzzleClockRestore => "修复时钟", DreamEscapeIds.PuzzleRosterSlider => "值日表滑片",
        DreamEscapeIds.PuzzleTeacherDesk => "教师桌", DreamEscapeIds.PuzzleMusicScore => "蒙尘乐谱",
        DreamEscapeIds.PuzzleMusicCage => "器材间音板", DreamEscapeIds.PuzzleMusicMetronome => "节拍器校准",
        DreamEscapeIds.PuzzleMusicTuningForks => "音叉排序", DreamEscapeIds.PuzzleMusicPianoRoll => "钢琴纸带",
        DreamEscapeIds.PuzzleMusicAcousticWall => "声学墙转译",
        DreamEscapeIds.PuzzleArchiveSignalRegistry => "广播档案登记", DreamEscapeIds.PuzzleMapCabinet => "地图柜",
        DreamEscapeIds.PuzzleTransomCache => "气窗盒",
        DreamEscapeIds.PuzzleMusicAttendanceReader => "点名读取装置",
        DreamEscapeIds.PuzzleMusicPianoBenchCache => "琴凳暗格",
        DreamEscapeIds.PuzzleArchiveDrawerRoute => "抽屉线路",
        DreamEscapeIds.PuzzleArchiveFuseRepair => "保险丝修复",
        DreamEscapeIds.PuzzleArchiveRecorderRebuild => "录音机重建",
        DreamEscapeIds.PuzzleArchiveEditingCounter => "剪辑计数器",
        DreamEscapeIds.PuzzleArchiveUpperVault => "高柜暗格",
        DreamEscapeIds.PuzzleAuditoriumLineSetBalance => "幕布吊杆配平",
        DreamEscapeIds.PuzzleAuditoriumShadowProjector => "影盘放映机",
        DreamEscapeIds.PuzzleAuditoriumMasterTapeSplice => "母带拼接",
        DreamEscapeIds.PuzzleAuditoriumLightConsole => "灯光控制台",
        DreamEscapeIds.PuzzleAuditoriumSoundConsole => "声道控制台",
        DreamEscapeIds.PuzzleAuditoriumBroadcastConsole => "广播控制台",
        DreamEscapeIds.PuzzleExit => "双侧出口机关",
        _ => id
    };
}
