using Godot;
using TownRpg.Application;
using TownRpg.Core.Domain;
using TownRpg.Core.Services;

namespace TownRpg.Presentation;

public partial class DreamEscapeController : Node
{
    public event Action? PinWakeRequested;
    public event Action? ExitOpened;

    private GameSession _session = null!;
    private Node _areaHost = null!;
    private Node? _areaNode;
    private IDreamAreaRuntime? _area;
    private CanvasLayer _interface = null!;
    private Control _interfaceRoot = null!;
    private Label _areaLabel = null!;
    private Label _prompt = null!;
    private Label _toast = null!;
    private TextureRect _clueCloseup = null!;
    private DreamPuzzlePanel _puzzle = null!;
    private DreamMechanismPanel _mechanism = null!;
    private DreamInventoryPanel _inventory = null!;
    private ConfirmationDialog _pinConfirmation = null!;
    private AcceptDialog _controlReminder = null!;
    private ColorRect _transitionFade = null!;
    private AudioStreamPlayer _masterTapeNoise = null!;
    private bool _usingGamepad;
    private bool _areaTransitionRunning;
    private bool _xiaoyuHoldingExitLever;
    private string? _previewPuzzleId;
    private IReadOnlyList<string>? _previewValues;
    private readonly CancellationTokenSource _lifetimeCancellation = new();
    private int _toastVersion;

    public bool InventoryVisible => _inventory?.Visible == true;
    public bool ControlReminderVisible => _controlReminder?.Visible == true;
    public bool UsingGamepadPrompts => _usingGamepad;
    public bool XiaoyuHoldingExitLever => _xiaoyuHoldingExitLever;

    public override void _Ready()
    {
        _session = GetNode<GameSession>("/root/GameSession");
        if (_session.State.DreamEscape is null)
        {
            throw new InvalidOperationException("DreamEscape scene requires a dream escape state.");
        }
        _session.State.Player.LocationId = DreamEscapeIds.Location;
        _areaHost = new Node { Name = "AreaHost" };
        AddChild(_areaHost);
        _masterTapeNoise = new AudioStreamPlayer
        {
            Name = "MasterTapeWhiteNoise",
            Stream = ResourceLoader.Load<AudioStream>(
                "res://assets/generated/dream_escape/audio/auditorium-master-white-noise.wav"),
            VolumeDb = -12f
        };
        _masterTapeNoise.Finished += OnMasterTapeNoiseFinished;
        AddChild(_masterTapeNoise);
        BuildInterface();
        LoadArea(_session.State.DreamEscape.CurrentAreaId);
        RefreshHud();
    }

    public override void _ExitTree()
    {
        _lifetimeCancellation.Cancel();
        _lifetimeCancellation.Dispose();
        if (_masterTapeNoise is not null)
        _masterTapeNoise.Finished -= OnMasterTapeNoiseFinished;
        if (_puzzle is not null)
        {
            _puzzle.Submitted -= SubmitPuzzle;
            _puzzle.PreviewChanged -= OnPuzzlePreviewChanged;
            _puzzle.Closed -= OnPuzzlePreviewClosed;
        }
        if (_mechanism is not null)
        {
            _mechanism.Submitted -= SubmitPuzzle;
            _mechanism.PreviewChanged -= OnPuzzlePreviewChanged;
            _mechanism.Closed -= OnPuzzlePreviewClosed;
        }
        _pinLease?.Dispose();
        _pinLease = null;
        _controlReminderLease?.Dispose();
        _controlReminderLease = null;
        DisconnectArea();
    }

    public override void _Input(InputEvent inputEvent)
    {
        var nextGamepad = inputEvent is InputEventJoypadButton button && button.Pressed ||
                          inputEvent is InputEventJoypadMotion motion && Math.Abs(motion.AxisValue) >= 0.35f;
        var nextKeyboard = inputEvent is InputEventKey key && key.Pressed ||
                           inputEvent is InputEventMouseButton mouse && mouse.Pressed ||
                           inputEvent is InputEventMouseMotion mouseMotion &&
                           mouseMotion.ScreenRelative.LengthSquared() > 0.01f;
        if ((nextGamepad && !_usingGamepad) || (nextKeyboard && _usingGamepad))
        {
            _usingGamepad = nextGamepad;
            _area?.SetGamepadPrompts(_usingGamepad);
            RefreshHud();
        }

        if (inputEvent.IsActionPressed("inventory") &&
            !_puzzle.Visible &&
            !_mechanism.Visible &&
            !_pinConfirmation.Visible &&
            (_inventory.Visible || (!_session.Modal.IsBlocked && !_areaTransitionRunning)))
        {
            GetViewport().SetInputAsHandled();
            if (_inventory.Visible) _inventory.ClosePanel();
            else _inventory.Open();
        }
    }

    private void BuildInterface()
    {
        _interface = new CanvasLayer { Layer = 12 };
        AddChild(_interface);
        _interfaceRoot = new Control
        {
            Theme = UiStyles.CreateTheme(),
            MouseFilter = Control.MouseFilterEnum.Ignore
        };
        _interfaceRoot.Theme.DefaultFontSize = Mathf.RoundToInt(14 * _session.State.Settings.FontScale);
        _interfaceRoot.SetAnchorsAndOffsetsPreset(Control.LayoutPreset.FullRect);
        _interface.AddChild(_interfaceRoot);

        var areaBadge = new PanelContainer
        {
            Name = "AreaBadge",
            Position = new Vector2(10, 8),
            CustomMinimumSize = new Vector2(0, 30)
        };
        areaBadge.MouseBehaviorRecursive = Control.MouseBehaviorRecursiveEnum.Disabled;
        areaBadge.AddThemeStyleboxOverride("panel", UiStyles.Panel(new Color("122431e8"), 6));
        _interfaceRoot.AddChild(areaBadge);
        var areaMargin = new MarginContainer();
        areaMargin.AddThemeConstantOverride("margin_left", 10);
        areaMargin.AddThemeConstantOverride("margin_right", 10);
        areaMargin.AddThemeConstantOverride("margin_top", 4);
        areaMargin.AddThemeConstantOverride("margin_bottom", 4);
        areaBadge.AddChild(areaMargin);
        _areaLabel = new Label { Name = "AreaName" };
        _areaLabel.AddThemeColorOverride("font_color", new Color("f2c66d"));
        areaMargin.AddChild(_areaLabel);
        _prompt = new Label
        {
            Position = new Vector2(90, 318),
            Size = new Vector2(460, 30),
            HorizontalAlignment = HorizontalAlignment.Center,
            VerticalAlignment = VerticalAlignment.Center,
            MouseFilter = Control.MouseFilterEnum.Ignore,
            Visible = false
        };
        _prompt.AddThemeStyleboxOverride("normal", UiStyles.Panel(
            _session.State.Settings.HighContrastPrompts ? new Color("000000ff") : new Color("152536ed"), 5));
        _prompt.AddThemeColorOverride("font_color", new Color("f5e6bd"));
        _interfaceRoot.AddChild(_prompt);
        _toast = new Label
        {
            Position = new Vector2(105, 44),
            Size = new Vector2(430, 54),
            HorizontalAlignment = HorizontalAlignment.Center,
            VerticalAlignment = VerticalAlignment.Center,
            AutowrapMode = TextServer.AutowrapMode.WordSmart,
            MouseFilter = Control.MouseFilterEnum.Ignore,
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
        _puzzle.PreviewChanged += OnPuzzlePreviewChanged;
        _puzzle.Closed += OnPuzzlePreviewClosed;
        _interfaceRoot.AddChild(_puzzle);
        _mechanism = new DreamMechanismPanel { Name = "MechanismPanel" };
        _mechanism.Submitted += SubmitPuzzle;
        _mechanism.PreviewChanged += OnPuzzlePreviewChanged;
        _mechanism.Closed += OnPuzzlePreviewClosed;
        _interfaceRoot.AddChild(_mechanism);
        _inventory = new DreamInventoryPanel { Name = "InventoryPanel" };
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

        _controlReminder = new AcceptDialog
        {
            Name = "EntryControlReminder",
            Title = "操作提示",
            DialogText = "Esc：暂停游戏，可进入设置或退出。\nI 或 Tab：打开随身物品与记录。\n\nXbox 手柄：Menu 暂停，X 打开随身物品与记录。",
            OkButtonText = "开始探索",
            Exclusive = true
        };
        _controlReminder.Confirmed += ReleaseControlReminder;
        _controlReminder.Canceled += ReleaseControlReminder;
        _interfaceRoot.AddChild(_controlReminder);

        _transitionFade = new ColorRect
        {
            Color = Colors.Black,
            Modulate = new Color(1f, 1f, 1f, 0f),
            MouseFilter = Control.MouseFilterEnum.Stop,
            Visible = false
        };
        _transitionFade.SetAnchorsAndOffsetsPreset(Control.LayoutPreset.FullRect);
        _interfaceRoot.AddChild(_transitionFade);
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
        _xiaoyuHoldingExitLever = false;
        _previewPuzzleId = null;
        _previewValues = null;
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
        var nextNode = packed.Instantiate();
        var next = nextNode as IDreamAreaRuntime
            ?? throw new InvalidOperationException($"Dream area scene '{path}' does not implement IDreamAreaRuntime.");
        var previous = _areaNode;
        DisconnectArea();
        _areaHost.AddChild(nextNode);
        _areaNode = nextNode;
        _area = next;
        next.InteractionRequested += HandleInteraction;
        next.PromptChanged += SetPrompt;
        next.SetGamepadPrompts(_usingGamepad);
        PushTransientState();
        previous?.QueueFree();
        RefreshHud();
    }

    private void DisconnectArea()
    {
        if (_area is null) return;
        _area.InteractionRequested -= HandleInteraction;
        _area.PromptChanged -= SetPrompt;
        _area = null;
        _areaNode = null;
    }

    private void HandleInteraction(string targetId)
    {
        if (_session.Modal.IsBlocked) return;
        var discoveredPuzzle = PuzzleForTarget(targetId);
        if (discoveredPuzzle is not null)
            _session.DiscoverDreamPuzzle(discoveredPuzzle);
        if (targetId.StartsWith("area:", StringComparison.Ordinal))
        {
            _ = TransitionToAreaAsync(targetId);
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
                "target.classroom.maintenance_card" => "维护卡画着三幅磨损示意：从左到右是上段、下段、中段。记录页已保存。",
                "target.classroom.morning_schedule" => "讲台上的晨间作息表在时钟图标旁写着 7:00。",
                "target.classroom.duty_note" => "后排便签用红笔圈出了“值日表第 2 项”。",
                "target.music.tempo_notice" => "入口排练通知圈出了 72 BPM。",
                "target.music.fork_reference" => "器材间对照表把衰减和波形对应到 A3、C4、E4、A4。",
                "target.music.roll_template" => "纸带柜裂口模板显示四段按 2—4—1—3 接合。",
                "target.archive.registry_rule" => "登记墙要求先排上午、再排下午；同一时段先北侧、再南侧。",
                "target.archive.registry_choir" => "合唱档案盖着“上午·北侧”的双重印章。",
                "target.archive.registry_broadcast" => "广播档案盖着“上午·南侧”的双重印章。",
                "target.archive.registry_art" => "美术档案盖着“下午·北侧”的双重印章。",
                "target.archive.registry_track" => "田径档案盖着“下午·南侧”的双重印章。",
                "target.archive.fuse_route" => "线路图标明保险丝桥接顺序：左—中—右。",
                "target.auditorium.line_set_notches" => "磨损尺显示 A、B、C、D 四组配重档位依次为 3、1、4、2。",
                "target.auditorium.projection_order" => "升起的幕布后留下三联轮廓：窗格—人物—校铃。",
                "target.auditorium.tape_order" => "登记卡注明母带顺序：教室—音乐—档案。",
                _ => string.Empty
            };
            ApplyAndRefresh(result, result.Changed ? special : null);
            return;
        }
        if (targetId == "target.classroom.clock")
        {
            UseAvailableItems(targetId, showResult: false);
            var state = _session.State.DreamEscape!;
            if (!state.InstalledParts.Contains(DreamEscapeIds.SlotClassroomClockHand) ||
                !state.InstalledParts.Contains(DreamEscapeIds.SlotClassroomClockKey))
            {
                ShowToast("时钟还缺少时针或上弦钥匙。找到部件后再次检查，背包中的匹配部件会自动安装。");
                return;
            }
            _mechanism.Open(DreamEscapeIds.PuzzleClockRestore, DreamMechanismMode.Clock);
            return;
        }
        if (targetId == "target.classroom.duty_roster")
        {
            UseAvailableItems(targetId, showResult: false);
            if (!_session.State.DreamEscape!.InstalledParts.Contains(DreamEscapeIds.SlotClassroomRosterSlider))
            {
                ShowToast("值日表缺少一层透明滑片。找到后再次检查即可自动安装。");
                return;
            }
            _mechanism.Open(DreamEscapeIds.PuzzleRosterSlider, DreamMechanismMode.Roster);
            return;
        }
        if (targetId == "target.music.metronome")
        {
            UseAvailableItems(targetId, showResult: false);
            if (!_session.State.DreamEscape!.InstalledParts.Contains(DreamEscapeIds.SlotMusicMetronomeWeight))
            {
                ShowToast("节拍器缺少摆锤。拾取摆锤后再次检查即可自动安装。");
                return;
            }
            OpenPuzzle("music_metronome");
            return;
        }
        if (targetId == "target.archive.recorder")
        {
            UseAvailableItems(targetId);
            var state = _session.State.DreamEscape!;
            if (state.SolvedPuzzles.Contains(DreamEscapeIds.PuzzleArchiveRecorderRebuild))
                return;
            if (!state.InstalledParts.Contains(DreamEscapeIds.SlotArchiveRecorderReel) ||
                !state.InstalledParts.Contains(DreamEscapeIds.SlotArchiveRecorderCrank) ||
                !state.InstalledParts.Contains(DreamEscapeIds.SlotArchiveRecorderFuse))
            {
                ShowToast("录音机仍缺校准带盘、摇柄或修复保险丝。已持有的匹配部件会自动安装。");
                return;
            }
            return;
        }
        if (targetId == "target.auditorium.line_set")
        {
            UseAvailableItems(targetId, showResult: false);
            if (!_session.State.DreamEscape!.InstalledParts.Contains(
                    DreamEscapeIds.SlotAuditoriumCurtainCounterweight))
            {
                ShowToast("飞索架缺少幕布配重。先在乐池拾取并安装配重。");
                return;
            }
            OpenPuzzle("auditorium_line_set_balance");
            return;
        }
        if (targetId == "target.auditorium.projector")
        {
            UseAvailableItems(targetId, showResult: false);
            if (!_session.State.DreamEscape!.InstalledParts.Contains(DreamEscapeIds.SlotAuditoriumProjectorLens))
            {
                ShowToast("放映机缺少镜头。找到后再次检查即可自动安装。");
                return;
            }
            OpenPuzzle("auditorium_shadow_projector");
            return;
        }
        if (targetId.StartsWith("puzzle:", StringComparison.Ordinal))
        {
            OpenPuzzle(targetId["puzzle:".Length..]);
            return;
        }
        if (targetId == "hint:xiaoyu")
        {
            _inventory.Open(DreamInventoryPage.Xiaoyu);
            return;
        }
        if (targetId == "locked:mansion_front_door")
        {
            ShowToast("厚重的双叶大门纹丝不动，锁舌像是从门外封死了。");
            return;
        }
        if (targetId == "action:left_lever")
        {
            var result = _session.PrepareDreamLeftLever();
            if (result.FeedbackKey == "DREAM_FEEDBACK_XIAOYU_READY")
                _xiaoyuHoldingExitLever = true;
            PushTransientState();
            ApplyAndRefresh(result);
            return;
        }
        if (targetId == "action:open_exit")
        {
            if (!_xiaoyuHoldingExitLever)
            {
                ShowToast(FeedbackText("DREAM_FEEDBACK_XIAOYU_NOT_READY"));
                return;
            }
            var result = _session.OpenDreamExit();
            ShowResult(result);
            if (result.Changed)
            {
                _xiaoyuHoldingExitLever = false;
                PushTransientState();
                ExitOpened?.Invoke();
            }
            return;
        }

        UseAvailableItems(targetId);
    }

    private DreamActionResult UseAvailableItems(string targetId, bool showResult = true)
    {
        var result = _session.UseAvailableDreamItems(targetId);
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
        if (showResult) ShowResult(result, special);
        if (result.Changed)
        {
            _inventory.Refresh();
            _area?.RefreshState();
        }
        RefreshHud();
        return result;
    }

    private async Task TransitionToAreaAsync(string targetId)
    {
        if (_areaTransitionRunning || !targetId.StartsWith("area:", StringComparison.Ordinal)) return;
        _areaTransitionRunning = true;
        using var lease = _session.Modal.Acquire("dream.area_transition");
        IDreamAreaExitTransition? exitTransition = null;
        try
        {
            var areaAtStart = _area;
            var sourceArea = _session.State.DreamEscape!.CurrentAreaId;
            var nextArea = targetId["area:".Length..];
            var spawnRoute = DreamAreaSpawnCatalog.Resolve(sourceArea, nextArea);
            float? cameraDoorOffset = null;
            if (areaAtStart is IDreamAreaTransitionPose3D poseProvider &&
                DreamAreaSpawnCatalog.TryGetExitForward(sourceArea, nextArea, out var exitForward))
            {
                cameraDoorOffset = poseProvider.CaptureCameraDoorOffsetDegrees(exitForward);
            }

            exitTransition = areaAtStart as IDreamAreaExitTransition;
            if (exitTransition is not null)
                await exitTransition.PlayExitTransitionAsync(targetId, _lifetimeCancellation.Token);

            await FadeTransitionAsync(1f, 0.15f, _lifetimeCancellation.Token);
            var result = _session.MoveDreamToArea(
                nextArea,
                spawnRoute.StoredPosition.X,
                spawnRoute.StoredPosition.Y);
            if (result.Changed)
            {
                LoadArea(nextArea);
                if (_area is IDreamAreaTransitionPose3D poseReceiver)
                {
                    poseReceiver.ApplyEntryPose(spawnRoute.EntryFacing, cameraDoorOffset);
                }
            }
            else
            {
                ResetExitTransitionSafely(exitTransition);
                ShowResult(result);
            }
            await FadeTransitionAsync(0f, 0.15f, _lifetimeCancellation.Token);
        }
        catch (OperationCanceledException)
        {
            // Scene shutdown cancels a presentation-only transition safely.
            ResetExitTransitionSafely(exitTransition);
        }
        catch (Exception exception)
        {
            GD.PushError($"Dream area transition failed: {exception}");
            ResetExitTransitionSafely(exitTransition);
            ShowToast("门没有完全打开，请再试一次。");
        }
        finally
        {
            _areaTransitionRunning = false;
            if (_transitionFade is not null)
            {
                _transitionFade.Modulate = new Color(1f, 1f, 1f, 0f);
                _transitionFade.Visible = false;
            }
        }
    }

    private async Task FadeTransitionAsync(float targetAlpha, float duration, CancellationToken cancellationToken)
    {
        _transitionFade.Visible = true;
        _transitionFade.MoveToFront();
        var startAlpha = _transitionFade.Modulate.A;
        var elapsed = 0f;
        while (elapsed < duration)
        {
            cancellationToken.ThrowIfCancellationRequested();
            await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame);
            elapsed += (float)GetProcessDeltaTime();
            var weight = Mathf.SmoothStep(0f, 1f, Mathf.Clamp(elapsed / duration, 0f, 1f));
            _transitionFade.Modulate = new Color(1f, 1f, 1f, Mathf.Lerp(startAlpha, targetAlpha, weight));
        }
        _transitionFade.Modulate = new Color(1f, 1f, 1f, targetAlpha);
    }

    private static void ResetExitTransitionSafely(IDreamAreaExitTransition? transition)
    {
        if (transition is null) return;
        if (transition is GodotObject godotObject && !GodotObject.IsInstanceValid(godotObject)) return;
        transition.ResetExitTransition();
    }

    private void OpenPuzzle(string shortId)
    {
        var digits = Enumerable.Range(0, 10).Select(value => (value.ToString(), value.ToString())).ToArray();
        switch (shortId)
        {
            case "window_latches":
                _mechanism.Open(DreamEscapeIds.PuzzleWindowLatches, DreamMechanismMode.WindowLatches);
                break;
            case "teacher_desk":
                _puzzle.Open(DreamEscapeIds.PuzzleTeacherDesk, "教师桌密码锁",
                    ClueAwarePrompt(DreamEscapeIds.PuzzleTeacherDesk, "综合分散在教室中的记录输入三位数字。"),
                    digits, 3, true);
                break;
            case "music_cage":
                _puzzle.Open(DreamEscapeIds.PuzzleMusicCage, "器材间音板", "依次按下四块音板；编号同时有长短谱线标记。", digits[1..5], 4);
                break;
            case "music_metronome":
                _puzzle.Open(DreamEscapeIds.PuzzleMusicMetronome, "节拍器校准",
                    ClueAwarePrompt(DreamEscapeIds.PuzzleMusicMetronome, "根据入口排练通知设置正确 BPM。"),
                    digits, 2, true);
                break;
            case "music_tuning_forks":
                _puzzle.Open(DreamEscapeIds.PuzzleMusicTuningForks, "音叉排序",
                    ClueAwarePrompt(DreamEscapeIds.PuzzleMusicTuningForks, "结合器材间对照表按衰减与波形排列。"),
                    PitchChoices(), 4);
                break;
            case "music_piano_roll":
                _puzzle.Open(DreamEscapeIds.PuzzleMusicPianoRoll, "纸带修补",
                    ClueAwarePrompt(DreamEscapeIds.PuzzleMusicPianoRoll, "根据纸带柜模板排列四段纸带。"),
                    digits[1..5], 4);
                break;
            case "music_acoustic_wall":
                _puzzle.Open(DreamEscapeIds.PuzzleMusicAcousticWall, "声学墙转译", "按记录依次完成“图形→音高→乐器”的三层换算。晓雨会确认低频第四拍。", InstrumentChoices(), 4);
                break;
            case "archive_signal_registry":
                _puzzle.Open(DreamEscapeIds.PuzzleArchiveSignalRegistry, "广播档案登记",
                    ClueAwarePrompt(DreamEscapeIds.PuzzleArchiveSignalRegistry,
                        "根据上午/下午与南/北双重印章排列四份档案。"),
                    SignalRegistryChoices(), 4);
                break;
            case "music_attendance_reader":
                _puzzle.Open(DreamEscapeIds.PuzzleMusicAttendanceReader, "点名读取装置", "叠放底片、孔卡和红色滤尺后，输入四个座位坐标。", CoordinateChoices(), 4);
                break;
            case "archive_drawer_route":
                _puzzle.Open(DreamEscapeIds.PuzzleArchiveDrawerRoute, "抽屉线路",
                    ClueAwarePrompt(DreamEscapeIds.PuzzleArchiveDrawerRoute, "根据入口索引条连接四个抽屉节点。"),
                    DrawerChoices(), 4);
                break;
            case "archive_fuse_repair":
                _puzzle.Open(DreamEscapeIds.PuzzleArchiveFuseRepair, "保险丝修复",
                    ClueAwarePrompt(DreamEscapeIds.PuzzleArchiveFuseRepair, "按奖励柜线路图排列三段铜线。"),
                    RouteChoices(), 3);
                break;
            case "archive_editing_counter":
                _puzzle.Open(DreamEscapeIds.PuzzleArchiveEditingCounter, "剪辑计数器",
                    ClueAwarePrompt(DreamEscapeIds.PuzzleArchiveEditingCounter, "输入录音机依次停下的四个机械读数。"),
                    CounterChoices(), 4);
                break;
            case "auditorium_line_set_balance":
                _mechanism.Open(DreamEscapeIds.PuzzleAuditoriumLineSetBalance,
                    DreamMechanismMode.LineSetBalance);
                break;
            case "auditorium_shadow_projector":
                _puzzle.Open(DreamEscapeIds.PuzzleAuditoriumShadowProjector, "影盘放映机",
                    ClueAwarePrompt(DreamEscapeIds.PuzzleAuditoriumShadowProjector, "根据幕后的三联轮廓对齐影盘。"),
                    ProjectionChoices(), 3);
                break;
            case "auditorium_master_tape_splice":
                _puzzle.Open(DreamEscapeIds.PuzzleAuditoriumMasterTapeSplice, "母带拼接",
                    ClueAwarePrompt(DreamEscapeIds.PuzzleAuditoriumMasterTapeSplice, "根据登记卡排列三段剪带。"),
                    TapeChoices(), 3);
                break;
            case "auditorium_light_console":
                _puzzle.Open(DreamEscapeIds.PuzzleAuditoriumLightConsole, "灯光控制台",
                    ClueAwarePrompt(DreamEscapeIds.PuzzleAuditoriumLightConsole, "设置三组遮光片高度；晓雨会在舞台确认重合。"),
                    digits, 3, true);
                break;
            case "auditorium_sound_console":
                _puzzle.Open(DreamEscapeIds.PuzzleAuditoriumSoundConsole, "声道控制台",
                    ClueAwarePrompt(DreamEscapeIds.PuzzleAuditoriumSoundConsole, "连接四条乐器声道。"),
                    InstrumentChoices(), 4);
                break;
            case "auditorium_broadcast_console":
                _puzzle.Open(DreamEscapeIds.PuzzleAuditoriumBroadcastConsole, "广播控制台",
                    ClueAwarePrompt(DreamEscapeIds.PuzzleAuditoriumBroadcastConsole, "连接四个广播图标通路。"),
                    BroadcastChoices(), 4);
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
            "DREAM_FEEDBACK_WINDOW_CACHE_OPENED" => "三只窗扣同时轻响。窗台暗格弹开，里面是粉笔、上弦钥匙和值日表滑片。",
            "DREAM_FEEDBACK_CLOCK_RESTORED" => "齿轮重新咬合，时钟停稳在七点整。",
            "DREAM_FEEDBACK_ROSTER_RESTORED" => "滑片边缘与缺口重合，红圈清楚落在第二项。",
            "DREAM_FEEDBACK_DESK_OPENED" => "锁舌弹开。教师桌里有第二室钥匙，还有一张写着“三角维护匙”的便签。",
            "DREAM_FEEDBACK_XIAOYU_JOINED" => "器材间打开。被困的女孩说她叫林晓雨，并决定先和你一起找出口。",
            "DREAM_FEEDBACK_METRONOME_CALIBRATED" => "节拍器稳定在 72 BPM。遮片依次显出 ○—◇—□—△；最后一拍的低频由晓雨确认。",
            "DREAM_FEEDBACK_TUNING_FORKS_CALIBRATED" => "音叉由低到高排成 A3—C4—E4—A4，背面的图形给出 ○、◇、□、△ 的对应关系。",
            "DREAM_FEEDBACK_PIANO_ROLL_REPAIRED" => "纸带按 2—4—1—3 接合，显出 A3=木鱼、C4=钢琴、E4=三角铁、A4=手铃。",
            "DREAM_FEEDBACK_ACOUSTIC_WALL_OPENED" => "四块共鸣板依次响应。声学墙弹出一把刻着“三”的黄铜钥匙。",
            "DREAM_FEEDBACK_ROOM_THREE_OPEN" => "四种音色重叠成一声短鸣，走廊第三扇门解锁了。",
            "DREAM_FEEDBACK_SIGNAL_REGISTRY_OPENED" => "登记台按合唱—广播—美术—田径亮起，奖励柜弹出三角维护钥匙、红滤尺和陶瓷保险管。",
            "DREAM_FEEDBACK_ARCHIVE_ROUTE_OPENED" => "抽屉矩阵弹开，里面有漆包铜线和接带胶。",
            "DREAM_FEEDBACK_FUSE_REPAIRED" => "保险管完成桥接。修复保险丝先放入了背包。",
            "DREAM_FEEDBACK_RECORDER_PLAYING" => "晓雨保持带盘张力，录音机依次停在 12—24—31—45，面板显示幕布—时钟—校铃—点名册。",
            "DREAM_FEEDBACK_EDITING_COUNTER_OPENED" => "剪辑计数器弹开，里面是第四室钥匙。",
            "DREAM_FEEDBACK_LINE_SET_BALANCED" => "A、B、C、D 四组配重停在 3—1—4—2，主幕升起并露出三联轮廓。",
            "DREAM_FEEDBACK_PROJECTOR_OPENED" => "影盘对齐。奖励柜弹出三件回访工具和空母带盘芯。",
            "DREAM_FEEDBACK_MASTER_TAPE_RESTORED" => "三段剪带拼接完成，复原母带先放入了背包。",
            "DREAM_FEEDBACK_ATTENDANCE_OPENED" => "点名装置弹开，里面放着校准带盘和录音机摇柄。",
            "DREAM_FEEDBACK_PIANO_BENCH_OPENED" => "夹锁松开。琴凳暗格中是一段标着“音乐”的旧剪带。",
            "DREAM_FEEDBACK_ROOM_FOUR_OPEN" => "第四扇门后的锁链落地。旧礼堂已经开放。",
            _ => null
        } : null;
        ApplyAndRefresh(result, special);
    }

    private IDisposable? _pinLease;
    private IDisposable? _controlReminderLease;

    public void ShowEntryControlReminder()
    {
        if (_controlReminder.Visible) return;
        _controlReminderLease?.Dispose();
        _controlReminderLease = _session.Modal.Acquire("dream-entry-controls");
        _controlReminder.PopupCentered(new Vector2I(430, 190));
    }

    private void ReleaseControlReminder()
    {
        _controlReminderLease?.Dispose();
        _controlReminderLease = null;
    }

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
            _area?.RefreshState();
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
        var shouldPlayWhiteNoise =
            dream.CurrentAreaId == DreamEscapeIds.AreaAuditorium &&
            dream.InstalledParts.Contains(DreamEscapeIds.SlotAuditoriumMasterTape);
        if (shouldPlayWhiteNoise && !_masterTapeNoise.Playing) _masterTapeNoise.Play();
        else if (!shouldPlayWhiteNoise && _masterTapeNoise.Playing) _masterTapeNoise.Stop();
    }

    private void OnMasterTapeNoiseFinished()
    {
        if (IsInsideTree() &&
            _session.State.DreamEscape is { CurrentAreaId: DreamEscapeIds.AreaAuditorium } dream &&
            dream.InstalledParts.Contains(DreamEscapeIds.SlotAuditoriumMasterTape))
            _masterTapeNoise.Play();
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

    private static IReadOnlyList<(string, string)> PitchChoices() =>
        [("A3", "A3　▂▂"), ("C4", "C4　▃▃"), ("E4", "E4　▅▅"), ("A4", "A4　▇▇")];

    private static IReadOnlyList<(string, string)> SignalRegistryChoices() =>
        [("choir", "合唱·上午北"), ("broadcast", "广播·上午南"),
            ("art", "美术·下午北"), ("track", "田径·下午南")];

    private static IReadOnlyList<(string, string)> CoordinateChoices() =>
        [("A3", "A3"), ("B2", "B2"), ("C4", "C4"), ("D1", "D1"), ("A1", "A1"), ("D4", "D4")];

    private static IReadOnlyList<(string, string)> BroadcastChoices() =>
        [("curtain", "窗帘　▥"), ("clock", "时钟　◷"), ("bell", "校铃　⌂"), ("register", "点名册　▤")];

    private static IReadOnlyList<(string, string)> DrawerChoices() =>
        [("A4", "A4"), ("B3", "B3"), ("C1", "C1"), ("D2", "D2")];

    private static IReadOnlyList<(string, string)> RouteChoices() =>
        [("left", "左"), ("middle", "中"), ("right", "右")];

    private static IReadOnlyList<(string, string)> CounterChoices() =>
        [("12", "12"), ("24", "24"), ("31", "31"), ("45", "45")];

    private static IReadOnlyList<(string, string)> ProjectionChoices() =>
        [("window", "窗格"), ("figure", "人物"), ("bell", "校铃")];

    private static IReadOnlyList<(string, string)> TapeChoices() =>
        [("classroom", "教室"), ("music", "音乐"), ("archive", "档案")];

    private string ClueAwarePrompt(string puzzleId, string completePrompt)
    {
        var missing = _session.DreamEscape.MissingInputClues(puzzleId);
        return missing.Count == 0
            ? completePrompt
            : "机关允许直接尝试；关联记录尚未收集完整，请在其他功能区寻找纸面或设备记录。";
    }

    private static string AreaName(string id) => id switch
    {
        DreamEscapeIds.AreaCorridor => "洋房前厅与内廊", DreamEscapeIds.AreaClassroom => "第一室·旧教室",
        DreamEscapeIds.AreaMusic => "第二室·音乐教室", DreamEscapeIds.AreaArchive => "第三室·档案兼广播室",
        DreamEscapeIds.AreaAuditorium => "第四室·旧礼堂", _ => id
    };

    private static string FeedbackText(string key) => key switch
    {
        "DREAM_FEEDBACK_ITEM_PICKED_UP" => "物品已放入随身物品栏。",
        "DREAM_FEEDBACK_ROOM_TWO_OPEN" => "黄铜钥匙转动，第二扇门打开了。",
        "DREAM_FEEDBACK_ROOM_THREE_OPEN" => "第三室钥匙转动，档案兼广播室已经开放。",
        "DREAM_FEEDBACK_ROOM_FOUR_OPEN" => "第四室钥匙转动，旧礼堂已经开放。",
        "DREAM_FEEDBACK_MAP_CABINET_OPENED" => "三角钥匙正好吻合。地图柜里压着合影底片和点名孔卡。",
        "DREAM_FEEDBACK_TRANSOM_CACHE_OPENED" => "钩杆拉下高处盒盖，里面是一段标着“教室”的剪带。",
        "DREAM_FEEDBACK_CLOCK_HAND_INSTALLED" => "黄铜时针卡入轴心。",
        "DREAM_FEEDBACK_CLOCK_KEY_INSTALLED" => "上弦钥匙插入侧孔，内部齿轮开始受力。",
        "DREAM_FEEDBACK_ROSTER_SLIDER_INSTALLED" => "透明滑片嵌入值日表边框。",
        "DREAM_FEEDBACK_METRONOME_WEIGHT_INSTALLED" => "摆锤卡入节拍器滑杆，现在可以调整速度。",
        "DREAM_FEEDBACK_TAPE_INSERTED" => "录音带装好了，但转轴仍然不动。",
        "DREAM_FEEDBACK_RECORDER_CRANK_INSTALLED" => "摇柄装入录音机侧轴。",
        "DREAM_FEEDBACK_RECORDER_FUSE_INSTALLED" => "修复保险丝装入录音机电路。",
        "DREAM_FEEDBACK_CURTAIN_WEIGHT_INSTALLED" => "配重挂入吊杆组，现在可以按磨损刻度调平四组线路。",
        "DREAM_FEEDBACK_PROJECTOR_LENS_INSTALLED" => "镜头装入放映机，现在可以对齐影盘。",
        "DREAM_FEEDBACK_MASTER_TAPE_PLAYING" => "母带播放器运转正常，只输出持续白噪声；屏幕同步显示静态波形。",
        "DREAM_FEEDBACK_FINAL_PANEL_SOLVED" => "控制板亮起稳定的青绿色光。",
        "DREAM_FEEDBACK_FINAL_PANELS_RETURN_TOOLS" => "三块控制板全部亮起，侧柜弹开，露出舞台钩杆和琴凳夹钥。",
        "DREAM_FEEDBACK_PIANO_BENCH_OPENED" => "夹锁松开，琴凳暗格里是一段标着“音乐”的剪带。",
        "DREAM_FEEDBACK_XIAOYU_READY" => "林晓雨按住左侧机关：“右边交给你。”",
        "DREAM_FEEDBACK_EXIT_OPENED" => "总闸落下，舞台深处的出口缓缓开启。",
        "DREAM_FEEDBACK_WRONG_ANSWER" => "机关发出短促的拒绝声，输入已复位。",
        "DREAM_FEEDBACK_ITEM_MISSING" => "随身物品中还没有能处理这个机关的道具。",
        "DREAM_FEEDBACK_ITEM_DOES_NOT_FIT" => "这个道具与眼前的结构对不上。",
        "DREAM_FEEDBACK_MATCHING_ITEMS_INSTALLED" => "背包中的匹配部件已经自动安装。",
        "DREAM_FEEDBACK_NEED_BLACKBOARD_CLUE" => "锁盘没有提示。黑板上似乎还有没显出的痕迹。",
        "DREAM_FEEDBACK_NEED_CLASSROOM_OBSERVATIONS" => "密码需要三个数字。先完成黑板、时钟和值日表。",
        "DREAM_FEEDBACK_CLOCK_PARTS_MISSING" => "时钟还缺少部件。",
        "DREAM_FEEDBACK_ROSTER_SLIDER_MISSING" => "值日表还缺少透明滑片。",
        "DREAM_FEEDBACK_NEED_SCORE_CLUE" => "四块音板没有编号；附近的乐谱也许能留下提示。",
        "DREAM_FEEDBACK_NEED_XIAOYU" => "先打开器材间；这组机关需要晓雨一起确认。",
        "DREAM_FEEDBACK_METRONOME_WEIGHT_MISSING" => "节拍器缺少摆锤。",
        "DREAM_FEEDBACK_TUNING_TOOLS_MISSING" => "音叉排序需要橡胶音槌和共鸣听筒。",
        "DREAM_FEEDBACK_PIANO_ROLL_PARTS_MISSING" => "纸带修补需要破损纸带和蜡纸修补条。",
        "DREAM_FEEDBACK_NEED_MUSIC_TRANSLATIONS" => "还缺少节拍图形、图形音高或音高乐器映射。",
        "DREAM_FEEDBACK_ROOM_THREE_KEY_NOT_READY" => "这把钥匙还没有从声学墙中取得。",
        "DREAM_FEEDBACK_BENCH_TOOL_NOT_READY" => "琴凳夹钥应当来自礼堂的回访工具柜。",
        "DREAM_FEEDBACK_ARCHIVE_INDEX_MISSING" => "抽屉矩阵需要入口档案台上的索引条。",
        "DREAM_FEEDBACK_FUSE_PARTS_MISSING" => "保险丝维修需要陶瓷保险管和漆包铜线。",
        "DREAM_FEEDBACK_RECORDER_PARTS_MISSING" => "录音机仍缺校准带盘、摇柄或修复保险丝。",
        "DREAM_FEEDBACK_TAPE_PARTS_MISSING" => "还缺剪带、接带胶或空母带盘芯。",
        "DREAM_FEEDBACK_MASTER_TAPE_NOT_PLAYING" => "先把复原母带安装到播放器。",
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
            ("DREAM_HINT_CLASSROOM_BLACKBOARD", 2) => "关联提示：窗台暗格里的粉笔可以让凹痕显形。",
            ("DREAM_HINT_CLASSROOM_BLACKBOARD", 3) => "近答案提示：带着粉笔检查黑板，系统会自动使用它。",
            ("DREAM_HINT_CLASSROOM_TEACHER_DESK", 1) => "目标提示：记录分散在黑板、后排维护卡、讲台作息表和后排便签。",
            ("DREAM_HINT_CLASSROOM_TEACHER_DESK", 2) => "关联提示：按黑板顺序读取窗格数、时钟结果和值日表结果。",
            ("DREAM_HINT_CLASSROOM_TEACHER_DESK", 3) => "近答案提示：输入 3、7、2。",
            ("DREAM_HINT_CLASSROOM_WINDOW_LATCHES", 1) => "目标提示：去后排左侧课桌区找窗扣维护资料。",
            ("DREAM_HINT_CLASSROOM_WINDOW_LATCHES", 2) => "关联提示：维护卡三幅磨损示意的高度分别对应三只窗扣的位置。",
            ("DREAM_HINT_CLASSROOM_WINDOW_LATCHES", 3) => "近答案提示：从左到右是上、下、中。",
            ("DREAM_HINT_CLASSROOM_CLOCK_RESTORE", 1) => "目标提示：前方讲台的晨间作息表记录了时钟应指向的时刻。",
            ("DREAM_HINT_CLASSROOM_CLOCK_RESTORE", 2) => "关联提示：读出时钟图标旁的时刻，再装入地上的时针和暗格钥匙。",
            ("DREAM_HINT_CLASSROOM_CLOCK_RESTORE", 3) => "近答案提示：装好两件部件，调到七点整。",
            ("DREAM_HINT_CLASSROOM_ROSTER_SLIDER", 1) => "目标提示：后排右侧课桌的便签记录了应对齐的项目。",
            ("DREAM_HINT_CLASSROOM_ROSTER_SLIDER", 2) => "关联提示：便签红圈对应值日表项目；暗格滑片用于复原表格。",
            ("DREAM_HINT_CLASSROOM_ROSTER_SLIDER", 3) => "近答案提示：中央对齐后确认第二项。",
            ("DREAM_HINT_CLASSROOM_MAP_CABINET", 1) => "目标提示：地图柜使用三角锁。",
            ("DREAM_HINT_CLASSROOM_MAP_CABINET", 2) => "关联提示：档案室取得的维护钥匙与它吻合。",
            ("DREAM_HINT_CLASSROOM_MAP_CABINET", 3) => "近答案提示：使用三角维护钥匙。",
            ("DREAM_HINT_CLASSROOM_TRANSOM_CACHE", 1) => "目标提示：门上方的盒子够不到。",
            ("DREAM_HINT_CLASSROOM_TRANSOM_CACHE", 2) => "关联提示：礼堂处理高处幕布的工具能够勾住金属环。",
            ("DREAM_HINT_CLASSROOM_TRANSOM_CACHE", 3) => "近答案提示：使用舞台钩杆。",
            ("DREAM_HINT_MUSIC_SCORE", 1) => "目标提示：去音乐教室西北侧查看谱面拓显台。",
            ("DREAM_HINT_MUSIC_SCORE", 2) => "关联提示：第一室留下的粉笔能显出谱面浅痕。",
            ("DREAM_HINT_MUSIC_SCORE", 3) => "近答案提示：对谱面使用粉笔，记录 1、4、2、3。",
            ("DREAM_HINT_MUSIC_CAGE", 1) => "目标提示：去房间另一侧的西北拓显台寻找音板次序。",
            ("DREAM_HINT_MUSIC_CAGE", 2) => "关联提示：拓显谱面的四个编号分别对应器材间四块音板。",
            ("DREAM_HINT_MUSIC_CAGE", 3) => "近答案提示：依次按 1、4、2、3。",
            ("DREAM_HINT_MUSIC_METRONOME", 1) => "目标提示：西南入口公告板的排练通知标出了速度。",
            ("DREAM_HINT_MUSIC_METRONOME", 2) => "关联提示：被圈出的 BPM 是装好摆锤后应设置的刻度。",
            ("DREAM_HINT_MUSIC_METRONOME", 3) => "近答案提示：把速度设为 72 BPM。",
            ("DREAM_HINT_MUSIC_TUNING_FORKS", 1) => "目标提示：打开后的东北器材间里有波形与衰减对照表。",
            ("DREAM_HINT_MUSIC_TUNING_FORKS", 2) => "关联提示：用音槌和听筒比较音叉，再按对照表换成音高文字。",
            ("DREAM_HINT_MUSIC_TUNING_FORKS", 3) => "近答案提示：A3、C4、E4、A4。",
            ("DREAM_HINT_MUSIC_PIANO_ROLL", 1) => "目标提示：西侧纸带柜内侧有一张裂口模板。",
            ("DREAM_HINT_MUSIC_PIANO_ROLL", 2) => "关联提示：模板编号决定四段纸带的拼合顺序，蜡纸用于固定。",
            ("DREAM_HINT_MUSIC_PIANO_ROLL", 3) => "近答案提示：2、4、1、3。",
            ("DREAM_HINT_MUSIC_ACOUSTIC_WALL", 1) => "目标提示：去南侧节拍器、北侧音叉架和南侧纸带播放器收集三层记录。",
            ("DREAM_HINT_MUSIC_ACOUSTIC_WALL", 2) => "关联提示：依次把图形换成音高、再换成乐器；第四拍由晓雨确认。",
            ("DREAM_HINT_MUSIC_ACOUSTIC_WALL", 3) => "近答案提示：木鱼、钢琴、三角铁、手铃。",
            ("DREAM_HINT_ARCHIVE_SIGNAL_REGISTRY", 1) => "目标提示：墙面登记规则与四份盖章档案分散在书架、广播桌、维修台和入口柜。",
            ("DREAM_HINT_ARCHIVE_SIGNAL_REGISTRY", 2) => "关联提示：先按上午、下午分组；同一时段再按北侧、南侧排序。",
            ("DREAM_HINT_ARCHIVE_SIGNAL_REGISTRY", 3) => "近答案提示：合唱、广播、美术、田径。",
            ("DREAM_HINT_MUSIC_ATTENDANCE_READER", 1) => "目标提示：点名装置需要三层载体叠放。",
            ("DREAM_HINT_MUSIC_ATTENDANCE_READER", 2) => "关联提示：使用合影底片、点名孔卡和档案室的红色滤尺。",
            ("DREAM_HINT_MUSIC_ATTENDANCE_READER", 3) => "近答案提示：B2、D1、A3、C4。",
            ("DREAM_HINT_MUSIC_PIANO_BENCH_CACHE", 1) => "目标提示：琴凳下方是夹锁，不是普通钥匙孔。",
            ("DREAM_HINT_MUSIC_PIANO_BENCH_CACHE", 2) => "关联提示：礼堂放映机关发放的扁平工具可以松开它。",
            ("DREAM_HINT_MUSIC_PIANO_BENCH_CACHE", 3) => "近答案提示：使用琴凳夹钥。",
            ("DREAM_HINT_ARCHIVE_DRAWER_ROUTE", 1) => "目标提示：入口档案台上有一条独立索引条。",
            ("DREAM_HINT_ARCHIVE_DRAWER_ROUTE", 2) => "关联提示：从索引条的 C1 起点沿未重复的下一站连接抽屉矩阵。",
            ("DREAM_HINT_ARCHIVE_DRAWER_ROUTE", 3) => "近答案提示：C1、A4、D2、B3。",
            ("DREAM_HINT_ARCHIVE_FUSE_REPAIR", 1) => "目标提示：完成中央分拣台后，奖励柜会露出保险丝线路图。",
            ("DREAM_HINT_ARCHIVE_FUSE_REPAIR", 2) => "关联提示：把保险管和铜线组合，再按线路图跨过左、中、右触点。",
            ("DREAM_HINT_ARCHIVE_FUSE_REPAIR", 3) => "近答案提示：路径是左、中、右。",
            ("DREAM_HINT_ARCHIVE_RECORDER_REBUILD", 1) => "目标提示：录音机在东北广播桌；所需部件来自维修台和第一次回访。",
            ("DREAM_HINT_ARCHIVE_RECORDER_REBUILD", 2) => "关联提示：装入校准带盘、摇柄和修复保险丝，请晓雨保持带盘张力。",
            ("DREAM_HINT_ARCHIVE_RECORDER_REBUILD", 3) => "近答案提示：三件部件全部安装后确认重建。",
            ("DREAM_HINT_ARCHIVE_EDITING_COUNTER", 1) => "目标提示：去房间另一端的剪辑计数器读取录音机记录。",
            ("DREAM_HINT_ARCHIVE_EDITING_COUNTER", 2) => "关联提示：按录音机四次机械停点的先后输入数字。",
            ("DREAM_HINT_ARCHIVE_EDITING_COUNTER", 3) => "近答案提示：12、24、31、45。",
            ("DREAM_HINT_ARCHIVE_UPPER_VAULT", 1) => "目标提示：北墙高柜的锁舌藏在背面。",
            ("DREAM_HINT_ARCHIVE_UPPER_VAULT", 2) => "关联提示：礼堂放映机取得的折角镜能看见并拨动背锁。",
            ("DREAM_HINT_ARCHIVE_UPPER_VAULT", 3) => "近答案提示：对高柜使用折角检视镜。",
            ("DREAM_HINT_AUDITORIUM_LINE_SET_BALANCE", 1) => "目标提示：后台磨损尺记录了 A、B、C、D 四组吊杆的配重档位，缺失配重在观众区侧边。",
            ("DREAM_HINT_AUDITORIUM_LINE_SET_BALANCE", 2) => "关联提示：装上配重后，把四组刻度分别调到磨损最重的位置。",
            ("DREAM_HINT_AUDITORIUM_LINE_SET_BALANCE", 3) => "近答案提示：A、B、C、D 依次是 3、1、4、2。",
            ("DREAM_HINT_AUDITORIUM_SHADOW_PROJECTOR", 1) => "目标提示：升幕后查看舞台背景的三联轮廓。",
            ("DREAM_HINT_AUDITORIUM_SHADOW_PROJECTOR", 2) => "关联提示：安装镜头后，让西北放映机的三个影盘依次对应轮廓。",
            ("DREAM_HINT_AUDITORIUM_SHADOW_PROJECTOR", 3) => "近答案提示：窗格、人物、校铃。",
            ("DREAM_HINT_AUDITORIUM_MASTER_TAPE_SPLICE", 1) => "目标提示：放映机奖励柜的登记卡记录了三段剪带顺序。",
            ("DREAM_HINT_AUDITORIUM_MASTER_TAPE_SPLICE", 2) => "关联提示：在东南接带台按房间顺序放入剪带，并加入接带胶和盘芯。",
            ("DREAM_HINT_AUDITORIUM_MASTER_TAPE_SPLICE", 3) => "近答案提示：教室、音乐、档案。",
            ("DREAM_HINT_AUDITORIUM_LIGHT_CONSOLE", 1) => "目标提示：西侧灯光台复用第一室教师桌的三位记录。",
            ("DREAM_HINT_AUDITORIUM_LIGHT_CONSOLE", 2) => "关联提示：按第一室记录设置三组高度，请舞台上的晓雨确认重合。",
            ("DREAM_HINT_AUDITORIUM_LIGHT_CONSOLE", 3) => "近答案提示：3、7、2。",
            ("DREAM_HINT_AUDITORIUM_SOUND_CONSOLE", 1) => "目标提示：中央调音台复用第二室声学墙记录。",
            ("DREAM_HINT_AUDITORIUM_SOUND_CONSOLE", 2) => "关联提示：按声学墙确认的四种乐器顺序连接声道。",
            ("DREAM_HINT_AUDITORIUM_SOUND_CONSOLE", 3) => "近答案提示：木鱼、钢琴、三角铁、手铃。",
            ("DREAM_HINT_AUDITORIUM_BROADCAST_CONSOLE", 1) => "目标提示：东侧广播台复用第三室剪辑计数器的图标记录。",
            ("DREAM_HINT_AUDITORIUM_BROADCAST_CONSOLE", 2) => "关联提示：按机械停点同时显示的四个图标顺序连接通路。",
            ("DREAM_HINT_AUDITORIUM_BROADCAST_CONSOLE", 3) => "近答案提示：幕布、时钟、校铃、点名册。",
            ("DREAM_HINT_AUDITORIUM_EXIT", 1) => "目标提示：左侧舞台机关和右侧总闸相距很远。",
            ("DREAM_HINT_AUDITORIUM_EXIT", 2) => "关联提示：请晓雨持续按住左侧机关，主角再去右侧总闸。",
            ("DREAM_HINT_AUDITORIUM_EXIT", 3) => "近答案提示：先左侧协作，再操作右侧总闸。",
            (_, 1) => "目标提示：先确认这个机关需要复现哪一间房的输入。",
            (_, 2) => "关联提示：打开记录，寻找相同形状或波形的线索。",
            (_, 3) => "近答案提示：答案已经出现在此前解开的机关中。",
            _ => "提示已经达到最明确的等级。"
        };
    }

    private static string? PuzzleForTarget(string targetId) => targetId switch
    {
        "puzzle:window_latches" => DreamEscapeIds.PuzzleWindowLatches,
        "target.classroom.blackboard" => DreamEscapeIds.PuzzleBlackboard,
        "target.classroom.clock" => DreamEscapeIds.PuzzleClockRestore,
        "target.classroom.duty_roster" => DreamEscapeIds.PuzzleRosterSlider,
        "puzzle:teacher_desk" => DreamEscapeIds.PuzzleTeacherDesk,
        "target.classroom.map_cabinet" => DreamEscapeIds.PuzzleMapCabinet,
        "target.classroom.transom_cache" => DreamEscapeIds.PuzzleTransomCache,
        "puzzle:music_cage" => DreamEscapeIds.PuzzleMusicCage,
        "target.music.metronome" => DreamEscapeIds.PuzzleMusicMetronome,
        "puzzle:music_tuning_forks" => DreamEscapeIds.PuzzleMusicTuningForks,
        "puzzle:music_piano_roll" => DreamEscapeIds.PuzzleMusicPianoRoll,
        "puzzle:music_acoustic_wall" => DreamEscapeIds.PuzzleMusicAcousticWall,
        "puzzle:archive_signal_registry" => DreamEscapeIds.PuzzleArchiveSignalRegistry,
        "puzzle:music_attendance_reader" => DreamEscapeIds.PuzzleMusicAttendanceReader,
        "target.music.piano_bench" => DreamEscapeIds.PuzzleMusicPianoBenchCache,
        "puzzle:archive_drawer_route" => DreamEscapeIds.PuzzleArchiveDrawerRoute,
        "puzzle:archive_fuse_repair" => DreamEscapeIds.PuzzleArchiveFuseRepair,
        "target.archive.recorder" => DreamEscapeIds.PuzzleArchiveRecorderRebuild,
        "puzzle:archive_editing_counter" => DreamEscapeIds.PuzzleArchiveEditingCounter,
        "target.archive.upper_vault" => DreamEscapeIds.PuzzleArchiveUpperVault,
        "target.auditorium.line_set" => DreamEscapeIds.PuzzleAuditoriumLineSetBalance,
        "target.auditorium.projector" => DreamEscapeIds.PuzzleAuditoriumShadowProjector,
        "puzzle:auditorium_master_tape_splice" => DreamEscapeIds.PuzzleAuditoriumMasterTapeSplice,
        "puzzle:auditorium_light_console" => DreamEscapeIds.PuzzleAuditoriumLightConsole,
        "puzzle:auditorium_sound_console" => DreamEscapeIds.PuzzleAuditoriumSoundConsole,
        "puzzle:auditorium_broadcast_console" => DreamEscapeIds.PuzzleAuditoriumBroadcastConsole,
        "action:open_exit" => DreamEscapeIds.PuzzleExit,
        _ => null
    };

    private void OnPuzzlePreviewChanged(string puzzleId, string[] values)
    {
        _previewPuzzleId = puzzleId;
        _previewValues = values;
        PushTransientState();
    }

    private void OnPuzzlePreviewClosed(string puzzleId)
    {
        if (_previewPuzzleId != puzzleId) return;
        _previewPuzzleId = null;
        _previewValues = null;
        PushTransientState();
    }

    private void PushTransientState()
    {
        if (_area is IDreamAreaTransientPresentation transient)
        {
            transient.ApplyTransientState(new DreamAreaTransientState(
                _xiaoyuHoldingExitLever, _previewPuzzleId, _previewValues));
        }
    }
}
