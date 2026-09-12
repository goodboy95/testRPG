using Godot;
using TownRpg.Application;
using TownRpg.Core.Domain;
using TownRpg.Core.Services;
using TownRpg.Presentation;

namespace TownRpg.Testing;

/// <summary>Opt-in rendered UI fixtures. Never reads or writes the player's save.</summary>
public partial class UiUxCapture : Node
{
    private string _directory = "";
    private float _scale = 1;
    private int _width = 1280;
    private int _captures;
    private HashSet<string>? _only;
    public override async void _Ready()
    {
        try
        {
            var args = OS.GetCmdlineUserArgs();
            _directory = args.FirstOrDefault(a => a.StartsWith("--output="))?.Replace("--output=", "") ?? "outputs/ui-remake/matrix";
            _scale = float.Parse(args.FirstOrDefault(a => a.StartsWith("--scale="))?.Replace("--scale=", "") ?? "1", System.Globalization.CultureInfo.InvariantCulture);
            _width = int.Parse(args.FirstOrDefault(a => a.StartsWith("--width="))?.Replace("--width=", "") ?? "1280");
            var only = args.FirstOrDefault(a => a.StartsWith("--only="));
            _only = only is null ? null : only[7..].Split(',').ToHashSet(StringComparer.Ordinal);
            Directory.CreateDirectory(_directory);
            var session = GetNode<GameSession>("/root/GameSession");
            using var suppression = session.SuppressPersistence();
            session.NewGame("界面验收", "同学");
            SetSettings(session);
            var main = ResourceLoader.Load<PackedScene>("res://scenes/app/Main.tscn").Instantiate<MainController>();
            AddChild(main);
            await Capture("title");
            Find<Button>(main).First(b => b.Text == "开始新游戏").EmitSignal(BaseButton.SignalName.Pressed);
            await Capture("name-entry");
            var nameInput = Find<LineEdit>(main).First(); nameInput.Text = "一二三四五六七八九";
            Find<Button>(main).First(b => b.Text == "开始暑假").EmitSignal(BaseButton.SignalName.Pressed);
            await Capture("name-entry-error"); nameInput.Text = "小岚";
            Find<Button>(main).First(b => b.Text == "开始暑假").EmitSignal(BaseButton.SignalName.Pressed);
            session.State.CompletedEvents.Add(DreamPrologueCatalog.EventId("intro"));
            SetSettings(session);
            Invoke(main, "ApplySettingsVisuals");
            await Frames();
            Find<AcceptDialog>(main).First(d => d.Name == "EntryControlReminder").GetOkButton().EmitSignal(BaseButton.SignalName.Pressed);
            await Capture("exploration");
            var inventory = Find<DreamInventoryPanel>(main).Single();
            session.State.DreamEscape!.Items.Clear(); inventory.Refresh(); inventory.Open(); await Capture("inventory-empty"); inventory.ClosePanel();
            foreach (var item in session.Content.Items.Items) session.State.DreamEscape!.Items.Add(item.Id);
            inventory.Refresh();
            inventory.Open();
            await Capture("inventory");
            inventory.ClosePanel();
            inventory.Open(DreamInventoryPage.Clues); await Capture("records-empty"); inventory.ClosePanel();
            session.State.DreamEscape!.DiscoveredClues.UnionWith(DreamEscapeIds.Clues);
            session.State.DreamEscape.XiaoyuJoined = true; inventory.Refresh(); inventory.Open(DreamInventoryPage.Clues); await Capture("records"); inventory.ClosePanel();
            inventory.Open(DreamInventoryPage.Xiaoyu); await Capture("xiaoyu"); inventory.ClosePanel();
            var mechanism = Find<DreamMechanismPanel>(main).Single();
            mechanism.Open(DreamEscapeIds.PuzzleClockRestore, DreamMechanismMode.Clock);
            await Capture("mechanism");
            mechanism.ClosePanel();
            foreach (var (id, mode, name) in new[] {
                (DreamEscapeIds.PuzzleWindowLatches, DreamMechanismMode.WindowLatches, "latches"),
                (DreamEscapeIds.PuzzleRosterSlider, DreamMechanismMode.Roster, "roster"),
                (DreamEscapeIds.PuzzleMusicPianoRoll, DreamMechanismMode.PaperRoll, "paper"),
                (DreamEscapeIds.PuzzleMusicBackstagePhoto, DreamMechanismMode.PhotoOverlay, "photo"),
                (DreamEscapeIds.PuzzleAuditoriumStageAlignment, DreamMechanismMode.StageAlignment, "stage") })
            {
                mechanism.Open(id, mode); await Capture(name);
                if (mode == DreamMechanismMode.PhotoOverlay)
                {
                    for (var i = 0; i < 3; i++) Find<Button>(mechanism).Single(b => b.Name == "MechanismValue" + i).EmitSignal(BaseButton.SignalName.Pressed);
                    await Capture("photo-layers");
                }
                if (mode == DreamMechanismMode.StageAlignment)
                {
                    Find<Button>(mechanism).Single(b => b.Text == UiText.Get("UI_SIDE_VIEW")).EmitSignal(BaseButton.SignalName.Pressed);
                    await Capture("stage-side");
                }
                mechanism.ClosePanel();
            }
            var puzzle = Find<DreamPuzzlePanel>(main).Single();
            puzzle.Open(DreamEscapeIds.PuzzleMusicCage, DreamPrologueText.Puzzle(DreamEscapeIds.PuzzleMusicCage), DreamPrologueText.Prompt(DreamEscapeIds.PuzzleMusicCage), [("1", "1"), ("2", "2"), ("3", "3"), ("4", "4")], 4);
            await Capture("puzzle");
            puzzle.ClosePanel();
            puzzle.Open(DreamEscapeIds.PuzzleMusicChannelCalibration, DreamPrologueText.Puzzle(DreamEscapeIds.PuzzleMusicChannelCalibration), DreamPrologueText.Prompt(DreamEscapeIds.PuzzleMusicChannelCalibration), [("woodblock", "木鱼"), ("piano", "钢琴"), ("triangle", "三角铁"), ("handbell", "手铃")], 4);
            await Capture("calibration"); puzzle.ClosePanel();
            var dialogue = Find<DialoguePanel>(main).Last();
            using var fixtureDialogueLease = session.Modal.Acquire("capture-dialogue");
            var line = dialogue.ShowLineAsync("ui.fixture.line", "林晓雨", "先看看窗边留下的痕迹吧。也许能找到打开暗格的方法。");
            await Capture("dialogue");
            dialogue.CloseDialogue();
            try { await line; } catch (TaskCanceledException) { }
            var choice = dialogue.ShowChoiceAsync("ui.fixture.choice", "林晓雨", "先从哪里看起？", ["检查窗边留下的痕迹", "再看看教室里其他东西", "稍后再调查"]);
            await Capture("choice"); dialogue.CloseDialogue(); try { await choice; } catch (TaskCanceledException) { }
            fixtureDialogueLease.Dispose();
            await SendKey(Godot.Key.Escape);
            await Capture("pause");
            Find<Button>(main).First(b => b.Name == "PauseSettings").EmitSignal(BaseButton.SignalName.Pressed);
            await Capture("settings");
            var settingsShell = Find<ManagementMenuShell>(main).Single(s => s.Name == "SettingsShell");
            settingsShell.Select("controls"); await Capture("settings-controls"); settingsShell.Select("accessibility"); await Capture("settings-accessibility");
            Invoke(main, "ClosePause");
            var layer = new CanvasLayer { Layer = 50 }; AddChild(layer);
            var ui = new Control { Theme = UiStyles.CreateTheme(_scale) }; layer.AddChild(ui); ui.SetAnchorsAndOffsetsPreset(Control.LayoutPreset.FullRect);
            var journal = new JournalPanel(); ui.AddChild(journal); journal.Open(session, "林晓雨\n先看看窗边留下的痕迹吧。"); await Capture("legacy-journal"); journal.ClosePanel();
            var battle = new BattlePanel(); ui.AddChild(battle); battle.Open(new ResonanceBattle()); await Capture("legacy-battle");
            battle.ApplyResult(new BattleTurnResult { Defeated = true, Resolve = 0, Resonance = 4, Turn = 6, Message = "暂时退下，重新整理呼吸。" }); await Capture("legacy-retry"); battle.ClosePanel();
            var reading = new ReadingPanel(); ui.AddChild(reading); reading.Open("工作记录", string.Join("\n\n", Enumerable.Repeat("纸页上的痕迹尚未消失。已经发现的资料可以反复阅读。", 12))); await Capture("reading"); reading.ClosePanel();
            var confirm = new ConfirmationDialog { Title = "返回标题", DialogText = "是否返回标题？尚未保存的进度可能丢失。", OkButtonText = "返回标题", CancelButtonText = "取消" }; ui.AddChild(confirm);
            UiStyles.Confirm(confirm, _scale); await Capture("confirmation"); confirm.Hide();
            layer.QueueFree(); await Frames();
            Invoke(main, "ShowDreamMechanicsComplete"); await Capture("completion");
            GD.Print($"UI_UX_CAPTURE_PASS files={_captures} width={_width} scale={_scale}");
            main.QueueFree(); await Frames();
            GetTree().Quit();
        }
        catch (Exception e) { GD.PushError(e.ToString()); GetTree().Quit(1); }
    }
    private async Task Frames() { for (var i = 0; i < 20; i++) await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame); }
    private async Task Capture(string name)
    {
        if (_only is not null && !_only.Contains(name)) return;
        await Frames();
        await ToSignal(RenderingServer.Singleton, RenderingServer.SignalName.FramePostDraw);
        var image = GetViewport().GetTexture().GetImage();
        if (image.GetWidth() != _width || image.GetHeight() != _width * 9 / 16)
            throw new InvalidOperationException($"Capture resolution is {image.GetWidth()}×{image.GetHeight()}, requested {_width}×{_width * 9 / 16}.");
        var error = image.SavePng(Path.Combine(_directory, name + ".png"));
        if (error != Error.Ok) throw new IOException(error.ToString());
        _captures++;
        File.WriteAllText(Path.Combine(_directory, name + ".layout.txt"), string.Join("\n", Find<Control>(this)
            .Where(c => c.IsVisibleInTree() && (c is Container || c is Button))
            .Select(c => $"{c.GetPath()} {c.GetGlobalRect()} min={c.GetCombinedMinimumSize()}")));
    }
    private void SetSettings(GameSession session)
    {
        session.State.Settings.FontScale = _scale; session.State.Settings.WindowWidth = _width;
        session.State.Settings.WindowHeight = _width * 9 / 16; session.State.Settings.BorderlessFullscreen = _width == 1920;
        DisplaySettingsController.Apply(session.State.Settings);
    }
    private static void Invoke(MainController main, string method) => typeof(MainController)
        .GetMethod(method, System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance)!.Invoke(main, null);
    private async Task SendKey(Godot.Key key)
    {
        Input.ParseInputEvent(new InputEventKey { Keycode = key, PhysicalKeycode = key, Pressed = true });
        await Frames();
        Input.ParseInputEvent(new InputEventKey { Keycode = key, PhysicalKeycode = key, Pressed = false });
        await Frames();
    }
    public static IEnumerable<T> Find<T>(Node node) where T : Node => node.GetChildren()
        .Where(n => !n.IsQueuedForDeletion()).SelectMany(n => (n is T t ? new[] { t } : []).Concat(Find<T>(n)));
}
