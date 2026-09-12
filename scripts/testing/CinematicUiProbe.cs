using System.Globalization;
using System.Reflection;
using System.Text.Json;
using Godot;
using TownRpg.Application;
using TownRpg.Core.Domain;
using TownRpg.Core.Services;
using TownRpg.Presentation;

namespace TownRpg.Testing;

/// <summary>Four live UI samples, real font layout and native input checks. Persistence is suppressed.</summary>
public partial class CinematicUiProbe : Node
{
    private MainController _main = null!;
    private DreamEscapeController _dream = null!;
    private GameSession _session = null!;
    private string? _output;
    private int _width;
    private float _menuScale;
    private float _subtitleScale;
    private int _captures;
    private readonly List<object> _layouts = [];
    private const BindingFlags PrivateInstance = BindingFlags.Instance | BindingFlags.NonPublic;
    private static object? Invoke(object target, string method, params object?[] args) =>
        target.GetType().GetMethod(method, PrivateInstance)!.Invoke(target, args);
    private static T Field<T>(object target, string name) => (T)target.GetType().GetField(name, PrivateInstance)!.GetValue(target)!;
    private static IEnumerable<T> Find<T>(Node root) where T : Node => UiUxCapture.Find<T>(root);
    private static Button Button(Node root, string name) => Find<Button>(root).Single(b => b.Name == name);
    private static void Require(bool condition, string message) { if (!condition) throw new InvalidOperationException(message); }
    private async Task Frames(int count = 6) { for (var i = 0; i < count; i++) await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame); }

    public override async void _Ready()
    {
        try
        {
            var args = OS.GetCmdlineUserArgs();
            _output = args.FirstOrDefault(a => a.StartsWith("--output="))?[9..];
            _width = int.Parse(args.FirstOrDefault(a => a.StartsWith("--width="))?[8..] ?? "1280", CultureInfo.InvariantCulture);
            _menuScale = float.Parse(args.FirstOrDefault(a => a.StartsWith("--scale="))?[8..] ?? "1", CultureInfo.InvariantCulture);
            _subtitleScale = float.Parse(args.FirstOrDefault(a => a.StartsWith("--subtitle-scale="))?[17..] ?? _menuScale.ToString(CultureInfo.InvariantCulture), CultureInfo.InvariantCulture);
            _session = GetNode<GameSession>("/root/GameSession");
            using var suppression = _session.SuppressPersistence();
            _session.NewGame("小岚", "同学");
            _session.State.Settings.FontScale = _menuScale; _session.State.Settings.SubtitleFontScale = _subtitleScale;
            _session.State.Settings.WindowWidth = _width; _session.State.Settings.WindowHeight = _width * 9 / 16;
            _session.State.Settings.BorderlessFullscreen = _width == 1920;
            _session.State.Settings.ReduceFlashing = true;
            DisplaySettingsController.Apply(_session.State.Settings);
            MarkStoriesComplete();
            if (_output is not null) Directory.CreateDirectory(_output);
            _main = ResourceLoader.Load<PackedScene>("res://scenes/app/Main.tscn").Instantiate<MainController>(); AddChild(_main);
            Invoke(_main, "CloseTitle"); Invoke(_main, "LoadDream", false);
            _dream = Find<DreamEscapeController>(_main).Single(); _dream.ApplySettings(_session.State.Settings);
            await Frames(30);
            if (!args.Contains("--capture-only")) await CheckBehavior();
            await CaptureSamples();
            if (_output is not null)
                File.WriteAllText(Path.Combine(_output, "layout.json"), JsonSerializer.Serialize(_layouts, new JsonSerializerOptions { WriteIndented = true }));
            _main.QueueFree(); await Frames(10);
            GC.Collect(); GC.WaitForPendingFinalizers(); await Frames(4);
            GD.Print($"CINEMATIC_UI_PASS width={_width} menu={_menuScale} subtitles={_subtitleScale} captures={_captures} mode={(args.Contains("--capture-only") ? "layout" : "input+layout")}");
            GetTree().Quit();
        }
        catch (Exception error) { GD.PushError(error.ToString()); GetTree().Quit(1); }
    }

    private void MarkStoriesComplete()
    {
        _session.State.CompletedEvents.Add(DreamPrologueCatalog.EventId("intro"));
        _session.State.CompletedEvents.UnionWith(DreamPrologueCatalog.Groups.Select(g => DreamPrologueCatalog.EventId(g.Id)));
    }

    private async Task Key(Key key)
    {
        Input.ParseInputEvent(new InputEventKey { Keycode = key, PhysicalKeycode = key, Pressed = true }); await Frames(2);
        Input.ParseInputEvent(new InputEventKey { Keycode = key, PhysicalKeycode = key, Pressed = false }); await Frames(3);
    }
    private async Task Pad(JoyButton button)
    {
        Input.ParseInputEvent(new InputEventJoypadButton { ButtonIndex = button, Pressed = true }); await Frames(2);
        Input.ParseInputEvent(new InputEventJoypadButton { ButtonIndex = button, Pressed = false }); await Frames(3);
    }
    private async Task Click(Control target)
    {
        var position = target.GetGlobalRect().GetCenter();
        GetViewport().PushInput(new InputEventMouseMotion { Position = position }, true);
        GetViewport().PushInput(new InputEventMouseButton { Position = position, ButtonIndex = MouseButton.Left, Pressed = true }, true);
        await Frames(2);
        GetViewport().PushInput(new InputEventMouseButton { Position = position, ButtonIndex = MouseButton.Left, Pressed = false }, true);
        await Frames(3);
    }
    private static async Task ExpectCancelled(Task task)
    {
        try { await task; throw new InvalidOperationException("Cancelled dialogue unexpectedly completed."); }
        catch (OperationCanceledException) { }
    }
    private async Task Drain(Task task, DialoguePanel panel)
    {
        for (var i = 0; i < 80 && !task.IsCompleted; i++)
        { Button(panel, "DialogueContinue").EmitSignal(BaseButton.SignalName.Pressed); await Frames(2); }
        Require(task.IsCompleted, "Dialogue pagination did not terminate."); await task;
    }

    private async Task CheckBehavior()
    {
        var panel = Find<DialoguePanel>(_dream).Single();
        using (var lease = _session.Modal.Acquire("cinematic-dialogue-test"))
        {
            GD.Print("CINEMATIC_CHECK tools_history");
            var line = panel.ShowLineAsync("ui.probe.cancel", "小岚", DreamPrologueText.Get("DREAM_V6_STORY_INTRO_002"));
            await Frames();
            Require(_session.Modal.IsBlocked && !line.IsCompleted, "Dialogue did not wait for its reader.");
            var avatar = Find<PlayerController3D>(_dream).Single(); var origin = avatar.GlobalPosition;
            await Key(Godot.Key.W);
            Require(avatar.GlobalPosition.DistanceTo(origin) < .01f, "Dialogue allowed world movement.");
            await Key(Godot.Key.H);
            var tools = Field<PanelContainer>(panel, "_tools"); var auto = Find<CheckButton>(panel).Single(b => b.Name == "DialogueAuto");
            Require(tools.Visible && GetViewport().GuiGetFocusOwner() == auto, "H did not open and focus dialogue tools.");
            auto.ButtonPressed = true;
            panel._Process(9); await Frames(); Require(!line.IsCompleted, "Tools did not pause auto advance.");
            await Pad(JoyButton.Y); Require(!tools.Visible, "Y did not close dialogue tools.");
            await Pad(JoyButton.Y); Require(tools.Visible, "Y did not reopen dialogue tools.");
            await Click(Button(panel, "DialogueHistory"));
            var history = Find<ReadingPanel>(panel).Single(); Require(history.Visible, "Mouse could not open dialogue history.");
            panel._Process(9); await Frames(); Require(!line.IsCompleted, "History did not pause auto advance.");
            await Pad(JoyButton.B);
            Require(!history.Visible && tools.Visible && GetViewport().GuiGetFocusOwner() == Button(panel, "DialogueHistory"), "History return lost its tool focus.");
            await Pad(JoyButton.B); Require(!tools.Visible && panel.Visible, "B should close tools before cancelling dialogue.");
            if (GetWindow().HasFocus())
            { panel._Process(9); await Frames(); Require(line.IsCompletedSuccessfully, "Auto did not resume after tools closed."); }
            else { auto.ButtonPressed = false; await Drain(line, panel); GD.Print("CINEMATIC_AUTO_RESUME_NOT_RUN window_without_focus"); }
            auto.ButtonPressed = false;
            GD.Print("CINEMATIC_CHECK cancel_retry");
            line = panel.ShowLineAsync("ui.probe.cancel.unread", "小岚", "取消后仍可重新阅读。"); await Frames();
            await Key(Godot.Key.Escape); await ExpectCancelled(line);
            Require(!_session.State.ReadDialogueLines.Contains("ui.probe.cancel.unread"), "Cancelled line was marked as read.");
            line = panel.ShowLineAsync("ui.probe.cancel.unread", "小岚", "取消后仍可重新阅读。"); await Frames();
            await Pad(JoyButton.A); Require(line.IsCompletedSuccessfully, "A did not advance the retried line.");

            var skip = Find<CheckButton>(panel).Single(b => b.Name == "DialogueSkipRead"); skip.ButtonPressed = true;
            GD.Print("CINEMATIC_CHECK skip_read");
            line = panel.ShowLineAsync("ui.probe.cancel.unread", "小岚", "取消后仍可重新阅读。"); await Frames();
            Require(line.IsCompletedSuccessfully, "Skip-read did not skip a read line.");
            line = panel.ShowLineAsync("ui.probe.new.unread", "小岚", "第一次读到的文字不能被跳过。"); await Frames();
            Require(!line.IsCompleted, "Skip-read skipped unread content."); skip.ButtonPressed = false; await Key(Godot.Key.Enter);
            Require(line.IsCompletedSuccessfully, $"Enter did not advance unread line; focus={GetViewport().GuiGetFocusOwner()?.Name}."); await line;

            var longText = string.Join("", Enumerable.Repeat(DreamPrologueText.Get("DREAM_V6_STORY_TIMELINE_001"), 5));
            GD.Print("CINEMATIC_CHECK long_choice");
            var choice = panel.ShowChoiceAsync("ui.probe.long.choice", "小岚", longText, ["继续查看。", "暂时离开。"]); await Frames();
            Require(!Field<PanelContainer>(panel, "_choiceFrame").Visible, "Long prompt exposed choices before all pages were read.");
            await Key(Godot.Key.Enter);
            var pageBefore = Field<int>(panel, "_pageIndex");
            _session.State.Settings.SubtitleBackground = SubtitleBackground.HighContrast;
            _session.State.Settings.FontScale = 2;
            await Frames();
            Require(Field<int>(panel, "_pageIndex") == pageBefore && !choice.IsCompleted,
                "Background or menu size change reset active subtitle pagination.");
            _session.State.Settings.SubtitleBackground = SubtitleBackground.SoftShadow;
            _session.State.Settings.FontScale = _menuScale;
            for (var i = 0; i < 50 && !Field<PanelContainer>(panel, "_choiceFrame").Visible; i++)
            { AssertSubtitle(panel); await Key(Godot.Key.Enter); }
            Require(Field<PanelContainer>(panel, "_choiceFrame").Visible, "Choices never appeared after prompt pages.");
            await Pad(JoyButton.DpadDown);
            var choiceFocus = GetViewport().GuiGetFocusOwner();
            await Pad(JoyButton.Y); await Pad(JoyButton.B);
            Require(GetViewport().GuiGetFocusOwner() == choiceFocus, "Tools lost the selected choice focus.");
            await Pad(JoyButton.A);
            Require(choice.IsCompletedSuccessfully && await choice == 1, "Gamepad choice selected the wrong response.");
            panel.CloseDialogue();

            _session.State.Settings.ReduceFlashing = false;
            line = panel.ShowLineAsync("ui.probe.fade", "小岚", "短淡入与阅读等待。"); await Frames(24);
            Require(Find<SubtitleView>(panel).First().Modulate.A >= .99f && !line.IsCompleted, "Fade did not finish independently of advance.");
            panel.CloseDialogue(); await ExpectCancelled(line); _session.State.Settings.ReduceFlashing = true;
            line = panel.ShowLineAsync("ui.probe.long.name", "一二三四五六七八", longText); await Frames();
            _session.State.Settings.SubtitleFontScale = 2; await Frames(); AssertSubtitle(panel);
            var nameLabel = Find<Label>(panel).Single(l => l.Name == "SubtitleSpeaker");
            Require(nameLabel.GetLineCount() <= 2 && nameLabel.GetMinimumSize().Y <= nameLabel.Size.Y + 1,
                "Long speaker name clipped after live subtitle resizing.");
            Require(!line.IsCompleted && Field<int>(panel, "_pageIndex") == 0, "Resizing skipped unread text.");
            panel.CloseDialogue(); await ExpectCancelled(line); _session.State.Settings.SubtitleFontScale = _subtitleScale;
        }
        Require(!_session.Modal.IsBlocked, "Dialogue test leaked its modal lease.");
        GD.Print("CINEMATIC_CHECK settings");
        await CheckSettings();
        GD.Print("CINEMATIC_INPUT_PASS mouse_history H Y B Enter A pagination unread retry auto tools focus fade settings");
    }

    private async Task CheckSettings()
    {
        _session.State.Settings.FontScale = 1.5f; _session.State.Settings.SubtitleFontScale = null;
        Invoke(_main, "OpenPause"); Invoke(_main, "OpenSettings", false); await Frames();
        var menu = Find<OptionButton>(_main).Single(b => b.Name == "SettingsFontScale");
        menu.EmitSignal(OptionButton.SignalName.ItemSelected, 0L); await Frames();
        Require(_session.State.Settings.FontScale == 1 && _session.State.Settings.EffectiveSubtitleFontScale == 1.5f,
            "Changing menu size also changed an inherited old subtitle preference.");
        var subtitle = Find<OptionButton>(_main).Single(b => b.Name == "SettingsSubtitleFontScale");
        subtitle.EmitSignal(OptionButton.SignalName.ItemSelected, 3L); await Frames();
        Require(_session.State.Settings.FontScale == 1 && _session.State.Settings.EffectiveSubtitleFontScale == 2,
            "Subtitle size failed to remain independent of menu text.");
        var background = Find<OptionButton>(_main).Single(b => b.Name == "SettingsSubtitleBackground");
        background.EmitSignal(OptionButton.SignalName.ItemSelected, 2L);
        var preview = Find<SubtitleSettingsPreview>(_main).Single(); preview.GrabFocus(); await Frames();
        Require(Find<SubtitleView>(preview).Single().TextSize == 18 && _session.State.Settings.SubtitleBackground == SubtitleBackground.HighContrast,
            "Live subtitle preview does not reflect settings.");
        Invoke(_main, "ClosePause"); await Frames();
        _session.State.Settings.FontScale = _menuScale; _session.State.Settings.SubtitleFontScale = _subtitleScale;
        _session.State.Settings.SubtitleBackground = SubtitleBackground.SoftShadow; Invoke(_main, "ApplySettingsVisuals");
    }

    private void AssertSubtitle(DialoguePanel panel)
    {
        var subtitle = Find<SubtitleView>(panel).First(); var label = subtitle.TextControl;
        Require(label.GetLineCount() <= 2, "Subtitle exceeded two rendered lines.");
        Require(label.GetContentHeight() <= label.Size.Y + 1, $"Subtitle height clipped: {label.GetContentHeight()} > {label.Size.Y}.");
        foreach (var line in label.Text.Split('\n'))
            Require(UiStyles.CinematicFont.GetStringSize(line, fontSize: subtitle.TextSize).X <= label.Size.X + 1, "Subtitle width clipped.");
        var bounds = subtitle.GetGlobalRect();
        Require(bounds.Position.X >= 0 && bounds.End.X <= 640 && bounds.Position.Y >= 0 && bounds.End.Y <= 326,
            "Subtitle escaped its reading safe area.");
    }

    private async Task CaptureSamples()
    {
        var inventory = Find<DreamInventoryPanel>(_dream).Single(); var state = _session.State.DreamEscape!;
        var camera = Find<ThirdPersonCameraRig>(_dream).Single(); var avatar = Find<PlayerController3D>(_dream).Single();
        avatar.GlobalPosition = new Vector3(0, 0, 0); camera.SetYawImmediate(90); camera.SnapToTarget();
        await Capture("01-exploration");
        var marker = Find<InteractionMarker3D>(_dream).Single(m => m.TargetId == "observe:target.classroom.maintenance_card");
        avatar.GlobalPosition = new Vector3(marker.GlobalPosition.X - .25f, 0, marker.GlobalPosition.Z); camera.SnapToTarget();
        await Frames(15);
        Require(Find<WorldArea3D>(_dream).Single().CurrentInteractionTargetId == marker.TargetId,
            $"Exploration fixture did not focus paper card: target={Find<WorldArea3D>(_dream).Single().CurrentInteractionTargetId} avatar={avatar.GlobalPosition} marker={marker.GlobalPosition} blocked={_session.Modal.IsBlocked}.");
        await Capture("02-interaction-prompt");
        state.Items.Clear(); inventory.Refresh(); inventory.Open(); await Capture("03-inventory-empty"); inventory.ClosePanel();
        state.Items.UnionWith(DreamEscapeIds.Items); inventory.Refresh(); inventory.Open(); await Frames();
        var grid = Find<ItemGridView>(inventory).Single(); var selected = grid.SelectionControl!;
        var gridScroll = Find<ScrollContainer>(grid).First();
        Require(selected.GetGlobalRect().Position.Y >= gridScroll.GetGlobalRect().Position.Y - 1, "Initial inventory focus clipped the first row.");
        await Capture("04-inventory");
        var lastItem = Find<Button>(inventory).Last(b => b.Name.ToString().StartsWith("Item_"));
        lastItem.GrabFocus(); await Frames(); await Capture("05-inventory-last-row");
        inventory.ClosePanel();
        state.DiscoveredClues.Clear(); inventory.Refresh(); inventory.Open(DreamInventoryPage.Clues); await Capture("06-records-empty"); inventory.ClosePanel();
        state.DiscoveredClues.UnionWith(DreamEscapeIds.Clues); state.XiaoyuJoined = true; inventory.Refresh(); inventory.Open(DreamInventoryPage.Clues); await Frames();
        var reader = Find<ArchiveReader>(inventory).Single();
        var longest = reader.Entries.MaxBy(e => e.Text.Length)!; reader.Select(longest.Id); await Capture("07-records-long"); inventory.ClosePanel();
        var panel = Find<DialoguePanel>(_dream).Single();
        using (var lease = _session.Modal.Acquire("cinematic-sample"))
        {
            var line = panel.ShowLineAsync("ui.sample.intro", "小岚", DreamPrologueText.Get("DREAM_V6_STORY_INTRO_002"));
            await Frames(); AssertSubtitle(panel); await Capture("08-dialogue-bright");
            await Key(Godot.Key.H); await Capture("09-dialogue-tools");
            await Click(Button(panel, "DialogueHistory")); await Capture("10-dialogue-history");
            await Pad(JoyButton.B); await Pad(JoyButton.B);
            panel.CloseDialogue(); await ExpectCancelled(line);
            // Real Legacy narration provides a longer sentence than the short prologue lines.
            line = panel.ShowLineAsync("ui.sample.legacy.narration", "旁白", "她的语气很平静。你们之间那场没有说清的争执，也安静地横在这句问候里。");
            await Frames(); AssertSubtitle(panel); await Capture("11-dialogue-two-lines"); panel.CloseDialogue(); await ExpectCancelled(line);
            // Existing Legacy choice text, exercised here without committing any story outcome.
            var choice = panel.ShowChoiceAsync("ui.sample.legacy.choice", "小岚", "你先做了什么？", ["拉开窗帘，确认声音的方向。", "再安静地听一会儿。"]);
            await Capture("12-dialogue-choice"); panel.CloseDialogue(); await ExpectCancelled(choice);
            foreach (var (background, name) in new[] { (SubtitleBackground.None, "13-dialogue-no-background"), (SubtitleBackground.HighContrast, "14-dialogue-high-contrast") })
            {
                _session.State.Settings.SubtitleBackground = background;
                line = panel.ShowLineAsync("ui.sample.background", "小岚", DreamPrologueText.Get("DREAM_V6_STORY_INTRO_002"));
                await Capture(name); panel.CloseDialogue(); await ExpectCancelled(line);
            }
        }
        _session.State.Settings.SubtitleBackground = SubtitleBackground.SoftShadow;
        state.UnlockedAreas.Add(DreamEscapeIds.AreaMusic);
        _session.MoveDreamToArea(DreamEscapeIds.AreaMusic, DreamAreaSpawnCatalog.MusicDoorEntryStored.X, DreamAreaSpawnCatalog.MusicDoorEntryStored.Y);
        Invoke(_dream, "LoadArea", DreamEscapeIds.AreaMusic); await Frames(15);
        camera = Find<ThirdPersonCameraRig>(_dream).Single(); avatar = Find<PlayerController3D>(_dream).Single();
        avatar.GlobalPosition = new Vector3(0, 0, 2); camera.SetYawImmediate(0); camera.SnapToTarget();
        using (var lease = _session.Modal.Acquire("cinematic-dark-sample"))
        {
            var line = panel.ShowLineAsync("ui.sample.meeting", "林晓雨", DreamPrologueText.Get("DREAM_V6_STORY_MEETING_005"));
            await Capture("15-dialogue-music"); panel.CloseDialogue(); await ExpectCancelled(line);
        }
        // Low-light legibility stress on a duplicated fixture environment; scene assets are never edited.
        var environmentNode = Find<WorldEnvironment>(_dream).Single();
        environmentNode.Environment = (Godot.Environment)environmentNode.Environment.Duplicate();
        environmentNode.Environment.AmbientLightEnergy = .08f;
        foreach (var light in Find<Light3D>(_dream)) light.LightEnergy *= .12f;
        using (var lease = _session.Modal.Acquire("cinematic-low-light-sample"))
        {
            var line = panel.ShowLineAsync("ui.sample.low.light", "林晓雨", DreamPrologueText.Get("DREAM_V6_STORY_MEETING_005"));
            await Capture("17-dialogue-low-light"); panel.CloseDialogue(); await ExpectCancelled(line);
        }
        Invoke(_main, "OpenPause"); Invoke(_main, "OpenSettings", false); await Frames();
        Find<SubtitleSettingsPreview>(_main).Single().GrabFocus(); await Capture("16-settings-preview");
        Invoke(_main, "ClosePause"); await Frames();
    }

    private async Task Capture(string name)
    {
        await Frames(15);
        foreach (var panel in Find<DialoguePanel>(_main).Where(p => p.Visible)) AssertSubtitle(panel);
        foreach (var frame in Find<PanelContainer>(_main).Where(p => p.Name == "ManagementFrame" && p.IsVisibleInTree()))
            Require(frame.GetGlobalRect().Position.X >= 0 && frame.GetGlobalRect().End.X <= 640 && frame.GetGlobalRect().End.Y <= 360,
                $"{name}: management surface exceeded the viewport.");
        _layouts.Add(new { name, controls = Find<Control>(_main).Where(c => c.IsVisibleInTree() && c is Label or RichTextLabel or Godot.Button or PanelContainer)
            .Select(c => new { path = c.GetPath().ToString(), rect = c.GetGlobalRect().ToString(), minimum = c.GetCombinedMinimumSize().ToString(), font = c.GetThemeDefaultFontSize() }).ToArray() });
        if (_output is null) return;
        await ToSignal(RenderingServer.Singleton, RenderingServer.SignalName.FramePostDraw);
        using var image = GetViewport().GetTexture().GetImage();
        Require(image.GetWidth() == _width && image.GetHeight() == _width * 9 / 16, "Screenshot size does not match requested resolution.");
        var error = image.SavePng(Path.Combine(_output, name + ".png"));
        if (error != Error.Ok) throw new IOException(error.ToString());
        _captures++;
    }
}
