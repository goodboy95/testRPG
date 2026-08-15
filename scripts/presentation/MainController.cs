using System.Globalization;
using Godot;
using TownRpg.Application;
using TownRpg.Core.Domain;
using TownRpg.Core.Services;

namespace TownRpg.Presentation;

public partial class MainController : Node
{
    private GameSession _session = null!;
    private Node2D _worldHost = null!;
    private CanvasLayer _uiLayer = null!;
    private Control _uiRoot = null!;
    private WorldSceneController? _world;
    private Control? _titleScreen;
    private Control? _pauseScreen;
    private Control? _completionScreen;
    private IDisposable? _titleLease;
    private IDisposable? _journalLease;
    private IDisposable? _pauseLease;
    private IDisposable? _battleLease;
    private IDisposable? _completionLease;
    private Label _timeLabel = null!;
    private Label _objectiveLabel = null!;
    private Label _promptLabel = null!;
    private Label _inputHint = null!;
    private Label _toast = null!;
    private Label _debugLabel = null!;
    private Label? _titleStatus;
    private Control _hud = null!;
    private DialoguePanel _dialogue = null!;
    private JournalPanel _journal = null!;
    private BattlePanel _battle = null!;
    private bool _flowRunning;
    private bool _battleEnding;
    private bool _usingGamepad;
    private int _toastVersion;

    public override void _Ready()
    {
        _session = GetNode<GameSession>("/root/GameSession");
        _session.StateChanged += RefreshHud;

        _worldHost = new Node2D { Name = "WorldHost" };
        AddChild(_worldHost);

        _uiLayer = new CanvasLayer { Name = "Interface", Layer = 10 };
        AddChild(_uiLayer);
        _uiRoot = new Control { Theme = UiStyles.CreateTheme() };
        _uiRoot.SetAnchorsAndOffsetsPreset(Control.LayoutPreset.FullRect);
        _uiLayer.AddChild(_uiRoot);

        BuildHud();
        BuildModalPanels();
        ApplySettingsVisuals();
        ShowTitle();
    }

    public override void _ExitTree()
    {
        if (_session is not null)
        {
            _session.StateChanged -= RefreshHud;
        }

        DisconnectWorld();
        _titleLease?.Dispose();
        _journalLease?.Dispose();
        _pauseLease?.Dispose();
        _battleLease?.Dispose();
        _completionLease?.Dispose();
    }

    public override void _Input(InputEvent inputEvent)
    {
        var nextGamepad = inputEvent is InputEventJoypadButton button && button.Pressed ||
                          inputEvent is InputEventJoypadMotion motion && Math.Abs(motion.AxisValue) >= 0.35f;
        var nextKeyboard = inputEvent is InputEventKey key && key.Pressed ||
                           inputEvent is InputEventMouseButton mouseButton && mouseButton.Pressed;
        if ((nextGamepad && !_usingGamepad) || (nextKeyboard && _usingGamepad))
        {
            _usingGamepad = nextGamepad;
            UpdateInputHint();
        }
    }

    public override void _UnhandledInput(InputEvent inputEvent)
    {
        if (_titleScreen is not null)
        {
            return;
        }

        if (inputEvent is InputEventKey key && key.Pressed && !key.Echo && key.Keycode == Key.F3)
        {
            _debugLabel.Visible = !_debugLabel.Visible;
            RefreshHud();
            GetViewport().SetInputAsHandled();
            return;
        }

        if (inputEvent.IsActionPressed("journal"))
        {
            if (_journal.Visible)
            {
                CloseJournal();
            }
            else if (!_flowRunning && _pauseScreen is null && !_battle.Visible && _completionScreen is null)
            {
                OpenJournal();
            }

            GetViewport().SetInputAsHandled();
            return;
        }

        if (inputEvent.IsActionPressed("pause_menu"))
        {
            if (_journal.Visible)
            {
                CloseJournal();
            }
            else if (_pauseScreen is not null)
            {
                ClosePause();
            }
            else if (!_flowRunning && !_battle.Visible && _completionScreen is null)
            {
                OpenPause();
            }

            GetViewport().SetInputAsHandled();
        }
    }

    private void BuildHud()
    {
        _hud = new Control();
        _hud.SetAnchorsAndOffsetsPreset(Control.LayoutPreset.FullRect);
        _hud.MouseFilter = Control.MouseFilterEnum.Ignore;
        _uiRoot.AddChild(_hud);

        var top = new PanelContainer
        {
            Position = new Vector2(10, 9),
            Size = new Vector2(620, 51),
            MouseFilter = Control.MouseFilterEnum.Ignore
        };
        top.AddThemeStyleboxOverride("panel", UiStyles.Panel(new Color("172337d8"), 6));
        _hud.AddChild(top);
        var row = new HBoxContainer();
        top.AddChild(row);
        _timeLabel = new Label { CustomMinimumSize = new Vector2(135, 24) };
        _timeLabel.AddThemeColorOverride("font_color", new Color("f2c66d"));
        row.AddChild(_timeLabel);
        _objectiveLabel = new Label
        {
            SizeFlagsHorizontal = Control.SizeFlags.ExpandFill,
            HorizontalAlignment = HorizontalAlignment.Right
        };
        row.AddChild(_objectiveLabel);

        _promptLabel = new Label
        {
            Position = new Vector2(120, 315),
            Size = new Vector2(400, 31),
            HorizontalAlignment = HorizontalAlignment.Center,
            VerticalAlignment = VerticalAlignment.Center
        };
        _promptLabel.AddThemeStyleboxOverride("normal", UiStyles.Panel(new Color("1a2638e5"), 5));
        _promptLabel.AddThemeColorOverride("font_color", new Color("f5e6bd"));
        _hud.AddChild(_promptLabel);

        _inputHint = new Label
        {
            Position = new Vector2(12, 64),
            Size = new Vector2(610, 18),
            HorizontalAlignment = HorizontalAlignment.Right
        };
        _inputHint.AddThemeFontSizeOverride("font_size", 10);
        _inputHint.AddThemeColorOverride("font_color", new Color("d2d8dc"));
        _hud.AddChild(_inputHint);

        _debugLabel = new Label
        {
            Position = new Vector2(12, 68),
            Size = new Vector2(260, 78),
            Visible = false
        };
        _debugLabel.AddThemeStyleboxOverride("normal", UiStyles.Panel(new Color("0b111be4"), 4));
        _debugLabel.AddThemeFontSizeOverride("font_size", 10);
        _hud.AddChild(_debugLabel);

        _toast = new Label
        {
            Position = new Vector2(135, 76),
            Size = new Vector2(370, 40),
            HorizontalAlignment = HorizontalAlignment.Center,
            VerticalAlignment = VerticalAlignment.Center,
            Visible = false
        };
        _toast.AddThemeStyleboxOverride("normal", UiStyles.Panel(new Color("263745f0"), 6));
        _toast.AddThemeColorOverride("font_color", new Color("f2c66d"));
        _hud.AddChild(_toast);
        _hud.Visible = false;
        UpdateInputHint();
    }

    private void BuildModalPanels()
    {
        _dialogue = new DialoguePanel { Name = "Dialogue" };
        _uiRoot.AddChild(_dialogue);
        _journal = new JournalPanel { Name = "Journal" };
        _journal.Closed += CloseJournal;
        _uiRoot.AddChild(_journal);
        _battle = new BattlePanel { Name = "Battle" };
        _battle.ActionSelected += OnBattleAction;
        _battle.RetrySelected += RetryBattle;
        _uiRoot.AddChild(_battle);
    }

    private void ShowTitle()
    {
        CloseCompletion();
        DisconnectWorld();
        foreach (var child in _worldHost.GetChildren())
        {
            child.QueueFree();
        }

        _world = null;
        _hud.Visible = false;
        _titleLease ??= _session.Modal.Acquire("title");
        _titleScreen?.QueueFree();
        _titleScreen = NewScreen("TitleScreen");

        var texture = ResourceLoader.Load<Texture2D>("res://assets/generated/title-town-dusk.png");
        if (texture is not null)
        {
            var background = new TextureRect
            {
                Texture = texture,
                ExpandMode = TextureRect.ExpandModeEnum.IgnoreSize,
                StretchMode = TextureRect.StretchModeEnum.KeepAspectCovered
            };
            background.SetAnchorsAndOffsetsPreset(Control.LayoutPreset.FullRect);
            _titleScreen.AddChild(background);
        }

        var shade = new ColorRect { Color = new Color("07101b8f") };
        shade.SetAnchorsAndOffsetsPreset(Control.LayoutPreset.FullRect);
        _titleScreen.AddChild(shade);

        var panel = new PanelContainer
        {
            Position = new Vector2(38, 53),
            Size = new Vector2(292, 260)
        };
        panel.AddThemeStyleboxOverride("panel", UiStyles.Panel(new Color("182536e9"), 10));
        _titleScreen.AddChild(panel);
        var column = new VBoxContainer();
        column.AddThemeConstantOverride("separation", 8);
        panel.AddChild(column);
        var title = new Label { Text = "雾山旧轨" };
        title.AddThemeFontSizeOverride("font_size", 32);
        title.AddThemeColorOverride("font_color", new Color("f2c66d"));
        column.AddChild(title);
        var subtitle = new Label
        {
            Text = "一个关于信任、旧铁路与夏日回声的剧情生活 RPG",
            AutowrapMode = TextServer.AutowrapMode.WordSmart,
            CustomMinimumSize = new Vector2(250, 44)
        };
        column.AddChild(subtitle);
        var newGame = UiStyles.Button("开始新游戏");
        newGame.Disabled = !string.IsNullOrEmpty(_session.InitializationError);
        newGame.Pressed += ShowNameEntry;
        column.AddChild(newGame);
        var continueGame = UiStyles.Button("继续");
        continueGame.Disabled = !string.IsNullOrEmpty(_session.InitializationError) ||
                                !_session.HasRecoverableSave;
        continueGame.Pressed += ContinueGame;
        column.AddChild(continueGame);
        var note = new Label
        {
            Text = "当前构建：两日生活＋首次异象垂直切片",
            AutowrapMode = TextServer.AutowrapMode.WordSmart
        };
        note.AddThemeFontSizeOverride("font_size", 11);
        note.AddThemeColorOverride("font_color", new Color("b9d1c5"));
        column.AddChild(note);
        _titleStatus = new Label
        {
            Text = string.IsNullOrEmpty(_session.InitializationError)
                ? string.Empty
                : $"内容校验失败，新游戏已禁用：{_session.InitializationError}",
            AutowrapMode = TextServer.AutowrapMode.WordSmart,
            CustomMinimumSize = new Vector2(250, 30)
        };
        _titleStatus.AddThemeFontSizeOverride("font_size", 10);
        _titleStatus.AddThemeColorOverride("font_color", new Color("ef9a8d"));
        column.AddChild(_titleStatus);
        newGame.GrabFocus();
    }

    private void ShowNameEntry()
    {
        _titleScreen?.QueueFree();
        _titleScreen = NewScreen("NameEntry");
        var shade = new ColorRect { Color = new Color("0e1826f0") };
        shade.SetAnchorsAndOffsetsPreset(Control.LayoutPreset.FullRect);
        _titleScreen.AddChild(shade);
        var frame = new PanelContainer
        {
            Position = new Vector2(165, 62),
            Size = new Vector2(310, 238)
        };
        frame.AddThemeStyleboxOverride("panel", UiStyles.Panel(new Color("1d2a3af2"), 10));
        _titleScreen.AddChild(frame);
        var column = new VBoxContainer();
        column.AddThemeConstantOverride("separation", 8);
        frame.AddChild(column);
        var title = new Label { Text = "写下这个暑假的称呼" };
        title.AddThemeFontSizeOverride("font_size", 21);
        title.AddThemeColorOverride("font_color", new Color("f2c66d"));
        column.AddChild(title);
        column.AddChild(new Label { Text = "姓名（默认：小岚，最多 8 个字符）" });
        var name = new LineEdit { Text = "小岚", MaxLength = 16 };
        column.AddChild(name);
        column.AddChild(new Label { Text = "他人如何称呼你" });
        var titleChoice = new OptionButton();
        titleChoice.AddItem("同学");
        titleChoice.AddItem("朋友");
        titleChoice.AddItem("前辈");
        column.AddChild(titleChoice);
        var error = new Label();
        error.AddThemeColorOverride("font_color", new Color("ef9a8d"));
        column.AddChild(error);
        var buttons = new HBoxContainer();
        column.AddChild(buttons);
        var back = UiStyles.Button("返回");
        back.SizeFlagsHorizontal = Control.SizeFlags.ExpandFill;
        back.Pressed += ShowTitle;
        buttons.AddChild(back);
        var begin = UiStyles.Button("开始暑假");
        begin.SizeFlagsHorizontal = Control.SizeFlags.ExpandFill;
        begin.Pressed += () =>
        {
            var trimmed = name.Text.Trim();
            if (string.IsNullOrWhiteSpace(trimmed))
            {
                trimmed = "小岚";
            }

            if (new StringInfo(trimmed).LengthInTextElements > 8)
            {
                error.Text = "姓名请控制在 8 个字符以内。";
                return;
            }

            _ = StartNewGameAsync(trimmed, titleChoice.GetItemText(titleChoice.Selected));
        };
        buttons.AddChild(begin);
        name.GrabFocus();
        name.SelectAll();
    }

    private async Task StartNewGameAsync(string playerName, string playerTitle)
    {
        _session.NewGame(playerName, playerTitle);
        ApplySettingsVisuals();
        CloseTitle();
        LoadWorld("old_house", new Vector2(320, 260));
        await RunWakeupAsync();
        _session.SaveCheckpoint();
    }

    private void ContinueGame()
    {
        if (!_session.TryLoad(out var error))
        {
            if (_titleStatus is not null)
            {
                _titleStatus.Text = error;
            }
            return;
        }

        CloseTitle();
        ApplySettingsVisuals();
        LoadWorld(_session.State.Player.LocationId);
        ShowToast("存档已载入。主线不会因为多睡几天而错过。");
        if (!_session.State.World.HasFact("slice.d1.awake"))
        {
            _ = RunWakeupAsync();
        }
    }

    private void CloseTitle()
    {
        _titleScreen?.QueueFree();
        _titleScreen = null;
        _titleLease?.Dispose();
        _titleLease = null;
        _hud.Visible = true;
        RefreshHud();
    }

    private Control NewScreen(string name)
    {
        var screen = new Control { Name = name };
        screen.SetAnchorsAndOffsetsPreset(Control.LayoutPreset.FullRect);
        _uiRoot.AddChild(screen);
        return screen;
    }

    private void LoadWorld(string locationId, Vector2? spawn = null)
    {
        var normalized = locationId switch
        {
            "old_house" => "old_house",
            "station_quarter" => "station_quarter",
            _ => throw new ArgumentOutOfRangeException(
                nameof(locationId),
                locationId,
                "Unknown player location.")
        };
        _session.State.Player.LocationId = normalized;
        if (spawn is Vector2 position)
        {
            _session.State.Player.PositionX = position.X;
            _session.State.Player.PositionY = position.Y;
        }

        var path = normalized == "old_house"
            ? "res://scenes/world/OldHouse.tscn"
            : "res://scenes/world/StationQuarter.tscn";
        var packed = ResourceLoader.Load<PackedScene>(path)
            ?? throw new FileNotFoundException($"World scene '{path}' is missing.");
        var next = packed.Instantiate<WorldSceneController>();
        var previous = _world;
        DisconnectWorld();
        _worldHost.AddChild(next);
        _world = next;
        _world.InteractionRequested += OnInteractionRequested;
        _world.PromptChanged += OnPromptChanged;
        _world.SetGamepadPrompts(_usingGamepad);
        previous?.QueueFree();
        RefreshHud();
    }

    private void DisconnectWorld()
    {
        if (_world is null)
        {
            return;
        }

        _world.InteractionRequested -= OnInteractionRequested;
        _world.PromptChanged -= OnPromptChanged;
    }

    private void OnInteractionRequested(string targetId) => _ = HandleInteractionAsync(targetId);

    private async Task HandleInteractionAsync(string targetId)
    {
        if (_flowRunning || _battle.Visible || _pauseScreen is not null || _journal.Visible)
        {
            return;
        }

        switch (targetId)
        {
            case "exit_house":
                LoadWorld("station_quarter", new Vector2(92, 255));
                if (!_session.State.World.HasFact("slice.promise.rail_notice"))
                {
                    await RunReunionAsync();
                }
                break;
            case "enter_house":
                LoadWorld("old_house", new Vector2(320, 260));
                break;
            case "bed":
                await AskToSleepAsync();
                break;
            case "desk":
                await InspectDeskAsync();
                break;
            case "railway_notice":
                await RunRailwayNoticeAsync();
                break;
            case "old_rail":
                await RunAnomalyAsync();
                break;
            default:
                if (targetId.StartsWith("npc:", StringComparison.Ordinal))
                {
                    await TalkToNpcAsync(targetId[4..]);
                }
                break;
        }
    }

    private async Task RunWakeupAsync()
    {
        if (_session.State.World.HasFact("slice.d1.awake"))
        {
            return;
        }

        await RunDialogueFlowAsync(async () =>
        {
            await Line("EVT_D1_WAKEUP_001", "旁白", "蝉声从旧宅的木窗缝里钻进来。暑假的第一天，屋里只剩你一个人。");
            await Line("EVT_D1_WAKEUP_002", "旁白", "山脚传来一声很轻的列车汽笛。那条停用多年的支线，本不该有车经过。");
            var choice = await Choice("EVT_D1_WAKEUP_CHOICE", "你先做了什么？",
                "拉开窗帘，确认声音的方向。",
                "再安静地听一会儿。");
            var observation = choice == 0 ? "slice.d1.saw_dark_signal" : "slice.d1.heard_mountain_echo";
            await Line(choice == 0 ? "EVT_D1_WAKEUP_003" : "EVT_D1_WAKEUP_004", "旁白",
                choice == 0
                    ? "雾贴在北山腰上，废线的信号灯却像刚熄灭一样，留着一点暗红。"
                    : "汽笛没有再响。取而代之的是某种低沉的回音，仿佛从山腹传来。");
            await Line("EVT_D1_WAKEUP_005", "旁白", "餐桌上压着家人的便笺：冰箱里有早饭，傍晚记得锁好院门。");
            await Line("EVT_D1_WAKEUP_006", _session.PlayerName, "先去镇上看看吧。好几年没回来，总得跟大家打声招呼。");
            Apply("slice.d1.wakeup.outcome", M("add_fact", observation), M("add_fact", "slice.d1.awake"),
                M("timeline", "TIMELINE_D1_WOKE_AT_OLD_HOUSE"), M("complete_event", "slice.d1.wakeup"));
        });
    }

    private async Task RunReunionAsync()
    {
        if (_session.State.World.HasFact("slice.promise.rail_notice"))
        {
            return;
        }

        await RunDialogueFlowAsync(async () =>
        {
            if (!_session.State.World.HasFact("slice.d1.reunion_complete"))
            {
                await Line("EVT_D1_REUNION_001", "旁白", "车站广场比记忆里空旷。旧候车室锁着门，屋檐下却站着三个熟悉的人。");
                await Line("EVT_D1_REUNION_002", "周明", "我就说你今天会出来。欢迎回来，民俗学者。");
                await Line("EVT_D1_REUNION_003", "沈砚", "他从八点半就开始等，还不许我发消息催你。");
                await Line("EVT_D1_REUNION_004", "林晓雨", "……好久不见。");
                await Line("EVT_D1_REUNION_005", "旁白", "她的语气很平静。你们之间那场没有说清的争执，也安静地横在这句问候里。");
                var greeting = await Choice("EVT_D1_REUNION_CHOICE", "你怎样回应？",
                    "先为久等道歉，再认真和每个人打招呼。",
                    "用以前的语气开个玩笑，缓和尴尬。");
                var greetingMutations = greeting == 0
                    ? new[]
                    {
                        M("change_trust", CharacterIds.AbilityHolder, CharacterIds.Protagonist, "2"),
                        M("change_affinity", CharacterIds.KnowledgeFriend, CharacterIds.Protagonist, "2"),
                        M("change_affinity", CharacterIds.TokenHolder, CharacterIds.Protagonist, "2")
                    }
                    : new[]
                    {
                        M("change_affinity", CharacterIds.AbilityHolder, CharacterIds.Protagonist, "2"),
                        M("change_affinity", CharacterIds.KnowledgeFriend, CharacterIds.Protagonist, "2"),
                        M("change_affinity", CharacterIds.TokenHolder, CharacterIds.Protagonist, "2")
                    };
                await Line(greeting == 0 ? "EVT_D1_REUNION_006" : "EVT_D1_REUNION_007", _session.PlayerName,
                    greeting == 0
                        ? "抱歉，让你们等了。周明、沈砚……晓雨，我回来了。"
                        : "三个人一起堵在车站，是准备收我返乡通行费吗？");
                Apply("slice.d1.reunion.greeting", greetingMutations);
                Apply("slice.d1.reunion.complete",
                    M("add_fact", "slice.d1.met_lin_xiaoyu"), M("add_fact", "slice.d1.met_zhou_ming"),
                    M("add_fact", "slice.d1.met_shen_yan"), M("add_fact", "slice.d1.reunion_complete"),
                    M("timeline", "TIMELINE_D1_FOUR_WAY_REUNION"), M("complete_event", "slice.d1.four_way_reunion"));
                _session.State.Investigation.KnownScheduleEntries.UnionWith(
                    [CharacterIds.AbilityHolder, CharacterIds.KnowledgeFriend, CharacterIds.TokenHolder]);
            }
            else
            {
                await Line("EVT_D1_REUNION_RESUME", "旁白", "三个人还在等你决定今天的去处。未完成的约定不会因为一次中断而消失。");
            }

            var activity = ExistingActivityIndex();
            if (activity < 0)
            {
                await Line("EVT_D1_REUNION_013", "旁白", "三个人都没有催促。午后去哪里，由你自己决定。");
                activity = await Choice("EVT_D1_ACTIVITY_PICK", "选择今天的共同活动：",
                    "去河岸陪晓雨录音。",
                    "去学校帮周明查旧档案。",
                    "去商店街陪沈砚检查怀表。");
                await PlayActivityAsync(activity);
            }

            if (!_session.State.World.HasFact("slice.promise.rail_notice"))
            {
                _session.State.World.AdvanceMinutes(Math.Max(0, 18 * 60 - _session.State.World.MinuteOfDay));
                await PlayPromiseAsync(activity);
            }

            _session.SaveCheckpoint();
        });
    }

    private async Task PlayActivityAsync(int activity)
    {
        if (activity == 0)
        {
            await Line("EVT_D1_XIAOYU_001", "旁白", "河滩的石头被晒得发白。林晓雨把录音机架在旧铁路桥的阴影里。");
            await Line("EVT_D1_XIAOYU_002", "林晓雨", "上周开始，风声里总夹着一个不属于这里的低音。");
            var response = await Choice("EVT_D1_XIAOYU_CHOICE", "你怎样回应她？",
                "承认自己是在担心她。",
                "尊重她的判断，帮忙检查录音设备。");
            Apply("slice.d1.activity_xiaoyu.outcome",
                response == 0
                    ? M("change_trust", CharacterIds.AbilityHolder, CharacterIds.Protagonist, "4")
                    : M("change_affinity", CharacterIds.AbilityHolder, CharacterIds.Protagonist, "5"),
                M("add_fact", "slice.d1.shared_activity_chosen"), M("add_fact", "slice.d1.activity_xiaoyu"),
                M("add_lead", "slice.lead.river_recording"), M("timeline", "TIMELINE_D1_RECORDED_RIVER_ECHO"),
                M("complete_event", "slice.d1.activity_xiaoyu"), M("grant_xp", "10"));
            await Line("EVT_D1_XIAOYU_010", "林晓雨", "不是回声。至少，不只是回声。");
            return;
        }

        if (activity == 1)
        {
            await Line("EVT_D1_ZHOU_001", "旁白", "学校旧档案室里浮着纸灰。周明把镇志、校史和几卷手绘地图排成一列。");
            await Line("EVT_D1_ZHOU_002", "周明", "官方地图把北山旧隧道画成了直线，但我祖父留下的版本在这里绕开了一段。");
            var response = await Choice("EVT_D1_ZHOU_CHOICE", "你从哪里着手？",
                "用民俗学的拓印方法恢复笔痕。",
                "先比对不同年代的线路走向。");
            Apply("slice.d1.activity_zhou.outcome",
                response == 0
                    ? M("add_evidence", "slice.evidence.erased_map_note")
                    : M("add_evidence", "slice.evidence.map_route_removed"),
                M("change_trust", CharacterIds.KnowledgeFriend, CharacterIds.Protagonist, "4"),
                M("add_fact", "slice.d1.shared_activity_chosen"), M("add_fact", "slice.d1.activity_zhou"),
                M("add_lead", "slice.lead.compare_old_rail_maps"), M("timeline", "TIMELINE_D1_STUDIED_OLD_MAP"),
                M("complete_event", "slice.d1.activity_zhou"), M("grant_xp", "10"));
            await Line("EVT_D1_ZHOU_008", "周明", "不是测绘误差。有人希望后来的人别走到那里。");
            return;
        }

        await Line("EVT_D1_SHEN_001", "旁白", "乔姨的杂货店后门敞着。沈砚坐在工作台前，掌心托着一只磨花的铁路怀表。");
        await Line("EVT_D1_SHEN_002", "沈砚", "我外公当扳道工时带着它。靠近北山就停，离远一点又自己走起来。");
        var shenResponse = await Choice("EVT_D1_SHEN_CHOICE", "你怎样检查怀表？",
            "逐件检查机械结构。",
            "带到街口，测试停止的方向。");
        Apply("slice.d1.activity_shen.outcome",
            shenResponse == 0
                ? M("change_trust", CharacterIds.TokenHolder, CharacterIds.Protagonist, "4")
                : M("change_affinity", CharacterIds.TokenHolder, CharacterIds.Protagonist, "5"),
            M("add_fact", "slice.d1.shared_activity_chosen"), M("add_fact", "slice.d1.activity_shen"),
            M("add_lead", "slice.lead.watch_points_north"), M("timeline", "TIMELINE_D1_TESTED_RAILWAY_WATCH"),
            M("complete_event", "slice.d1.activity_shen"), M("grant_xp", "10"));
        await Line("EVT_D1_SHEN_009", "乔姨", "你们两个别拆到忘了吃饭。车站明早要贴新的工程告示。");
    }

    private async Task PlayPromiseAsync(int activity)
    {
        var npcId = activity switch
        {
            0 => CharacterIds.AbilityHolder,
            1 => CharacterIds.KnowledgeFriend,
            _ => CharacterIds.TokenHolder
        };
        var name = WorldSceneController.CharacterName(npcId);
        await Line("EVT_D1_PROMISE_001", "旁白", $"住宅巷口，{name}提起车站明早会贴出的工程告示，并问你是否愿意一起确认。");
        var promiseChoice = await Choice("EVT_D1_PROMISE_CHOICE", $"{name}：明早一起去看吗？",
            "【承诺】明早在车站见，我一定到。",
            "【承诺】我会去；如果临时有事，我会先告诉你。");
        var trust = promiseChoice == 0 ? "3" : "2";
        var mutations = new List<NarrativeMutation>
        {
            M("create_promise", "promise.slice.rail_notice", CharacterIds.Protagonist, npcId, "明早一起查看车站工程告示"),
            M("change_trust", npcId, CharacterIds.Protagonist, trust),
            M("add_fact", "slice.promise.rail_notice"),
            M("timeline", "TIMELINE_D1_PROMISED_RAIL_NOTICE"),
            M("complete_event", "slice.d1.promise")
        };
        if (promiseChoice == 1)
        {
            mutations.Add(M("change_affinity", npcId, CharacterIds.Protagonist, "1"));
        }

        Apply("slice.d1.promise.outcome", mutations.ToArray());
        _session.SaveCheckpoint();
        await Line("EVT_D1_PROMISE_RECORDED", "旁白", "这项【承诺】已记入事实手册。它没有隐藏期限；如果无法履行，你可以主动解释。");
    }

    private async Task InspectDeskAsync()
    {
        await RunDialogueFlowAsync(async () =>
        {
            await Line("DESK_001", "旁白", "书桌抽屉里放着大学的田野笔记本。空白页很多，刚好够记下这个暑假的见闻。");
            Apply("slice.optional.desk", M("add_fact", "slice.d1.field_notebook_found"));
        });
    }

    private async Task TalkToNpcAsync(string npcId)
    {
        if (npcId != CharacterIds.Shopkeeper && !_session.State.World.HasFact("slice.promise.rail_notice"))
        {
            await RunReunionAsync();
            return;
        }

        _session.State.Investigation.KnownScheduleEntries.Add(npcId);
        await RunDialogueFlowAsync(async () =>
        {
            var name = WorldSceneController.CharacterName(npcId);
            var text = npcId switch
            {
                CharacterIds.Shopkeeper => "冰柜里还有你小时候常买的汽水。开发公司的人这两天也常来问旧隧道的路。",
                CharacterIds.AbilityHolder => "风向没变，可北山的回声比昨天更近。",
                CharacterIds.KnowledgeFriend => "有些被删掉的历史，不会因为纸上没有就真正消失。",
                CharacterIds.TokenHolder => "怀表只要朝向北山就慢下来，这可不像普通故障。",
                _ => "今天镇上还算平静。"
            };
            await Line($"CASUAL_{npcId}", name, text);
            if (npcId == CharacterIds.Shopkeeper)
            {
                Apply("slice.optional.met_aunt_qiao", M("add_fact", "slice.d1.met_aunt_qiao"),
                    M("change_affinity", CharacterIds.Shopkeeper, CharacterIds.Protagonist, "2"));
            }
        });
        _session.NotifyChanged();
    }

    private async Task AskToSleepAsync()
    {
        var shouldSleep = false;
        await RunDialogueFlowAsync(async () =>
        {
            var choice = await Choice("SLEEP_CHOICE", "现在休息到第二天早上吗？时间只会在睡眠后跨日，主线不会自动推进。",
                "睡到明天 08:00。",
                "暂时不睡。");
            shouldSleep = choice == 0;
        });
        if (!shouldSleep)
        {
            return;
        }

        _session.Sleep();
        LoadWorld("old_house", new Vector2(320, 250));
        ShowToast($"第 {_session.State.World.Day} 日。未完成的调查仍然可以继续。");
    }

    private async Task RunRailwayNoticeAsync()
    {
        if (_session.State.World.HasFact("slice.d2.railway_notice_read"))
        {
            await RunDialogueFlowAsync(() => Line("NOTICE_REPEAT", "旁白", "告示仍贴在原处。施工边界精确包住了被旧地图抹去的隧道区域。"));
            return;
        }

        if (!_session.State.World.HasFact("slice.promise.rail_notice"))
        {
            if (_session.State.World.HasFact("slice.d1.reunion_complete"))
            {
                await RunReunionAsync();
                return;
            }

            await RunDialogueFlowAsync(() => Line("NOTICE_LOCKED", "旁白", "这里只贴着旧时刻表。先和车站前的旧友们聊聊，也许有人知道新工程的消息。"));
            return;
        }

        if (_session.State.World.Day < 2)
        {
            await RunDialogueFlowAsync(() => Line("NOTICE_TOMORROW", "旁白", "新工程告示要到明早才会张贴。你已经把约定记在了手册里。"));
            return;
        }

        await RunDialogueFlowAsync(async () =>
        {
            var partner = ActivityPartner();
            await Line("EVT_D2_NOTICE_001", "旁白", "新告示盖住了旧时刻表。标题写着“北山旧铁路安全整修与遗迹勘察工程”。");
            await Line("EVT_D2_NOTICE_002", "旁白", "红色施工边界越过废弃支线，正好包住地图上被抹去的旧隧道。");
            await Line("EVT_D2_NOTICE_PARTNER", WorldSceneController.CharacterName(partner), "你来了。告示刚贴好，浆糊还没干。");
            var choice = await Choice("EVT_D2_NOTICE_CHOICE", "你优先记录哪一部分？",
                "对照告示与残存的旧线路图。",
                "记录公司、审批章和现场联络方式。");
            var evidence = choice == 0
                ? "slice.evidence.notice_route_mismatch"
                : "slice.evidence.notice_office_mismatch";
            Apply("slice.d2.notice.outcome", M("add_evidence", evidence),
                M("resolve_promise", "promise.slice.rail_notice", "fulfilled"),
                M("change_trust", partner, CharacterIds.Protagonist, "6"),
                M("add_fact", "slice.d2.railway_notice_read"), M("add_fact", "slice.d2.railway_repair_announced"),
                M("add_lead", "slice.lead.visit_abandoned_railway"), M("timeline", "TIMELINE_D2_READ_RAIL_NOTICE"),
                M("complete_event", "slice.d2.railway_notice"), M("grant_xp", "15"));
            await Line("EVT_D2_NOTICE_010", "旁白", "【承诺】已履行。你们决定沿公开步道走到废弃支线外缘，只确认声音来源，不进入施工封锁区。");
            _session.SaveCheckpoint();
        });
    }

    private async Task RunAnomalyAsync()
    {
        if (!_session.State.World.HasFact("slice.d2.railway_notice_read"))
        {
            await RunDialogueFlowAsync(() => Line("ANOMALY_LOCKED", "旁白", "通往隧道的步道被施工警示带拦住。先调查车站的工程告示，确认这里发生了什么。"));
            return;
        }

        if (_session.State.World.HasFact("slice.d2.first_anomaly_witnessed"))
        {
            await RunDialogueFlowAsync(() => Line("ANOMALY_DONE", "旁白", "枕木下的震动已经平静，但旧隧道更深处仍像有什么在缓慢呼吸。"));
            return;
        }

        var shouldStartBattle = false;
        var withdrewThisVisit = false;
        await RunDialogueFlowAsync(async () =>
        {
            if (!_session.State.World.HasFact("slice.d2.anomaly_engaged"))
            {
                await Line("EVT_D2_ANOMALY_001", "旁白", "废弃支线尽头，锈轨钻进齐腰高的夏草。空气里没有风，枕木下却传来规律的震动。");
                await Line("EVT_D2_ANOMALY_004", "旁白", "一层白霜沿铁轨迅速铺开。倒影里出现一座不存在的黑色山门。");
                await Line("EVT_D2_ANOMALY_005", "林晓雨", "它不是在说话……它只是在跟着我的声音呼吸。");
                var choice = await Choice("EVT_D2_ANOMALY_CHOICE", "你如何处理晓雨的能力与周围的视线？",
                    "【隐瞒】挡住坡上的视线，只做最短安抚。",
                    "【冒险】再维持片刻，确认共鸣指向哪里。",
                    "立即组织撤离，先确保所有人安全。");
                var mutations = new List<NarrativeMutation> { M("add_fact", "slice.d2.anomaly_engaged") };
                if (choice == 0)
                {
                    mutations.AddRange([
                        M("add_fact", "slice.d2.anomaly_concealed"),
                        M("add_evidence", "slice.evidence.resonance_frost_pattern"),
                        M("change_trust", CharacterIds.AbilityHolder, CharacterIds.Protagonist, "6")]);
                    await Line("EVT_D2_ANOMALY_008", "林晓雨", "好。数到三，我会收声。");
                }
                else if (choice == 1)
                {
                    mutations.AddRange([
                        M("add_fact", "slice.d2.anomaly_traced"),
                        M("add_fact", "slice.d2.ability_seen_from_slope"),
                        M("add_evidence", "slice.evidence.resonance_directional_echo"),
                        M("add_lead", "slice.lead.old_tunnel_below"),
                        M("raise_exposure", "1"),
                        M("change_trust", CharacterIds.AbilityHolder, CharacterIds.Protagonist, "4")]);
                    await Line("EVT_D2_ANOMALY_011", "旁白", "黑色山门完全显现了一瞬。坡上的树影随即向后缩去——有人看见了这一幕。");
                }
                else
                {
                    withdrewThisVisit = true;
                    mutations.AddRange([
                        M("add_fact", "slice.d2.anomaly_withdrawal"),
                        M("add_lead", "slice.lead.return_to_old_railway_prepared"),
                        M("change_trust", CharacterIds.AbilityHolder, CharacterIds.Protagonist, "3")]);
                    await Line("EVT_D2_ANOMALY_013", "林晓雨", "谢谢。它没有恶意，可我不知道再唱下去会发生什么。");
                }

                Apply($"slice.d2.anomaly.choice.{choice}", mutations.ToArray());
            }
            else
            {
                await Line("ANOMALY_RETURN", "旁白", "你们回到记录的位置。铁轨的节拍仍在，先前的判断和暴露风险都没有被时间抹去。");
            }

            if (withdrewThisVisit)
            {
                await Line("ANOMALY_WITHDRAWAL_RESULT", "旁白", "你们沿原路退回安全地带，把共鸣位置记进手册。准备好后，可以再主动返回这里。");
                _session.SaveCheckpoint();
                return;
            }

            await Line("ANOMALY_BATTLE_WARNING", "周明", "共鸣正在封住退路。先观察节拍、保护同伴，再安抚或打断它。我们不需要伤害任何人。");
            shouldStartBattle = true;
        });

        if (shouldStartBattle)
        {
            _session.SaveCheckpoint();
            OpenBattle();
        }
    }

    private void OpenBattle()
    {
        _session.ActiveBattle = new ResonanceBattle();
        _battleLease?.Dispose();
        _battleLease = _session.Modal.Acquire("battle");
        _battleEnding = false;
        _battle.Open(_session.ActiveBattle);
    }

    private void OnBattleAction(BattleAction action)
    {
        if (_battleEnding || _session.ActiveBattle is null)
        {
            return;
        }

        var result = _session.ActiveBattle.Act(
            action,
            hasAbilityHolderSupport: true,
            storyMode: _session.State.Settings.StoryMode);
        _battle.ApplyResult(result);
        if (result.Won)
        {
            _ = CompleteBattleAsync();
        }
        else if (result.Retreated)
        {
            _ = RetreatFromBattleAsync();
        }
    }

    private void RetryBattle()
    {
        if (_battleEnding)
        {
            return;
        }

        _session.ActiveBattle = new ResonanceBattle();
        _battle.Open(_session.ActiveBattle);
    }

    private async Task CompleteBattleAsync()
    {
        if (_battleEnding)
        {
            return;
        }

        _battleEnding = true;
        await ToSignal(GetTree().CreateTimer(0.7), SceneTreeTimer.SignalName.Timeout);
        _battle.ClosePanel();
        _battleLease?.Dispose();
        _battleLease = null;
        _session.ActiveBattle = null;
        Apply("slice.d2.anomaly.victory",
            M("add_fact", "slice.d2.first_anomaly_witnessed"), M("add_fact", "slice.d2.first_anomaly_resolved"),
            M("timeline", "TIMELINE_D2_WITNESSED_FIRST_ANOMALY"), M("complete_event", "slice.d2.first_anomaly"),
            M("grant_xp", "20"));
        await RunDialogueFlowAsync(async () =>
        {
            await Line("EVT_D2_ANOMALY_014", "周明", "今天能确认三件事：工程范围有问题，旧隧道下面有东西，晓雨能暂时让它平静。");
            await Line("EVT_D2_ANOMALY_015", "沈砚", "第四件事——我们别再让任何人单独来。");
            await Line("EVT_D2_ANOMALY_016", "林晓雨", "这次，我同意。");
        });
        _session.SaveCheckpoint();
        ShowSliceComplete();
    }

    private async Task RetreatFromBattleAsync()
    {
        if (_battleEnding)
        {
            return;
        }

        _battleEnding = true;
        await ToSignal(GetTree().CreateTimer(0.8), SceneTreeTimer.SignalName.Timeout);
        _battle.ClosePanel();
        _battleLease?.Dispose();
        _battleLease = null;
        _session.ActiveBattle = null;
        LoadWorld("station_quarter", new Vector2(500, 130));
        ShowToast("已撤退到安全地点。准备好后仍可重试，剧情不会锁死。");
    }

    private void ShowSliceComplete()
    {
        CloseCompletion();
        _completionScreen = NewScreen("SliceComplete");
        _completionLease = _session.Modal.Acquire("slice-complete");
        var shade = new ColorRect { Color = new Color("07101bdf") };
        shade.SetAnchorsAndOffsetsPreset(Control.LayoutPreset.FullRect);
        _completionScreen.AddChild(shade);
        var panel = new PanelContainer { Position = new Vector2(135, 73), Size = new Vector2(370, 214) };
        panel.AddThemeStyleboxOverride("panel", UiStyles.Panel(new Color("1c2b3af2"), 10));
        _completionScreen.AddChild(panel);
        var column = new VBoxContainer();
        column.AddThemeConstantOverride("separation", 9);
        panel.AddChild(column);
        var title = new Label { Text = "垂直切片完成" };
        title.AddThemeFontSizeOverride("font_size", 25);
        title.AddThemeColorOverride("font_color", new Color("f2c66d"));
        column.AddChild(title);
        column.AddChild(new Label
        {
            Text = "你建立的关系、兑现的承诺，以及是否让能力暴露，都已写入同一份世界状态。接下来的谨慎/救援分歧将在正式内容阶段读取这些结果。",
            AutowrapMode = TextServer.AutowrapMode.WordSmart,
            CustomMinimumSize = new Vector2(330, 82)
        });
        var keepExploring = UiStyles.Button("继续在镇上查看手册");
        keepExploring.Pressed += () =>
        {
            CloseCompletion();
        };
        column.AddChild(keepExploring);
        var titleButton = UiStyles.Button("返回标题");
        titleButton.Pressed += () =>
        {
            CloseCompletion();
            ShowTitle();
        };
        column.AddChild(titleButton);
        keepExploring.GrabFocus();
    }

    private void OpenJournal()
    {
        _journalLease = _session.Modal.Acquire("journal");
        _journal.Open(_session, _dialogue.HistoryText);
    }

    private void CloseJournal()
    {
        _journal.ClosePanel();
        _journalLease?.Dispose();
        _journalLease = null;
    }

    private void CloseCompletion()
    {
        _completionScreen?.QueueFree();
        _completionScreen = null;
        _completionLease?.Dispose();
        _completionLease = null;
    }

    private void OpenPause()
    {
        _pauseLease = _session.Modal.Acquire("pause");
        _pauseScreen = NewScreen("PauseMenu");
        var shade = new ColorRect { Color = new Color("000000a8") };
        shade.SetAnchorsAndOffsetsPreset(Control.LayoutPreset.FullRect);
        _pauseScreen.AddChild(shade);
        var panel = new PanelContainer { Position = new Vector2(200, 34), Size = new Vector2(240, 292) };
        panel.AddThemeStyleboxOverride("panel", UiStyles.Panel(new Color("1c2b3af2"), 10));
        _pauseScreen.AddChild(panel);
        var column = new VBoxContainer();
        column.AddThemeConstantOverride("separation", 7);
        panel.AddChild(column);
        var title = new Label { Text = "暂停" };
        title.AddThemeFontSizeOverride("font_size", 23);
        title.AddThemeColorOverride("font_color", new Color("f2c66d"));
        column.AddChild(title);
        var resume = UiStyles.Button("继续");
        resume.Pressed += ClosePause;
        column.AddChild(resume);
        var save = UiStyles.Button("保存");
        save.Pressed += () =>
        {
            ClosePause();
            try
            {
                _session.Save();
                ShowToast("已保存。对话和战斗中不能手动保存。");
            }
            catch (Exception exception)
            {
                ShowToast($"保存失败：{exception.Message}");
            }
        };
        column.AddChild(save);
        var load = UiStyles.Button("载入");
        load.Pressed += () =>
        {
            ClosePause();
            if (_session.TryLoad(out var error))
            {
                ApplySettingsVisuals();
                LoadWorld(_session.State.Player.LocationId);
                ShowToast("存档已载入。");
            }
            else
            {
                ShowToast(error);
            }
        };
        column.AddChild(load);
        var settings = UiStyles.Button("显示与辅助设置");
        settings.Pressed += OpenSettings;
        column.AddChild(settings);
        var unstick = UiStyles.Button("脱离卡住：回安全点");
        unstick.Pressed += () =>
        {
            ClosePause();
            _world?.MovePlayerToSafePoint();
            ShowToast("已回到当前区域的安全点。");
        };
        column.AddChild(unstick);
        var titleButton = UiStyles.Button("返回标题");
        titleButton.Pressed += () =>
        {
            ClosePause();
            ShowTitle();
        };
        column.AddChild(titleButton);
        resume.GrabFocus();
    }

    private void OpenSettings()
    {
        _pauseScreen?.QueueFree();
        _pauseScreen = NewScreen("SettingsMenu");
        var shade = new ColorRect { Color = new Color("000000b8") };
        shade.SetAnchorsAndOffsetsPreset(Control.LayoutPreset.FullRect);
        _pauseScreen.AddChild(shade);
        var panel = new PanelContainer { Position = new Vector2(90, 8), Size = new Vector2(460, 344) };
        panel.AddThemeStyleboxOverride("panel", UiStyles.Panel(new Color("1c2b3af2"), 10));
        _pauseScreen.AddChild(panel);
        var column = new VBoxContainer();
        column.AddThemeConstantOverride("separation", 2);
        panel.AddChild(column);
        var title = new Label { Text = "显示与辅助设置" };
        title.AddThemeFontSizeOverride("font_size", 21);
        title.AddThemeColorOverride("font_color", new Color("f2c66d"));
        column.AddChild(title);

        var fontScale = AddOptionRow(column, "字号", ["100%", "125%", "150%"]);
        fontScale.Selected = _session.State.Settings.FontScale switch { >= 1.5f => 2, >= 1.25f => 1, _ => 0 };
        fontScale.ItemSelected += index =>
        {
            _session.State.Settings.FontScale = index switch { 2 => 1.5f, 1 => 1.25f, _ => 1f };
            ApplySettingsVisuals();
        };

        var textSpeed = AddOptionRow(column, "自动播放速度", ["舒缓", "标准", "快速"]);
        textSpeed.Selected = _session.State.Settings.TextSpeed switch { >= 1.5f => 2, <= 0.75f => 0, _ => 1 };
        textSpeed.ItemSelected += index =>
            _session.State.Settings.TextSpeed = index switch { 2 => 1.5f, 0 => 0.75f, _ => 1f };

        var story = AddToggle(column, "故事模式（战斗不损失意志）", _session.State.Settings.StoryMode);
        story.Toggled += enabled => _session.State.Settings.StoryMode = enabled;
        var contrast = AddToggle(column, "高对比交互提示", _session.State.Settings.HighContrastPrompts);
        contrast.Toggled += enabled =>
        {
            _session.State.Settings.HighContrastPrompts = enabled;
            ApplySettingsVisuals();
        };
        var flashing = AddToggle(column, "减少闪烁（本切片默认无闪烁）", _session.State.Settings.ReduceFlashing);
        flashing.Toggled += enabled => _session.State.Settings.ReduceFlashing = enabled;
        var vibration = AddToggle(column, "手柄震动", _session.State.Settings.VibrationEnabled);
        vibration.Toggled += enabled => _session.State.Settings.VibrationEnabled = enabled;
        var fullscreen = AddToggle(column, "无边框全屏", _session.State.Settings.BorderlessFullscreen);
        fullscreen.Toggled += enabled =>
        {
            _session.State.Settings.BorderlessFullscreen = enabled;
            DisplayServer.WindowSetMode(enabled
                ? DisplayServer.WindowMode.Fullscreen
                : DisplayServer.WindowMode.Windowed);
        };
        var note = new Label
        {
            Text = "键盘同时支持 WASD 与方向键；完整按键重映射将在引擎验收后接入。",
            AutowrapMode = TextServer.AutowrapMode.WordSmart
        };
        note.AddThemeFontSizeOverride("font_size", 10);
        note.AddThemeColorOverride("font_color", new Color("b9d1c5"));
        column.AddChild(note);
        var back = UiStyles.Button("返回暂停菜单");
        back.Pressed += () =>
        {
            ClosePause();
            OpenPause();
        };
        column.AddChild(back);
        fontScale.GrabFocus();
    }

    private static OptionButton AddOptionRow(VBoxContainer column, string labelText, IReadOnlyList<string> items)
    {
        var row = new HBoxContainer();
        column.AddChild(row);
        var label = new Label { Text = labelText, SizeFlagsHorizontal = Control.SizeFlags.ExpandFill };
        row.AddChild(label);
        var option = new OptionButton { CustomMinimumSize = new Vector2(120, 28) };
        foreach (var item in items)
        {
            option.AddItem(item);
        }
        row.AddChild(option);
        return option;
    }

    private static CheckButton AddToggle(VBoxContainer column, string label, bool enabled)
    {
        var toggle = new CheckButton { Text = label, ButtonPressed = enabled };
        column.AddChild(toggle);
        return toggle;
    }

    private void ApplySettingsVisuals()
    {
        if (_uiRoot?.Theme is null)
        {
            return;
        }

        _uiRoot.Theme.DefaultFontSize = Mathf.RoundToInt(14 * _session.State.Settings.FontScale);
        _promptLabel?.AddThemeStyleboxOverride("normal", UiStyles.Panel(
            _session.State.Settings.HighContrastPrompts ? new Color("000000ff") : new Color("1a2638e5"), 5));
        DisplayServer.WindowSetMode(_session.State.Settings.BorderlessFullscreen
            ? DisplayServer.WindowMode.Fullscreen
            : DisplayServer.WindowMode.Windowed);
    }

    private void ClosePause()
    {
        _pauseScreen?.QueueFree();
        _pauseScreen = null;
        _pauseLease?.Dispose();
        _pauseLease = null;
    }

    private async Task RunDialogueFlowAsync(Func<Task> body)
    {
        if (_flowRunning)
        {
            return;
        }

        _flowRunning = true;
        using var lease = _session.Modal.Acquire($"dialogue:{Guid.NewGuid():N}");
        try
        {
            await body();
        }
        catch (TaskCanceledException)
        {
            // A scene shutdown can cancel a pending dialogue without committing a partial outcome.
        }
        catch (Exception exception)
        {
            GD.PushError(exception.ToString());
            ShowToast($"事件未提交，可重新交互：{exception.Message}");
        }
        finally
        {
            _dialogue.CloseDialogue();
            _flowRunning = false;
            RefreshHud();
        }
    }

    private Task Line(string id, string speaker, string text) => _dialogue.ShowLineAsync(id, speaker, text);

    private Task<int> Choice(string id, string text, params string[] choices) =>
        _dialogue.ShowChoiceAsync(id, _session.PlayerName, text, choices);

    private void Apply(string outcomeId, params NarrativeMutation[] mutations)
    {
        _session.Outcomes.Apply(outcomeId, mutations);
        _session.NotifyChanged();
    }

    private static NarrativeMutation M(string command, params string[] arguments) => new()
    {
        Command = command,
        Arguments = arguments.ToList()
    };

    private int ExistingActivityIndex()
    {
        if (_session.State.World.HasFact("slice.d1.activity_xiaoyu"))
        {
            return 0;
        }

        if (_session.State.World.HasFact("slice.d1.activity_zhou"))
        {
            return 1;
        }

        return _session.State.World.HasFact("slice.d1.activity_shen") ? 2 : -1;
    }

    private string ActivityPartner()
    {
        if (_session.State.World.HasFact("slice.d1.activity_xiaoyu"))
        {
            return CharacterIds.AbilityHolder;
        }

        return _session.State.World.HasFact("slice.d1.activity_zhou")
            ? CharacterIds.KnowledgeFriend
            : CharacterIds.TokenHolder;
    }

    private void OnPromptChanged(string prompt)
    {
        _promptLabel.Text = prompt;
        _promptLabel.Visible = !string.IsNullOrEmpty(prompt);
    }

    private void RefreshHud()
    {
        if (_timeLabel is null)
        {
            return;
        }

        var world = _session.State.World;
        _timeLabel.Text = $"第 {world.Day} 日　{world.MinuteOfDay / 60:00}:{world.MinuteOfDay % 60:00}";
        _objectiveLabel.Text = ObjectiveText();
        _debugLabel.Text =
            $"[F3] 调试状态\n位置: {_session.State.Player.LocationId}\n事实: {world.Facts.Count}  证据: {world.Evidence.Count}\n暴露: {world.Exposure}  等级: {_session.State.Progression.Level}";
    }

    private string ObjectiveText()
    {
        var facts = _session.State.World.Facts;
        if (!facts.Contains("slice.d1.awake")) return "目标：开始这个暑假";
        if (!facts.Contains("slice.d1.reunion_complete")) return "目标：去车站广场见旧友";
        if (!facts.Contains("slice.promise.rail_notice")) return "目标：选择共同活动并作出承诺";
        if (_session.State.World.Day < 2) return "目标：回旧宅睡到第二天";
        if (!facts.Contains("slice.d2.railway_notice_read")) return "目标：阅读车站工程告示";
        if (!facts.Contains("slice.d2.first_anomaly_witnessed")) return "目标：调查右上方废弃隧道";
        return "切片完成：查看手册中的因果记录";
    }

    private void UpdateInputHint()
    {
        if (_inputHint is null)
        {
            return;
        }

        _inputHint.Text = _usingGamepad
            ? "左摇杆 移动　A 互动　Y 手册　Menu 暂停"
            : "WASD / 方向键 移动　E / 空格 互动　J 手册　Esc 暂停";
        _world?.SetGamepadPrompts(_usingGamepad);
        _journal?.SetGamepadPrompts(_usingGamepad);
    }

    private void ShowToast(string message)
    {
        if (_toast is null)
        {
            return;
        }

        var version = ++_toastVersion;
        _toast.Text = message;
        _toast.Visible = true;
        _ = HideToastLaterAsync(version);
    }

    private async Task HideToastLaterAsync(int version)
    {
        await ToSignal(GetTree().CreateTimer(2.6), SceneTreeTimer.SignalName.Timeout);
        if (version == _toastVersion && GodotObject.IsInstanceValid(_toast))
        {
            _toast.Visible = false;
        }
    }
}
