using Godot;
using TownRpg.Application;
using TownRpg.Core.Domain;
using TownRpg.Core.Services;

namespace TownRpg.Presentation;

public partial class DreamEscapeController : Node2D
{
    public event Action? PinWakeRequested;
    public event Action? ExitOpened;

    private GameSession _session = null!;
    private Node2D _areaHost = null!;
    private DreamAreaController? _area;
    private CanvasLayer _interface = null!;
    private Control _interfaceRoot = null!;
    private Label _areaLabel = null!;
    private Label _objective = null!;
    private Label _prompt = null!;
    private Label _selected = null!;
    private TextureRect _selectedIcon = null!;
    private Label _toast = null!;
    private TextureRect _clueCloseup = null!;
    private TextureRect _xiaoyuPortrait = null!;
    private DreamPuzzlePanel _puzzle = null!;
    private DreamInventoryPanel _inventory = null!;
    private ConfirmationDialog _pinConfirmation = null!;
    private string _selectedItemId = string.Empty;
    private bool _usingGamepad;
    private int _toastVersion;

    public override void _Ready()
    {
        _session = GetNode<GameSession>("/root/GameSession");
        if (_session.State.DreamEscape is null)
        {
            throw new InvalidOperationException("DreamEscape scene requires a dream escape state.");
        }
        _session.State.Player.LocationId = DreamEscapeIds.Location;
        _areaHost = new Node2D { Name = "AreaHost" };
        AddChild(_areaHost);
        BuildInterface();
        LoadArea(_session.State.DreamEscape.CurrentAreaId);
        RefreshHud();
    }

    public override void _ExitTree()
    {
        DisconnectArea();
    }

    public override void _Input(InputEvent inputEvent)
    {
        var nextGamepad = inputEvent is InputEventJoypadButton button && button.Pressed ||
                          inputEvent is InputEventJoypadMotion motion && Math.Abs(motion.AxisValue) >= 0.35f;
        var nextKeyboard = inputEvent is InputEventKey key && key.Pressed ||
                           inputEvent is InputEventMouseButton mouse && mouse.Pressed;
        if ((nextGamepad && !_usingGamepad) || (nextKeyboard && _usingGamepad))
        {
            _usingGamepad = nextGamepad;
            _area?.SetGamepadPrompts(_usingGamepad);
            RefreshHud();
        }
    }

    public override void _UnhandledInput(InputEvent inputEvent)
    {
        if (inputEvent.IsActionPressed("inventory") && !_puzzle.Visible && !_pinConfirmation.Visible)
        {
            GetViewport().SetInputAsHandled();
            if (_inventory.Visible) _inventory.ClosePanel();
            else _inventory.Open(_selectedItemId);
            return;
        }
        if (inputEvent.IsActionPressed("pause_menu") && !_session.Modal.IsBlocked)
        {
            GetViewport().SetInputAsHandled();
            ShowToast("此处没有可暂停的日程；进度会在每次有效操作后保存。", 3.2);
        }
    }

    private void BuildInterface()
    {
        _interface = new CanvasLayer { Layer = 12 };
        AddChild(_interface);
        _interfaceRoot = new Control { Theme = UiStyles.CreateTheme() };
        _interfaceRoot.Theme.DefaultFontSize = Mathf.RoundToInt(14 * _session.State.Settings.FontScale);
        _interfaceRoot.SetAnchorsAndOffsetsPreset(Control.LayoutPreset.FullRect);
        _interface.AddChild(_interfaceRoot);

        var top = new PanelContainer { Position = new Vector2(10, 8), Size = new Vector2(620, 50) };
        top.AddThemeStyleboxOverride("panel", UiStyles.Panel(new Color("122431e8"), 6));
        _interfaceRoot.AddChild(top);
        var topRow = new HBoxContainer();
        top.AddChild(topRow);
        _areaLabel = new Label { CustomMinimumSize = new Vector2(126, 28) };
        _areaLabel.AddThemeColorOverride("font_color", new Color("f2c66d"));
        topRow.AddChild(_areaLabel);
        _objective = new Label
        {
            SizeFlagsHorizontal = Control.SizeFlags.ExpandFill,
            HorizontalAlignment = HorizontalAlignment.Center,
            TextOverrunBehavior = TextServer.OverrunBehavior.TrimEllipsis
        };
        topRow.AddChild(_objective);
        _selectedIcon = new TextureRect
        {
            CustomMinimumSize = new Vector2(28, 28),
            ExpandMode = TextureRect.ExpandModeEnum.IgnoreSize,
            StretchMode = TextureRect.StretchModeEnum.KeepAspectCentered,
            MouseFilter = Control.MouseFilterEnum.Ignore,
            Visible = false
        };
        topRow.AddChild(_selectedIcon);
        _selected = new Label
        {
            CustomMinimumSize = new Vector2(116, 28),
            HorizontalAlignment = HorizontalAlignment.Right,
            VerticalAlignment = VerticalAlignment.Center,
            TextOverrunBehavior = TextServer.OverrunBehavior.TrimEllipsis
        };
        _selected.AddThemeColorOverride("font_color", new Color("9bd3c9"));
        topRow.AddChild(_selected);
        _xiaoyuPortrait = new TextureRect
        {
            CustomMinimumSize = new Vector2(34, 34),
            Texture = ResourceLoader.Load<Texture2D>("res://assets/generated/dream_escape/lin-xiaoyu-neutral.png"),
            ExpandMode = TextureRect.ExpandModeEnum.IgnoreSize,
            StretchMode = TextureRect.StretchModeEnum.KeepAspectCentered,
            MouseFilter = Control.MouseFilterEnum.Ignore,
            TooltipText = "林晓雨"
        };
        topRow.AddChild(_xiaoyuPortrait);
        _prompt = new Label
        {
            Position = new Vector2(90, 318),
            Size = new Vector2(460, 30),
            HorizontalAlignment = HorizontalAlignment.Center,
            VerticalAlignment = VerticalAlignment.Center,
            Visible = false
        };
        _prompt.AddThemeStyleboxOverride("normal", UiStyles.Panel(
            _session.State.Settings.HighContrastPrompts ? new Color("000000ff") : new Color("152536ed"), 5));
        _prompt.AddThemeColorOverride("font_color", new Color("f5e6bd"));
        _interfaceRoot.AddChild(_prompt);
        _toast = new Label
        {
            Position = new Vector2(105, 66),
            Size = new Vector2(430, 54),
            HorizontalAlignment = HorizontalAlignment.Center,
            VerticalAlignment = VerticalAlignment.Center,
            AutowrapMode = TextServer.AutowrapMode.WordSmart,
            Visible = false
        };
        _toast.AddThemeStyleboxOverride("normal", UiStyles.Panel(new Color("233846fa"), 6));
        _toast.AddThemeColorOverride("font_color", new Color("f2c66d"));
        _interfaceRoot.AddChild(_toast);
        _clueCloseup = new TextureRect
        {
            Position = new Vector2(112, 62),
            Size = new Vector2(416, 234),
            ExpandMode = TextureRect.ExpandModeEnum.IgnoreSize,
            StretchMode = TextureRect.StretchModeEnum.KeepAspectCentered,
            MouseFilter = Control.MouseFilterEnum.Ignore,
            Visible = false
        };
        _interfaceRoot.AddChild(_clueCloseup);
        _toast.MoveToFront();

        _puzzle = new DreamPuzzlePanel { Name = "PuzzlePanel" };
        _puzzle.Submitted += SubmitPuzzle;
        _interfaceRoot.AddChild(_puzzle);
        _inventory = new DreamInventoryPanel { Name = "InventoryPanel" };
        _inventory.ItemSelected += SelectItem;
        _inventory.PinUseRequested += AskPinUse;
        _inventory.HintRequested += RequestHint;
        _interfaceRoot.AddChild(_inventory);

        _pinConfirmation = new ConfirmationDialog
        {
            Title = "确认使用",
            DialogText = "真的要刺下去吗？这可能让你永久失去尚未弄清的信息。",
            OkButtonText = "刺下去",
            CancelButtonText = "收起大头针",
            Exclusive = true
        };
        _pinConfirmation.Confirmed += ConfirmPinUse;
        _pinConfirmation.Canceled += ReleasePinModal;
        _interfaceRoot.AddChild(_pinConfirmation);
    }

    public void ApplySettings(PlayerSettings settings)
    {
        if (_interfaceRoot?.Theme is not null)
        {
            _interfaceRoot.Theme.DefaultFontSize = Mathf.RoundToInt(14 * settings.FontScale);
        }
        _prompt?.AddThemeStyleboxOverride("normal", UiStyles.Panel(
            settings.HighContrastPrompts ? new Color("000000ff") : new Color("152536ed"), 5));
        _area?.ApplySettings(settings);
    }

    private void LoadArea(string areaId)
    {
        var path = areaId switch
        {
            DreamEscapeIds.AreaCorridor => "res://scenes/dream/DreamCorridor.tscn",
            DreamEscapeIds.AreaClassroom => "res://scenes/dream/DreamClassroom.tscn",
            DreamEscapeIds.AreaMusic => "res://scenes/dream/DreamMusicRoom.tscn",
            DreamEscapeIds.AreaArchive => "res://scenes/dream/DreamArchiveRoom.tscn",
            DreamEscapeIds.AreaAuditorium => "res://scenes/dream/DreamAuditorium.tscn",
            _ => throw new ArgumentOutOfRangeException(nameof(areaId), areaId, "Unknown dream area.")
        };
        var packed = ResourceLoader.Load<PackedScene>(path)
            ?? throw new FileNotFoundException($"Dream area scene '{path}' is missing.");
        var next = packed.Instantiate<DreamAreaController>();
        var previous = _area;
        DisconnectArea();
        _areaHost.AddChild(next);
        _area = next;
        next.InteractionRequested += HandleInteraction;
        next.PromptChanged += SetPrompt;
        next.SetGamepadPrompts(_usingGamepad);
        next.SetSelectedItem(_selectedItemId);
        previous?.QueueFree();
        RefreshHud();
    }

    private void DisconnectArea()
    {
        if (_area is null) return;
        _area.InteractionRequested -= HandleInteraction;
        _area.PromptChanged -= SetPrompt;
        _area = null;
    }

    private void HandleInteraction(string targetId)
    {
        if (_session.Modal.IsBlocked) return;
        if (targetId.StartsWith("area:", StringComparison.Ordinal))
        {
            var nextArea = targetId["area:".Length..];
            var spawn = nextArea switch
            {
                DreamEscapeIds.AreaCorridor => new Vector2(320, 270),
                DreamEscapeIds.AreaClassroom => new Vector2(588, 232),
                _ => new Vector2(320, 282)
            };
            var result = _session.MoveDreamToArea(nextArea, spawn.X, spawn.Y);
            if (result.Changed) LoadArea(nextArea);
            else ShowResult(result);
            return;
        }
        if (targetId.StartsWith("pickup:", StringComparison.Ordinal))
        {
            ApplyAndRefresh(_session.PickupDreamItem(targetId["pickup:".Length..]));
            return;
        }
        if (targetId.StartsWith("observe:", StringComparison.Ordinal))
        {
            var observation = targetId["observe:".Length..];
            var result = _session.ObserveDreamTarget(observation);
            var special = observation switch
            {
                "target.classroom.window" => "三扇窗格分别留下划痕，合起来正好是三处标记。",
                "target.classroom.clock" => "时钟的分针停在十二，时针停在七。",
                "target.classroom.duty_roster" => "值日表的第二项被红笔圈了起来。",
                "target.music.listen_window" => "你们听见短促敲击；文字与矩形波形都标记为“木鱼”。",
                "target.music.listen_lectern" => "连续音阶从讲台下传来；键形波与文字标记为“钢琴”。",
                "target.music.listen_door" => "先是清脆三角声，后是圆润铃声；图形和文字标为“三角铁、手铃”。",
                "target.archive.rules" => "纸上写着：合唱 7:10、广播 7:30、美术 16:00、田径 17:00，按当天最早使用时间归档。",
                _ => string.Empty
            };
            ApplyAndRefresh(result, result.Changed ? special : null);
            return;
        }
        if (targetId.StartsWith("puzzle:", StringComparison.Ordinal))
        {
            OpenPuzzle(targetId["puzzle:".Length..]);
            return;
        }
        if (targetId == "hint:xiaoyu")
        {
            _inventory.Open(_selectedItemId, DreamInventoryPage.Xiaoyu);
            return;
        }
        if (targetId == "locked:room_three")
        {
            ShowToast("门板里传来四种乐器的回声。音乐教室里还有没解决的机关。");
            return;
        }
        if (targetId == "action:left_lever")
        {
            ApplyAndRefresh(_session.PrepareDreamLeftLever());
            return;
        }
        if (targetId == "action:open_exit")
        {
            var result = _session.OpenDreamExit();
            ShowResult(result);
            if (result.Changed) ExitOpened?.Invoke();
            return;
        }

        UseSelectedItem(targetId);
    }

    private void UseSelectedItem(string targetId)
    {
        if (string.IsNullOrEmpty(_selectedItemId))
        {
            ShowToast("先打开随身物品，选中要使用的道具。");
            return;
        }
        var result = _session.UseDreamItem(_selectedItemId, targetId);
        string? special = null;
        if (result.Changed)
        {
            special = result.FeedbackKey switch
            {
                "DREAM_FEEDBACK_BLACKBOARD_REVEALED" => "粉笔字浮现，只留下三个依次相连的图案：窗格—时钟—值日表。",
                "DREAM_FEEDBACK_SCORE_REVEALED" => "灰尘下露出四段编号：1—4—2—3；数字同时用长短谱线表示。",
                "DREAM_FEEDBACK_SEATS_REVEALED" => "红尺滤去杂色，四个座位依次显出：B2—D1—A3—C4。",
                "DREAM_FEEDBACK_RECORDER_PLAYING" => "录音恢复：窗帘摩擦、时钟报时、校铃、翻点名册；面板也依次亮起四个图标。",
                _ => null
            };
            if (result.FeedbackKey == "DREAM_FEEDBACK_SEATS_REVEALED")
            {
                ShowCloseup("res://assets/generated/dream_escape/class-photo-filter-closeup.png");
            }
            else if (result.FeedbackKey == "DREAM_FEEDBACK_RECORDER_PLAYING")
            {
                ShowCloseup("res://assets/generated/dream_escape/recorder-closeup.png");
            }
        }
        if (!_session.State.DreamEscape!.Items.Contains(_selectedItemId)) _selectedItemId = string.Empty;
        ApplyAndRefresh(result, special);
    }

    private void OpenPuzzle(string shortId)
    {
        var digits = Enumerable.Range(0, 10).Select(value => (value.ToString(), value.ToString())).ToArray();
        switch (shortId)
        {
            case "teacher_desk":
                _puzzle.Open(DreamEscapeIds.PuzzleTeacherDesk, "教师桌密码锁", "按黑板标出的观察顺序输入三位数字。", digits, 3, true);
                break;
            case "music_cage":
                _puzzle.Open(DreamEscapeIds.PuzzleMusicCage, "器材间音板", "依次按下四块音板；编号同时有长短谱线标记。", digits[1..5], 4);
                break;
            case "listening":
                _puzzle.Open(DreamEscapeIds.PuzzleListening, "辨听排列", "将三处听见的四种声音按出现顺序排列。", InstrumentChoices(), 4);
                break;
            case "archive_order":
                _puzzle.Open(DreamEscapeIds.PuzzleArchiveOrder, "社团档案", "按墙上的归档规则排列四份档案。", ClubChoices(), 4);
                break;
            case "attendance":
                _puzzle.Open(DreamEscapeIds.PuzzleAttendance, "点名柜", "按合影中被圈出的先后输入四个座位坐标。", CoordinateChoices(), 4);
                break;
            case "room_four_door":
                _puzzle.Open(DreamEscapeIds.PuzzleRoomFourDoor, "第四扇门", "按旧录音机留下的图标顺序排列。", BroadcastChoices(), 4);
                break;
            case "final_desk":
                _puzzle.Open(DreamEscapeIds.PuzzleFinalDesk, "数字控制板", "复现旧教室教师桌的三位输入。", digits, 3, true);
                break;
            case "final_instruments":
                _puzzle.Open(DreamEscapeIds.PuzzleFinalInstruments, "乐器控制板", "复现音乐教室的四种乐器顺序。", InstrumentChoices(), 4);
                break;
            case "final_broadcast":
                _puzzle.Open(DreamEscapeIds.PuzzleFinalBroadcast, "广播控制板", "复现录音机波形下的四个图标。", BroadcastChoices(), 4);
                break;
            default:
                ShowToast("这个机关还没有可操作的部分。");
                break;
        }
    }

    private void SubmitPuzzle(string puzzleId, string[] answer)
    {
        var result = _session.SubmitDreamPuzzle(puzzleId, answer);
        var special = result.Changed ? result.FeedbackKey switch
        {
            "DREAM_FEEDBACK_DESK_OPENED" => "锁舌弹开。教师桌里有第二室钥匙，还有一张写着“三角维护匙”的便签。",
            "DREAM_FEEDBACK_XIAOYU_JOINED" => "器材间打开。被困的女孩说她叫林晓雨，并决定先和你一起找出口。",
            "DREAM_FEEDBACK_ROOM_THREE_OPEN" => "四种音色重叠成一声短鸣，走廊第三扇门解锁了。",
            "DREAM_FEEDBACK_ARCHIVE_DRAWER_OPENED" => "抽屉打开：里面是三角维护钥匙和红色透明尺。",
            "DREAM_FEEDBACK_ATTENDANCE_OPENED" => "点名柜弹开，里面放着录音带和录音机摇柄。",
            "DREAM_FEEDBACK_ROOM_FOUR_OPEN" => "第四扇门后的锁链落地。旧礼堂已经开放。",
            _ => null
        } : null;
        ApplyAndRefresh(result, special);
    }

    private void SelectItem(string itemId)
    {
        _selectedItemId = itemId;
        _area?.SetSelectedItem(_selectedItemId);
        RefreshHud();
    }

    private IDisposable? _pinLease;

    private void AskPinUse()
    {
        _inventory.ClosePanel();
        _pinLease?.Dispose();
        _pinLease = _session.Modal.Acquire("dream-pin-confirmation");
        _pinConfirmation.PopupCentered(new Vector2I(430, 180));
    }

    private void ReleasePinModal()
    {
        _pinLease?.Dispose();
        _pinLease = null;
    }

    private void ConfirmPinUse()
    {
        ReleasePinModal();
        var result = _session.UseDreamItem(DreamEscapeIds.ItemPin, "target.self");
        if (result.Changed) PinWakeRequested?.Invoke();
        else ShowResult(result);
    }

    private void RequestHint(string puzzleId)
    {
        var result = _session.AdvanceDreamHint(puzzleId);
        _inventory.Refresh();
        ShowToast(HintText(result.FeedbackKey));
    }

    private void ApplyAndRefresh(DreamActionResult result, string? successText = null)
    {
        ShowResult(result, successText);
        if (result.Changed)
        {
            _inventory.Refresh();
            if (_session.State.DreamEscape is { } dream) LoadArea(dream.CurrentAreaId);
        }
        RefreshHud();
    }

    private void ShowResult(DreamActionResult result, string? successText = null) =>
        ShowToast(successText ?? FeedbackText(result.FeedbackKey));

    private void SetPrompt(string prompt)
    {
        _prompt.Text = prompt;
        _prompt.Visible = !string.IsNullOrEmpty(prompt);
    }

    private void RefreshHud()
    {
        if (_session.State.DreamEscape is not { } dream || _areaLabel is null) return;
        _areaLabel.Text = AreaName(dream.CurrentAreaId);
        _selected.Text = string.IsNullOrEmpty(_selectedItemId)
            ? $"[{(_usingGamepad ? "X" : "I/Tab")}] 物品"
            : DreamInventoryPanel.ItemName(_selectedItemId);
        var selectedDefinition = _session.Content.Items.Items
            .FirstOrDefault(item => item.Id == _selectedItemId);
        _selectedIcon.Texture = selectedDefinition is null
            ? null
            : ResourceLoader.Load<Texture2D>(selectedDefinition.IconPath);
        _selectedIcon.Visible = _selectedIcon.Texture is not null;
        _selected.TooltipText = string.IsNullOrEmpty(_selectedItemId)
            ? "打开随身物品"
            : $"当前选用：{DreamInventoryPanel.ItemName(_selectedItemId)}";
        _objective.Text = CurrentObjective(dream);
        _xiaoyuPortrait.Visible = dream.XiaoyuJoined;
        _area?.SetSelectedItem(_selectedItemId);
    }

    private static string CurrentObjective(DreamEscapeState dream)
    {
        if (dream.Status == DreamEscapeStatus.ExitOpened) return "出口已经开启";
        if (dream.CurrentAreaId == DreamEscapeIds.AreaClassroom)
        {
            if (!dream.SolvedPuzzles.Contains("dream.pickup.chalk")) return "调查教室，寻找可用物品";
            if (!dream.SolvedPuzzles.Contains(DreamEscapeIds.PuzzleBlackboard)) return "让黑板擦痕重新显形";
            if (!dream.DiscoveredClues.Contains(DreamEscapeIds.ClueClassroomWindow) ||
                !dream.DiscoveredClues.Contains(DreamEscapeIds.ClueClassroomClock) ||
                !dream.DiscoveredClues.Contains(DreamEscapeIds.ClueClassroomDutyRoster))
                return "按黑板顺序观察三个目标";
            if (!dream.SolvedPuzzles.Contains(DreamEscapeIds.PuzzleTeacherDesk)) return "打开教师桌";
            if (dream.Items.Contains(DreamEscapeIds.ItemRoomTwoKey)) return "去走廊开启第二室";
        }
        if (dream.CurrentAreaId == DreamEscapeIds.AreaCorridor &&
            !dream.UnlockedAreas.Contains(DreamEscapeIds.AreaMusic))
            return dream.Items.Contains(DreamEscapeIds.ItemRoomTwoKey) ? "用钥匙开启第二室" : "第二室仍然锁着";
        if (dream.CurrentAreaId == DreamEscapeIds.AreaMusic && !dream.XiaoyuJoined)
            return "打开器材间，救出被困女孩";
        return $"已开放 {dream.UnlockedAreas.Count - 1}/4 间房间 · 记录 {dream.DiscoveredClues.Count}";
    }

    private void ShowToast(string message, double seconds = 4.2)
    {
        var version = ++_toastVersion;
        _toast.Text = message;
        _toast.Visible = true;
        _ = HideToastLater(version, seconds);
    }

    private async Task HideToastLater(int version, double seconds)
    {
        await ToSignal(GetTree().CreateTimer(seconds), SceneTreeTimer.SignalName.Timeout);
        if (version == _toastVersion && GodotObject.IsInstanceValid(_toast))
        {
            _toast.Visible = false;
            _clueCloseup.Visible = false;
        }
    }

    private void ShowCloseup(string path)
    {
        _clueCloseup.Texture = ResourceLoader.Load<Texture2D>(path);
        _clueCloseup.Visible = _clueCloseup.Texture is not null;
    }

    private static IReadOnlyList<(string, string)> InstrumentChoices() =>
        [("woodblock", "木鱼　▮▮"), ("piano", "钢琴　▂▄▆"), ("triangle", "三角铁　△"), ("handbell", "手铃　○")];

    private static IReadOnlyList<(string, string)> ClubChoices() =>
        [("choir", "合唱 7:10"), ("broadcast", "广播 7:30"), ("art", "美术 16:00"), ("track", "田径 17:00")];

    private static IReadOnlyList<(string, string)> CoordinateChoices() =>
        [("A3", "A3"), ("B2", "B2"), ("C4", "C4"), ("D1", "D1"), ("A1", "A1"), ("D4", "D4")];

    private static IReadOnlyList<(string, string)> BroadcastChoices() =>
        [("curtain", "窗帘　▥"), ("clock", "时钟　◷"), ("bell", "校铃　⌂"), ("register", "点名册　▤")];

    private static string AreaName(string id) => id switch
    {
        DreamEscapeIds.AreaCorridor => "旧校舍走廊", DreamEscapeIds.AreaClassroom => "第一室·旧教室",
        DreamEscapeIds.AreaMusic => "第二室·音乐教室", DreamEscapeIds.AreaArchive => "第三室·档案兼广播室",
        DreamEscapeIds.AreaAuditorium => "第四室·旧礼堂", _ => id
    };

    private static string FeedbackText(string key) => key switch
    {
        "DREAM_FEEDBACK_ITEM_PICKED_UP" => "物品已放入随身物品栏。",
        "DREAM_FEEDBACK_ROOM_TWO_OPEN" => "黄铜钥匙转动，第二扇门打开了。",
        "DREAM_FEEDBACK_MAP_CABINET_OPENED" => "三角钥匙正好吻合。地图柜里压着一张全班合影。",
        "DREAM_FEEDBACK_TAPE_INSERTED" => "录音带装好了，但转轴仍然不动。",
        "DREAM_FEEDBACK_FINAL_PANEL_SOLVED" => "控制板亮起稳定的青绿色光。",
        "DREAM_FEEDBACK_XIAOYU_READY" => "林晓雨按住左侧机关：“右边交给你。”",
        "DREAM_FEEDBACK_EXIT_OPENED" => "总闸落下，舞台深处的出口缓缓开启。",
        "DREAM_FEEDBACK_WRONG_ANSWER" => "机关发出短促的拒绝声，输入已复位。",
        "DREAM_FEEDBACK_ITEM_DOES_NOT_FIT" => "这个道具与眼前的结构对不上。",
        "DREAM_FEEDBACK_NEED_BLACKBOARD_CLUE" => "锁盘没有提示。黑板上似乎还有没显出的痕迹。",
        "DREAM_FEEDBACK_NEED_CLASSROOM_OBSERVATIONS" => "密码需要三个数字。按黑板顺序把窗、时钟和值日表都观察一遍。",
        "DREAM_FEEDBACK_NEED_SCORE_CLUE" => "四块音板没有编号；附近的乐谱也许能留下提示。",
        "DREAM_FEEDBACK_NEED_LISTENING_CLUES" => "你们还没有在三个标记位置都辨听过。",
        "DREAM_FEEDBACK_NEED_ARCHIVE_RULES" => "先读懂墙上的社团归档规则。",
        "DREAM_FEEDBACK_NEED_SEAT_COORDINATES" => "点名柜需要座位坐标，合影上也许藏着它们。",
        "DREAM_FEEDBACK_NEED_BROADCAST_CLUE" => "第四扇门需要广播图标序列。先修好录音机。",
        "DREAM_FEEDBACK_NEED_TAPE_FIRST" => "摇柄能装上，但录音机里还没有录音带。",
        "DREAM_FEEDBACK_FINAL_PANELS_INCOMPLETE" => "三块控制板必须全部亮起，晓雨才能守住左侧机关。",
        "DREAM_FEEDBACK_XIAOYU_NOT_READY" => "右侧总闸回弹了；需要有人同时按住左侧机关。",
        "DREAM_FEEDBACK_PUZZLE_ALREADY_SOLVED" => "这个机关已经解开。",
        "DREAM_FEEDBACK_CLUE_ALREADY_RECORDED" => "这条观察已经在记录里。",
        "DREAM_FEEDBACK_AREA_LOCKED" => "门仍然锁着。",
        _ => "机关没有发生变化。"
    };

    private static string HintText(string key)
    {
        if (!key.StartsWith("DREAM_HINT_", StringComparison.Ordinal)) return FeedbackText(key);
        var level = key[^1] - '0';
        var baseKey = key[..^2];
        return (baseKey, level) switch
        {
            ("DREAM_HINT_CLASSROOM_BLACKBOARD", 1) => "目标提示：找一件能让黑板旧痕重新显形的东西。",
            ("DREAM_HINT_CLASSROOM_BLACKBOARD", 2) => "关联提示：讲台附近掉着仍可使用的粉笔。",
            ("DREAM_HINT_CLASSROOM_BLACKBOARD", 3) => "近答案提示：选中粉笔，对黑板使用。",
            ("DREAM_HINT_CLASSROOM_TEACHER_DESK", 1) => "目标提示：密码来自黑板点出的三个观察对象。",
            ("DREAM_HINT_CLASSROOM_TEACHER_DESK", 2) => "关联提示：依次看窗格、时钟、值日表旁的数字。",
            ("DREAM_HINT_CLASSROOM_TEACHER_DESK", 3) => "近答案提示：输入 3、7、2。",
            ("DREAM_HINT_MUSIC_CAGE", 1) => "目标提示：音板的次序藏在蒙尘乐谱里。",
            ("DREAM_HINT_MUSIC_CAGE", 2) => "关联提示：粉笔也能描出乐谱上褪色的编号。",
            ("DREAM_HINT_MUSIC_CAGE", 3) => "近答案提示：依次按 1、4、2、3。",
            ("DREAM_HINT_MUSIC_LISTENING", 1) => "目标提示：三个位置合起来才有四种声音。",
            ("DREAM_HINT_MUSIC_LISTENING", 2) => "关联提示：窗边一个、讲台一个、门边两个；文字与波形可代替声音。",
            ("DREAM_HINT_MUSIC_LISTENING", 3) => "近答案提示：木鱼、钢琴、三角铁、手铃。",
            ("DREAM_HINT_ARCHIVE_CLUB_ORDER", 1) => "目标提示：档案不是按社团名称排序。",
            ("DREAM_HINT_ARCHIVE_CLUB_ORDER", 2) => "关联提示：比较墙上记录的每天最早使用时间。",
            ("DREAM_HINT_ARCHIVE_CLUB_ORDER", 3) => "近答案提示：合唱、广播、美术、田径。",
            ("DREAM_HINT_MUSIC_ATTENDANCE", 1) => "目标提示：点名柜等待全班合影中的座位。",
            ("DREAM_HINT_MUSIC_ATTENDANCE", 2) => "关联提示：合影要配合红色透明尺观察。",
            ("DREAM_HINT_MUSIC_ATTENDANCE", 3) => "近答案提示：B2、D1、A3、C4。",
            ("DREAM_HINT_CORRIDOR_ROOM_FOUR_DOOR", 1) => "目标提示：门上的四个图标来自旧录音机。",
            ("DREAM_HINT_CORRIDOR_ROOM_FOUR_DOOR", 2) => "关联提示：录音带和摇柄都装好后，声音也有图形替代。",
            ("DREAM_HINT_CORRIDOR_ROOM_FOUR_DOOR", 3) => "近答案提示：窗帘、时钟、校铃、点名册。",
            ("DREAM_HINT_AUDITORIUM_DESK_PANEL", 3) => "近答案提示：复现 3、7、2。",
            ("DREAM_HINT_AUDITORIUM_INSTRUMENT_PANEL", 3) => "近答案提示：复现木鱼、钢琴、三角铁、手铃。",
            ("DREAM_HINT_AUDITORIUM_BROADCAST_PANEL", 3) => "近答案提示：复现窗帘、时钟、校铃、点名册。",
            (_, 1) => "目标提示：先确认这个机关需要复现哪一间房的输入。",
            (_, 2) => "关联提示：打开记录，寻找相同形状或波形的线索。",
            (_, 3) => "近答案提示：答案已经出现在此前解开的机关中。",
            _ => "提示已经达到最明确的等级。"
        };
    }
}
