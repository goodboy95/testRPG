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
    private DreamStoryPlayer _story = null!;
    private bool _finishingExit;
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
    private int _handoffStep;
    private string? _previewPuzzleId;
    private IReadOnlyList<string>? _previewValues;
    private readonly CancellationTokenSource _lifetimeCancellation = new();
    private int _toastVersion;
    private readonly Dictionary<string, int> _thoughtVariants = new(StringComparer.Ordinal);

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

    public override void _Process(double delta)
    {
        if (_areaLabel is not null) _areaLabel.GetParent().GetParent<Control>().Visible = !_session.Modal.IsBlocked;
        if (_prompt is not null) _prompt.Visible = !_session.Modal.IsBlocked && !string.IsNullOrEmpty(_prompt.Text);
        UpdatePromptPosition();
        if (_xiaoyuHoldingExitLever && _session.Modal.IsBlocked) ResetCooperation();
        if (_story is null || _story.Running || _session.Modal.IsBlocked || _areaTransitionRunning || _finishingExit) return;
        if (_session.State.DreamEscape is not { Status: DreamEscapeStatus.InProgress } dream) return;
        if (! _session.State.CompletedEvents.Contains(DreamPrologueCatalog.EventId("intro")) ||
            DreamPrologueCatalog.Groups.Any(group => ! _session.State.CompletedEvents.Contains(DreamPrologueCatalog.EventId(group.Id)) &&
                DreamPrologueCatalog.StoryReady(DreamPrologueCatalog.EventId(group.Id), dream)))
            _ = PlayStorySafelyAsync();
    }

    private async Task PlayStorySafelyAsync()
    {
        try { await _story.PlayPendingAsync(_lifetimeCancellation.Token); }
        catch (OperationCanceledException) { }
        catch (Exception exception) { GD.PushError(exception.ToString()); SetProcess(false); ShowToast(DreamPrologueText.Format("DREAM_V6_UI_ESCAPECONTROLLER_001"), 12); }
    }

    public async Task FinishOpenedExitAsync()
    {
        if (_finishingExit) return;
        _finishingExit = true;
        try
        {
            await _story.PlayPendingAsync(_lifetimeCancellation.Token);
            if (IsInsideTree()) ExitOpened?.Invoke();
        }
        catch (OperationCanceledException) { }
        catch (Exception exception) { GD.PushError(exception.ToString()); ShowToast(DreamPrologueText.Format("DREAM_V6_UI_ESCAPECONTROLLER_002"), 12); }
        finally { _finishingExit = false; }
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
            _inventory.SetGamepad(_usingGamepad);
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
        _interfaceRoot.Theme = UiStyles.CreateTheme(_session.State.Settings.FontScale);
        _interfaceRoot.SetAnchorsAndOffsetsPreset(Control.LayoutPreset.FullRect);
        _interface.AddChild(_interfaceRoot);

        var areaBadge = new PanelContainer
        {
            Name = "AreaBadge",
            Position = new Vector2(18, 14),
            CustomMinimumSize = new Vector2(0, 20)
        };
        areaBadge.MouseBehaviorRecursive = Control.MouseBehaviorRecursiveEnum.Disabled;
        areaBadge.AddThemeStyleboxOverride("panel", UiStyles.Panel(Colors.Transparent));
        UiStyles.UseCinematic(areaBadge, _session.State.Settings.FontScale);
        _interfaceRoot.AddChild(areaBadge);
        var areaMargin = new MarginContainer();
        areaMargin.AddThemeConstantOverride("margin_left", 0);
        areaMargin.AddThemeConstantOverride("margin_right", 0);
        areaMargin.AddThemeConstantOverride("margin_top", 0);
        areaMargin.AddThemeConstantOverride("margin_bottom", 0);
        areaBadge.AddChild(areaMargin);
        _areaLabel = new Label { Name = "AreaName" };
        UiStyles.Role(_areaLabel, UiTextRole.Auxiliary);
        _areaLabel.AddThemeColorOverride("font_shadow_color", Colors.Black);
        _areaLabel.AddThemeConstantOverride("shadow_offset_y", 1);
        _areaLabel.AddThemeConstantOverride("shadow_outline_size", 0);
        _areaLabel.AddThemeConstantOverride("outline_size", 0);
        areaMargin.AddChild(_areaLabel);
        _prompt = new Label
        {
            Position = new Vector2(160, 326),
            Size = new Vector2(320, 24),
            HorizontalAlignment = HorizontalAlignment.Center,
            VerticalAlignment = VerticalAlignment.Center,
            AutowrapMode = TextServer.AutowrapMode.WordSmart,
            MouseFilter = Control.MouseFilterEnum.Ignore,
            Visible = false
        };
        _prompt.AddThemeStyleboxOverride("normal", UiStyles.Panel(
            _session.State.Settings.HighContrastPrompts ? Colors.Black : new Color(UiStyles.Background, .55f)));
        UiStyles.UseCinematic(_prompt, _session.State.Settings.FontScale);
        UiStyles.Role(_prompt, UiTextRole.Auxiliary);
        _prompt.AddThemeColorOverride("font_color", UiStyles.Ink);
        _interfaceRoot.AddChild(_prompt);
        _toast = new Label
        {
            Position = new Vector2(364, 18),
            Size = new Vector2(252, 32),
            HorizontalAlignment = HorizontalAlignment.Center,
            VerticalAlignment = VerticalAlignment.Center,
            AutowrapMode = TextServer.AutowrapMode.WordSmart,
            MouseFilter = Control.MouseFilterEnum.Ignore,
            Visible = false
        };
        _toast.AddThemeStyleboxOverride("normal", UiStyles.Panel(new Color(UiStyles.Surface, .75f)));
        _toast.AddThemeColorOverride("font_color", UiStyles.Ink);
        UiStyles.UseCinematic(_toast, _session.State.Settings.FontScale);
        UiStyles.Role(_toast, UiTextRole.Auxiliary);
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
            Title = DreamPrologueText.Format("DREAM_V6_UI_ESCAPECONTROLLER_003"),
            DialogText = DreamPrologueText.Format("DREAM_V6_UI_ESCAPECONTROLLER_004"),
            OkButtonText = DreamPrologueText.Format("DREAM_V6_UI_ESCAPECONTROLLER_005"),
            CancelButtonText = DreamPrologueText.Format("DREAM_V6_UI_ESCAPECONTROLLER_006"),
            Exclusive = true
        };
        _pinConfirmation.Confirmed += ConfirmPinUse;
        _pinConfirmation.Canceled += ReleasePinModal;
        _interfaceRoot.AddChild(_pinConfirmation);

        _controlReminder = new AcceptDialog
        {
            Name = "EntryControlReminder",
            Title = DreamPrologueText.Format("DREAM_V6_UI_ESCAPECONTROLLER_007"),
            DialogText = DreamPrologueText.Format("DREAM_V6_UI_ESCAPECONTROLLER_008"),
            OkButtonText = DreamPrologueText.Format("DREAM_V6_UI_ESCAPECONTROLLER_009"),
            Exclusive = true
        };
        _controlReminder.Confirmed += ReleaseControlReminder;
        _controlReminder.Canceled += ReleaseControlReminder;
        _interfaceRoot.AddChild(_controlReminder);

        _story = new DreamStoryPlayer { Name = "StoryPlayer" };
        AddChild(_story);
        _story.Configure(_interfaceRoot);

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
            UiStyles.ApplyScale(_interfaceRoot, settings.FontScale);
        }
        _prompt?.AddThemeStyleboxOverride("normal", UiStyles.Panel(
            settings.HighContrastPrompts ? Colors.Black : new Color(UiStyles.Background, .55f)));
        _area?.ApplySettings(settings);
    }

    private void UpdatePromptPosition()
    {
        if (_prompt is null || !_prompt.Visible) return;
        var scale = _session.State.Settings.FontScale;
        var width = Mathf.Clamp(_prompt.GetThemeDefaultFont().GetStringSize(_prompt.Text, fontSize: Mathf.RoundToInt(UiStyles.RoleSize(UiTextRole.Auxiliary) * scale)).X + 20, 90, 270);
        var height = width >= 270 ? 36 * scale : 20 * scale;
        _prompt.Size = new Vector2(width, height);
        var point = (_areaNode as WorldArea3D)?.InteractionScreenPoint ?? new Vector2(320 - width / 2, 316 - height);
        _prompt.Position = new Vector2(Mathf.Clamp(point.X + 14, 18, 622 - width), Mathf.Clamp(point.Y - height - 12, 58, 338 - height));
    }

    private void LoadArea(string areaId)
    {
        _handoffStep = 0;
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
            if (observation == "target.auditorium.handoff_circuit" && _xiaoyuHoldingExitLever)
            {
                if (_handoffStep >= 2)
                {
                    _handoffStep = 3;
                    ShowToast(DreamPrologueText.Format("DREAM_V6_UI_ESCAPECONTROLLER_010"));
                    PushTransientState();
                }
                else ShowFailure(DreamEscapeIds.PuzzleExit, "DREAM_FEEDBACK_FINAL_PANELS_INCOMPLETE");
                return;
            }
            var result = _session.ObserveDreamTarget(observation);
            ApplyAndRefresh(result);
            var clue = ObservationClue(observation);
            if (_session.State.DreamEscape!.DiscoveredClues.Contains(clue)) OpenClueReading(clue);
            return;
        }
        if (targetId == "target.classroom.clock")
        {
            var installed = UseAvailableItems(targetId, showResult: false);
            var state = _session.State.DreamEscape!;
            if (!state.InstalledParts.Contains(DreamEscapeIds.SlotClassroomClockHand) ||
                !state.InstalledParts.Contains(DreamEscapeIds.SlotClassroomClockKey))
            {
                if (installed.Changed) ShowResult(installed);
                else ShowFailure(DreamEscapeIds.PuzzleClockRestore, "DREAM_FEEDBACK_CLOCK_PARTS_MISSING");
                return;
            }
            _mechanism.Open(DreamEscapeIds.PuzzleClockRestore, DreamMechanismMode.Clock);
            return;
        }
        if (targetId == "target.classroom.duty_roster")
        {
            var installed = UseAvailableItems(targetId, showResult: false);
            if (!_session.State.DreamEscape!.InstalledParts.Contains(DreamEscapeIds.SlotClassroomRosterSlider))
            {
                if (installed.Changed) ShowResult(installed);
                else ShowFailure(DreamEscapeIds.PuzzleRosterSlider, "DREAM_FEEDBACK_ROSTER_SLIDER_MISSING");
                return;
            }
            _mechanism.Open(DreamEscapeIds.PuzzleRosterSlider, DreamMechanismMode.Roster);
            return;
        }
        if (targetId == "target.music.metronome")
        {
            UseAvailableItems(targetId);
            return;
        }
        if (targetId == "assembly:archive_fuse")
        {
            ApplyAndRefresh(_session.AssembleDreamFuse(), DreamEscapeIds.ProgressFuseAssembly);
            return;
        }
        if (targetId == "target.archive.recorder")
        {
            UseAvailableItems(targetId);
            return;
        }
        if (targetId is "target.auditorium.line_set" or "target.auditorium.projector")
        {
            UseAvailableItems(targetId, showResult: false);
            OpenPuzzle("auditorium_stage_alignment");
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
            ShowToast(DreamPrologueText.Format("DREAM_V6_UI_ESCAPECONTROLLER_015"));
            return;
        }
        if (targetId == "action:left_lever")
        {
            if (_xiaoyuHoldingExitLever)
            {
                ResetCooperation();
                ShowToast(DreamPrologueText.Format("DREAM_V6_UI_ESCAPECONTROLLER_016"));
                return;
            }
            var result = _session.PrepareDreamLeftLever();
            if (result.FeedbackKey == "DREAM_FEEDBACK_XIAOYU_READY")
            {
                _xiaoyuHoldingExitLever = true;
                _handoffStep = 1;
            }
            PushTransientState();
            ApplyAndRefresh(result, DreamEscapeIds.PuzzleExit);
            return;
        }
        if (targetId == "action:handoff_monitor")
        {
            if (!_xiaoyuHoldingExitLever) { ShowFailure(DreamEscapeIds.PuzzleExit, "DREAM_FEEDBACK_XIAOYU_NOT_READY"); return; }
            _handoffStep = Math.Max(_handoffStep, 2);
            ShowToast(DreamPrologueText.Format("DREAM_V6_UI_ESCAPECONTROLLER_018"));
            PushTransientState();
            return;
        }
        if (targetId == "action:open_exit")
        {
            if (!_xiaoyuHoldingExitLever || _handoffStep != 3)
            {
                ShowFailure(DreamEscapeIds.PuzzleExit, "DREAM_FEEDBACK_FINAL_PANELS_INCOMPLETE");
                return;
            }
            var result = _session.OpenDreamExit();
            ShowResult(result, DreamEscapeIds.PuzzleExit);
            if (result.Changed)
            {
                _xiaoyuHoldingExitLever = false;
                _handoffStep = 0;
                PushTransientState();
                _ = FinishOpenedExitAsync();
            }
            return;
        }

        UseAvailableItems(targetId);
    }

    private DreamActionResult UseAvailableItems(string targetId, bool showResult = true)
    {
        var result = _session.UseAvailableDreamItems(targetId);
        if (showResult) ShowResult(result, PuzzleForTarget(targetId) ?? targetId switch
        {
            "target.music.metronome" => DreamEscapeIds.ProgressMetronomeReady,
            "target.auditorium.master_player" => "master_player",
            "target.corridor.room_two_door" or "target.corridor.room_three_door" or "target.corridor.room_four_door" => "door",
            _ => null
        });
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
                ShowResult(result, "door");
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
            ShowToast(DreamPrologueText.Format("DREAM_V6_UI_ESCAPECONTROLLER_020"));
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
        var puzzleId = PuzzleForTarget("puzzle:" + shortId)
            ?? throw new InvalidDataException($"Unknown puzzle target: {shortId}");
        var dream = _session.State.DreamEscape!;
        if (dream.SolvedPuzzles.Contains(puzzleId)) return;
        if (puzzleId == DreamEscapeIds.PuzzleMusicCage)
            _session.ObserveDreamTarget("target.music.door_voice");
        if (puzzleId == DreamEscapeIds.PuzzleMusicChannelCalibration && dream.XiaoyuJoined)
            _session.ObserveDreamTarget("target.music.side_wave");
        if (puzzleId == DreamEscapeIds.PuzzleAuditoriumHandoffPlan)
            _session.ObserveDreamTarget("target.auditorium.handoff_circuit");
        if (puzzleId == DreamEscapeIds.PuzzleAuditoriumStageAlignment)
        {
            _mechanism.Open(puzzleId, DreamMechanismMode.StageAlignment);
            return;
        }
        if (puzzleId == DreamEscapeIds.PuzzleMusicPianoRoll)
        {
            _mechanism.Open(puzzleId, DreamMechanismMode.PaperRoll);
            return;
        }
        if (puzzleId == DreamEscapeIds.PuzzleMusicBackstagePhoto)
        {
            if (!dream.Items.IsSupersetOf([DreamEscapeIds.ItemClassPhotoNegative, DreamEscapeIds.ItemAttendanceOverlay, DreamEscapeIds.ItemRedFilter]))
            { ShowFailure(puzzleId, "DREAM_FEEDBACK_NEED_SEAT_COORDINATES"); return; }
            _mechanism.Open(puzzleId, DreamMechanismMode.PhotoOverlay);
            return;
        }
        if (puzzleId == DreamEscapeIds.PuzzleWindowLatches)
        { _mechanism.Open(puzzleId, DreamMechanismMode.WindowLatches); return; }
        var digits = Enumerable.Range(0, 10).Select(value => (value.ToString(), value.ToString())).ToArray();
        IReadOnlyList<(string, string)> choices = puzzleId switch
        {
            DreamEscapeIds.PuzzleTeacherDesk => digits,
            DreamEscapeIds.PuzzleMusicCage => digits[1..5],
            DreamEscapeIds.PuzzleMusicChannelCalibration or DreamEscapeIds.PuzzleMusicAcousticWall => InstrumentChoices(),
            DreamEscapeIds.PuzzleArchiveRetrieval => DrawerChoices(),
            DreamEscapeIds.PuzzleArchiveWorkTimeline => [("open", DreamPrologueText.Format("DREAM_V6_UI_ESCAPECONTROLLER_022")), ("calibrate", DreamPrologueText.Format("DREAM_V6_UI_ESCAPECONTROLLER_023")), ("score", DreamPrologueText.Format("DREAM_V6_UI_ESCAPECONTROLLER_024")), ("backstage", DreamPrologueText.Format("DREAM_V6_UI_ESCAPECONTROLLER_025"))],
            DreamEscapeIds.PuzzleAuditoriumTapeReconstruction => TapeChoices(),
            DreamEscapeIds.PuzzleAuditoriumHandoffPlan => [("lights", DreamPrologueText.Format("DREAM_V6_UI_ESCAPECONTROLLER_026")), ("position", DreamPrologueText.Format("DREAM_V6_UI_ESCAPECONTROLLER_027")), ("release", DreamPrologueText.Format("DREAM_V6_UI_ESCAPECONTROLLER_028")), ("monitor", DreamPrologueText.Format("DREAM_V6_UI_ESCAPECONTROLLER_029"))],
            _ => throw new InvalidDataException($"No panel for {puzzleId}")
        };
        var count = puzzleId is DreamEscapeIds.PuzzleTeacherDesk or DreamEscapeIds.PuzzleAuditoriumTapeReconstruction ? 3 : 4;
        _puzzle.Open(puzzleId, DreamPrologueText.Puzzle(puzzleId), DreamPrologueText.Prompt(puzzleId), choices, count,
            puzzleId == DreamEscapeIds.PuzzleTeacherDesk);
    }

    private void SubmitPuzzle(string puzzleId, string[] answer)
    {
        _areaNode?.GetNodeOrNull<PlayerController3D>("Player")?.PlayInteractionPose("operate");
        var result = _session.SubmitDreamPuzzle(puzzleId, answer);
        ApplyAndRefresh(result, puzzleId);
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
        if (result.FeedbackKey.StartsWith("DREAM_HINT_", StringComparison.Ordinal))
            ShowToast(HintText(result.FeedbackKey));
        else ShowResult(result);
    }

    private void OpenClueReading(string clueId)
    {
        var panel = new ReadingPanel { Name = "ClueReading" };
        _interfaceRoot.AddChild(panel);
        panel.Open(DreamPrologueText.Format("DREAM_V6_UI_ESCAPECONTROLLER_030"), DreamInventoryPanel.ClueText(clueId), DreamInventoryPanel.ClueImage(clueId), panel.QueueFree);
    }

    private void ApplyAndRefresh(DreamActionResult result, string? context = null)
    {
        ShowResult(result, context);
        if (result.Changed)
        {
            _inventory.Refresh();
            _area?.RefreshState();
        }
        RefreshHud();
    }

    private void ShowFailure(string context, string feedbackKey) =>
        ShowResult(DreamActionResult.NoChange(feedbackKey), context);

    private void ShowResult(DreamActionResult result, string? context = null)
    {
        var thought = DreamThoughtRules.Select(_session.State.DreamEscape!, context, result);
        if (thought is null) { ShowToast(DreamPrologueText.Get(result.FeedbackKey)); return; }
        var selectionId = string.Join('|', thought.TextKeys);
        var index = _thoughtVariants.GetValueOrDefault(selectionId);
        var key = thought.TextKeys[index % thought.TextKeys.Count];
        _thoughtVariants[selectionId] = (index + 1) % thought.TextKeys.Count;
        ShowToast(DreamPrologueText.Format("DREAM_V6_UI_PLAYER_THOUGHT", _session.State.Player.Name, DreamPrologueText.Get(key)), 5.5);
    }

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

    private static IReadOnlyList<(string, string)> InstrumentChoices() =>
        [("woodblock", DreamPrologueText.Format("DREAM_V6_UI_ESCAPECONTROLLER_031")), ("piano", DreamPrologueText.Format("DREAM_V6_UI_ESCAPECONTROLLER_032")), ("triangle", DreamPrologueText.Format("DREAM_V6_UI_ESCAPECONTROLLER_033")), ("handbell", DreamPrologueText.Format("DREAM_V6_UI_ESCAPECONTROLLER_034"))];

    private static IReadOnlyList<(string, string)> DrawerChoices() =>
        [("A4", "A4"), ("B3", "B3"), ("C1", "C1"), ("D2", "D2")];

    private static IReadOnlyList<(string, string)> TapeChoices() =>
        [("classroom", DreamPrologueText.Format("DREAM_V6_UI_ESCAPECONTROLLER_035")), ("music", DreamPrologueText.Format("DREAM_V6_UI_ESCAPECONTROLLER_036")), ("archive", DreamPrologueText.Format("DREAM_V6_UI_ESCAPECONTROLLER_037"))];

    private static string AreaName(string id) => id switch
    {
        DreamEscapeIds.AreaCorridor => DreamPrologueText.Format("DREAM_V6_UI_ESCAPECONTROLLER_038"), DreamEscapeIds.AreaClassroom => DreamPrologueText.Format("DREAM_V6_UI_ESCAPECONTROLLER_039"),
        DreamEscapeIds.AreaMusic => DreamPrologueText.Format("DREAM_V6_UI_ESCAPECONTROLLER_040"), DreamEscapeIds.AreaArchive => DreamPrologueText.Format("DREAM_V6_UI_ESCAPECONTROLLER_041"),
        DreamEscapeIds.AreaAuditorium => DreamPrologueText.Format("DREAM_V6_UI_ESCAPECONTROLLER_042"), _ => id
    };

    public static string HintText(string key) => DreamPrologueText.Hint(key);

    private static string? PuzzleForTarget(string targetId) => targetId switch
    {
        "puzzle:window_latches" => DreamEscapeIds.PuzzleWindowLatches,
        "target.classroom.blackboard" => DreamEscapeIds.PuzzleBlackboard,
        "target.classroom.clock" => DreamEscapeIds.PuzzleClockRestore,
        "target.classroom.duty_roster" => DreamEscapeIds.PuzzleRosterSlider,
        "puzzle:teacher_desk" => DreamEscapeIds.PuzzleTeacherDesk,
        "target.classroom.map_cabinet" => DreamEscapeIds.PuzzleMapCabinet,
        "target.music.score" => DreamEscapeIds.PuzzleMusicScore,
        "puzzle:music_cage" => DreamEscapeIds.PuzzleMusicCage,
        "puzzle:music_channel_calibration" => DreamEscapeIds.PuzzleMusicChannelCalibration,
        "puzzle:music_piano_roll" => DreamEscapeIds.PuzzleMusicPianoRoll,
        "puzzle:music_acoustic_wall" => DreamEscapeIds.PuzzleMusicAcousticWall,
        "puzzle:music_backstage_photo" => DreamEscapeIds.PuzzleMusicBackstagePhoto,
        "puzzle:archive_retrieval" => DreamEscapeIds.PuzzleArchiveRetrieval,
        "target.archive.recorder" => DreamEscapeIds.PuzzleArchiveRecorderRebuild,
        "puzzle:archive_work_timeline" => DreamEscapeIds.PuzzleArchiveWorkTimeline,
        "puzzle:auditorium_stage_alignment" or "target.auditorium.line_set" or "target.auditorium.projector" => DreamEscapeIds.PuzzleAuditoriumStageAlignment,
        "puzzle:auditorium_tape_reconstruction" => DreamEscapeIds.PuzzleAuditoriumTapeReconstruction,
        "puzzle:auditorium_handoff_plan" => DreamEscapeIds.PuzzleAuditoriumHandoffPlan,
        "action:open_exit" => DreamEscapeIds.PuzzleExit,
        _ => null
    };

    private static string ObservationClue(string target) => target switch
    {
        "target.classroom.maintenance_card" => DreamEscapeIds.ClueClassroomWindow,
        "target.classroom.morning_schedule" => DreamEscapeIds.ClueClassroomClock,
        "target.classroom.duty_note" => DreamEscapeIds.ClueClassroomDutyRoster,
        "target.music.door_voice" => DreamEscapeIds.ClueDoorMapping,
        "target.music.side_wave" => DreamEscapeIds.ClueMusicSideWave,
        "target.music.tempo_notice" => DreamEscapeIds.ClueMusicTempoNotice,
        "target.music.fork_reference" => DreamEscapeIds.ClueMusicFrontWaves,
        "target.music.roll_template" => DreamEscapeIds.ClueMusicRollTemplate,
        "target.archive.registry_rule" => DreamEscapeIds.ClueArchiveRegistryRule,
        "target.archive.registry_choir" => DreamEscapeIds.ClueArchiveRegistryChoir,
        "target.archive.registry_broadcast" => DreamEscapeIds.ClueArchiveRegistryBroadcast,
        "target.archive.registry_art" => DreamEscapeIds.ClueArchiveRegistryArt,
        "target.archive.registry_track" => DreamEscapeIds.ClueArchiveRegistryTrack,
        "target.archive.fuse_route" => DreamEscapeIds.ClueArchiveFuseRoute,
        "target.auditorium.line_set_notches" => DreamEscapeIds.ClueStageFrontFrame,
        "target.auditorium.projection_order" => DreamEscapeIds.ClueStageSideFrame,
        "target.auditorium.tape_order" => DreamEscapeIds.ClueTapeSeams,
        "target.auditorium.handoff_circuit" => DreamEscapeIds.ClueHandoffCircuit,
        _ => throw new InvalidDataException($"Unknown observation {target}")
    };

    private void OnPuzzlePreviewChanged(string puzzleId, string[] values)
    {
        _previewPuzzleId = puzzleId;
        _previewValues = values;
        PushTransientState();
    }

    private void OnPuzzlePreviewClosed(string puzzleId)
    {
        ResetCooperation();
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
                _xiaoyuHoldingExitLever, _previewPuzzleId, _previewValues, _handoffStep));
        }
    }

    private void ResetCooperation()
    {
        _xiaoyuHoldingExitLever = false;
        _handoffStep = 0;
        PushTransientState();
    }
}
