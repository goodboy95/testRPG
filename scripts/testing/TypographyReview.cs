using System.Globalization;
using System.Reflection;
using Godot;
using TownRpg.Application;
using TownRpg.Core.Domain;
using TownRpg.Core.Services;
using TownRpg.Presentation;

namespace TownRpg.Testing;

/// <summary>Native, isolated font comparison. The real panels run against a disposable in-memory session.</summary>
public partial class TypographyReview : Node
{
    private static readonly string[] Pages = ["对白", "选项", "事项", "跟踪", "背包", "记录", "机关", "设置"];
    private static readonly string[] Files = ["dialogue", "choices", "journal", "tracking", "inventory", "records", "mechanism", "settings"];
    private static readonly UiTypography?[] Profiles = [null, new(8, 9, 7, 12, 2, 18), new(9, 10, 7, 13, 3, 20), new(10, 11, 8, 14, 4, 22)];
    private GameSession Session => GetNode<GameSession>("/root/GameSession");
    private IDisposable? _suppression;
    private Control _root = null!;
    private Control _content = null!;
    private Camera3D _camera = null!;
    private ObjectiveTrackerView? _tracker;
    private DialoguePanel? _dialogue;
    private int _profile = 3, _page = 2;
    private float _menu = 1, _subtitle = 1;
    private int _width = 1280;
    private bool _switching;
    private string? _output;
    private bool _formal;
    private OptionButton _profilePicker = null!, _pagePicker = null!;
    private readonly List<object> _layouts = [];
    private const BindingFlags Private = BindingFlags.Instance | BindingFlags.NonPublic;
    private async Task Frames(int count = 6) { for (var i = 0; i < count; i++) await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame); }

    public override async void _Ready()
    {
        try
        {
            _suppression = Session.SuppressPersistence();
            Session.NewGame("小岚", "同学");
            Session.State.VillageExploration = new() { WakeIntroductionComplete = true };
            Session.VillageOpening.Initialize();
            Session.VillageOpening.Execute("accept_stool"); Session.VillageOpening.Execute("accept_orders");
            foreach (var h in VillageOpeningIds.Houses) Session.VillageOpening.Execute("pack", h);
            Session.VillageOpening.Track(OpeningJob.Orders, "receive_house13");
            Session.State.DreamEscape!.Items.UnionWith(Session.Content.Items.Items.Select(i => i.Id));
            Session.State.DreamEscape.DiscoveredClues.UnionWith(DreamEscapeIds.Clues);
            Session.State.DreamEscape.XiaoyuJoined = true;
            var args = OS.GetCmdlineUserArgs();
            _formal = args.Contains("--formal");
            _output = args.FirstOrDefault(a => a.StartsWith("--output="))?[9..];
            _width = int.Parse(args.FirstOrDefault(a => a.StartsWith("--width="))?[8..] ?? "1280");
            _menu = float.Parse(args.FirstOrDefault(a => a.StartsWith("--scale="))?[8..] ?? "1", CultureInfo.InvariantCulture);
            _subtitle = float.Parse(args.FirstOrDefault(a => a.StartsWith("--subtitle-scale="))?[17..] ?? _menu.ToString(CultureInfo.InvariantCulture), CultureInfo.InvariantCulture);
            SetResolution(); BuildBackdrop();
            var layer = new CanvasLayer { Layer = 100 }; AddChild(layer);
            _root = new Control { MouseFilter = Control.MouseFilterEnum.Ignore }; _root.SetAnchorsAndOffsetsPreset(Control.LayoutPreset.FullRect); layer.AddChild(_root);
            _content = new Control { MouseFilter = Control.MouseFilterEnum.Ignore };
            _content.SetAnchorsAndOffsetsPreset(Control.LayoutPreset.FullRect); _root.AddChild(_content);
            BuildToolbar();
            await ShowSample();
            if (_output is not null)
            {
                Directory.CreateDirectory(_output);
                for (_profile = _formal ? 3 : 0; _profile < Profiles.Length; _profile++)
                for (_page = 0; _page < Pages.Length; _page++)
                {
                    _profilePicker.Selected = _profile; _pagePicker.Selected = _page;
                    await ShowSample(); await Frames(10);
                    if (_page == 1 && _dialogue is not null)
                    {
                        for (var i = 0; i < 30 && !_dialogue.AwaitingChoice; i++)
                        {
                            UiUxCapture.Find<Button>(_dialogue).Single(b => b.Name == "DialogueContinue").EmitSignal(BaseButton.SignalName.Pressed);
                            await Frames(2);
                        }
                        if (!_dialogue.AwaitingChoice) throw new InvalidOperationException("Choice sample never reached its choices.");
                    }
                    await Capture();
                }
                File.WriteAllText(Path.Combine(_output, "layout.json"), System.Text.Json.JsonSerializer.Serialize(_layouts, new System.Text.Json.JsonSerializerOptions { WriteIndented = true }));
                _dialogue?.CloseDialogue(); _dialogue = null; _tracker = null;
                UiStyles.Clear(_content); await Frames(4);
                GC.Collect(); GC.WaitForPendingFinalizers(); await Frames(4);
                GD.Print($"TYPOGRAPHY_REVIEW_PASS width={_width} menu={_menu} subtitle={_subtitle} captures={_layouts.Count} formal={_formal}");
                GetTree().Quit();
            }
        }
        catch (Exception error) { GD.PushError(error.ToString()); GetTree().Quit(1); }
    }
    private void SetResolution()
    {
        var settings = Session.State.Settings;
        settings.WindowWidth = _width; settings.WindowHeight = _width * 9 / 16; settings.BorderlessFullscreen = _width == 1920;
        settings.FontScale = _menu; settings.SubtitleFontScale = _subtitle;
        DisplaySettingsController.Apply(settings);
    }
    private void BuildBackdrop()
    {
        var world = new Node3D(); AddChild(world);
        world.AddChild(new WorldEnvironment { Environment = new Godot.Environment { BackgroundMode = Godot.Environment.BGMode.Color, BackgroundColor = new("374741"), AmbientLightSource = Godot.Environment.AmbientSource.Color, AmbientLightColor = Colors.White, AmbientLightEnergy = .8f } });
        world.AddChild(new MeshInstance3D { Mesh = new PlaneMesh { Size = new(80, 80) }, MaterialOverride = new StandardMaterial3D { AlbedoColor = new("596454") } });
        foreach (var p in new[] { new Vector3(-7, 2, -6), new Vector3(7, 2, -12), new Vector3(0, 2, -23) })
            world.AddChild(new MeshInstance3D { Position = p, Mesh = new BoxMesh { Size = new(4, 4, 5) }, MaterialOverride = new StandardMaterial3D { AlbedoColor = new("857e67") } });
        _camera = new Camera3D { Position = new(0, 5, 10), Current = true }; world.AddChild(_camera); _camera.LookAt(new(0, 1, -10));
    }
    private void BuildToolbar()
    {
        var layer = new CanvasLayer { Layer = 200 }; AddChild(layer);
        var bar = new HBoxContainer { Position = new(6, 2) }; bar.Theme = UiStyles.CreateTheme(1, cinematic: true); layer.AddChild(bar);
        OptionButton Picker(string[] entries, int selected, Action<int> changed)
        {
            var option = new OptionButton { CustomMinimumSize = new(96, 20) }; option.AddThemeFontSizeOverride("font_size", 8);
            foreach (var text in entries) option.AddItem(text); option.Selected = selected;
            option.ItemSelected += i => { if (_switching) return; changed((int)i); _ = ShowSample(); }; bar.AddChild(option); return option;
        }
        _profilePicker = Picker(["原版对照", "A 紧凑 8/9", "B 均衡 9/10", "C 舒适 10/11"], _profile, i => _profile = i);
        _pagePicker = Picker(Pages, _page, i => _page = i);
        var scales = new[] { 1f, 1.25f, 1.5f, 2f };
        Picker(["菜单 100%", "菜单 125%", "菜单 150%", "菜单 200%"], Array.IndexOf(scales, _menu), i => _menu = scales[i]);
        Picker(["字幕 100%", "字幕 125%", "字幕 150%", "字幕 200%"], Array.IndexOf(scales, _subtitle), i => _subtitle = scales[i]);
        Picker(["1280×720", "1600×900", "1920×1080"], _width == 1920 ? 2 : _width == 1600 ? 1 : 0, i => { _width = new[] { 1280, 1600, 1920 }[i]; SetResolution(); });
    }
    private async Task ShowSample()
    {
        if (_switching) return; _switching = true;
        try
        {
            _dialogue?.CloseDialogue(); _dialogue = null; _tracker = null;
            foreach (var child in _content.GetChildren()) { _content.RemoveChild(child); child.QueueFree(); }
            await Frames(2);
            UiStyles.ReviewTypography = _formal ? null : Profiles[_profile];
            UiStyles.ReviewLegacyTypography = !_formal && _profile == 0;
            Session.State.Settings.FontScale = _menu; Session.State.Settings.SubtitleFontScale = _subtitle;
            _root.Theme = UiStyles.CreateTheme(_menu);
            switch (_page)
            {
                case 0: case 1:
                    _dialogue = new DialoguePanel(); _content.AddChild(_dialogue);
                    _ = DialogueSample(_dialogue, _page == 1); break;
                case 2:
                    if (_profile == 0)
                    { var old = new ReadingPanel { PlainText = true, ControllerScroll = true }; _content.AddChild(old); old.Open("周六委托", Session.VillageOpening.Journal()); }
                    else
                    { var journal = new OpeningJournalPanel { Service = Session.VillageOpening }; _content.AddChild(journal); journal.Open(); journal.Select(OpeningJob.Orders); }
                    break;
                case 3:
                    _tracker = new ObjectiveTrackerView(); _content.AddChild(_tracker); break;
                case 4: case 5:
                    var inventory = new DreamInventoryPanel(); _content.AddChild(inventory); inventory.Refresh(); inventory.Open(_page == 4 ? DreamInventoryPage.Items : DreamInventoryPage.Clues); break;
                case 6:
                    var mechanism = new DreamMechanismPanel(); _content.AddChild(mechanism); mechanism.Open(DreamEscapeIds.PuzzleClockRestore, DreamMechanismMode.Clock); break;
                case 7:
                    var main = new MainController(); _content.AddChild(main);
                    typeof(MainController).GetMethod("CloseTitle", Private)!.Invoke(main, null);
                    foreach (var layer in main.GetChildren().OfType<CanvasLayer>()) layer.Visible = false;
                    main.ProcessMode = ProcessModeEnum.Disabled;
                    var shell = new ManagementMenuShell(); _content.AddChild(shell); shell.SetTitle("显示与辅助设置");
                    Control Build(string name) => (Control)typeof(MainController).GetMethod(name, Private)!.Invoke(main, null)!;
                    shell.SetPages([new("display", "显示", "", () => true, () => Build("BuildDisplaySettings")), new("controls", "操作", "", () => true, () => Build("BuildControlSettings")), new("accessibility", "辅助功能", "", () => true, () => Build("BuildAccessibilitySettings"))]); break;
            }
        }
        finally { _switching = false; }
    }
    private static async Task DialogueSample(DialoguePanel panel, bool choices)
    {
        try
        {
            const string text = "没事，我不会生气。你先把手头的东西放稳，我们再看看该怎么搬。许婆婆的洗衣皂、陈叔的棉绳，还有刘伯的两条毛巾，都已经分好了。先送哪一家，你按顺路的来就行。";
            if (choices) await panel.ShowChoiceAsync("", "乔姨", text, ["我先送陈叔那一份，再去溪对面找刘伯。", "我想先核对一下许婆婆订的东西。", "等我把木凳送好，再回来拿货包。"]);
            else await panel.ShowLineAsync("", "乔姨", text);
        }
        catch (OperationCanceledException) { }
    }
    public override void _Process(double delta)
    {
        if (_tracker is not null) _tracker.ShowTarget(_camera, Vector3.Zero, new(7, 0, -12), "三户订货", "把货包送给陈叔", "陈叔家 · 收货处", _menu);
    }
    private async Task Capture()
    {
        await ToSignal(RenderingServer.Singleton, RenderingServer.SignalName.FramePostDraw);
        using var image = GetViewport().GetTexture().GetImage();
        if (image.GetWidth() != _width || image.GetHeight() != _width * 9 / 16) throw new InvalidOperationException("Unexpected screenshot resolution.");
        var name = $"{_profile}-{Files[_page]}";
        if (image.SavePng(Path.Combine(_output!, name + ".png")) != Error.Ok) throw new IOException("PNG capture failed.");
        var controls = UiUxCapture.Find<Control>(_content).Where(c => c.IsVisibleInTree()).ToArray();
        if (_profile > 0)
            foreach (var label in controls.OfType<Label>())
                if (!UiStyles.IsCinematic(label)) throw new InvalidOperationException("Sample did not inherit its typography: " + label.GetPath());
        var frames = controls.Where(c => c is UiModalSurface || c.Name == "ManagementFrame" || c.Name == "WorkspaceFrame").ToArray();
        foreach (var frame in frames)
            if (frame.GetGlobalRect().End.X > 641 || frame.GetGlobalRect().End.Y > 361) throw new InvalidOperationException("Sample frame exceeds the logical viewport: " + frame.GetPath());
        if (_profile > 0)
            foreach (var scroll in controls.OfType<ScrollContainer>())
            {
                if (scroll.VerticalScrollMode == ScrollContainer.ScrollMode.Disabled) continue;
                var last = UiUxCapture.Find<Button>(scroll).LastOrDefault(b => b.IsVisibleInTree());
                if (last is null) continue;
                var previous = scroll.ScrollVertical;
                scroll.EnsureControlVisible(last); await Frames(2);
                var bounds = scroll.GetGlobalRect(); var button = last.GetGlobalRect();
                if (button.Position.Y < bounds.Position.Y - 1 || button.End.Y > bounds.End.Y + 1)
                    throw new InvalidOperationException("Last action cannot be scrolled into view: " + last.GetPath());
                scroll.ScrollVertical = previous;
            }
        _layouts.Add(new { name, width = _width, menu = _menu, subtitle = _subtitle, controls = controls.Select(c => new { path = c.GetPath().ToString(), rect = c.GetGlobalRect().ToString(), min = c.GetCombinedMinimumSize().ToString(), fontSize = c.GetThemeFontSize("font_size"), font = c.GetThemeFont("font").ResourcePath }).ToArray() });
    }
    public override void _ExitTree() { _dialogue?.CloseDialogue(); UiStyles.ReviewTypography = null; UiStyles.ReviewLegacyTypography = false; _suppression?.Dispose(); }
}
