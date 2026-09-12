using System.Reflection;
using Godot;
using TownRpg.Application;
using TownRpg.Core.Domain;
using TownRpg.Core.Services;
using TownRpg.Presentation;
using static TownRpg.Core.Domain.DreamEscapeIds;

namespace TownRpg.Testing;

/// <summary>Actual controller failure routing plus optional rendered UI matrix; never writes player saves.</summary>
public partial class DreamThoughtProbe : Node
{
    private DreamEscapeController _controller = null!;
    private string? _output;
    private int _width = 1280;
    private int _captures;
    private const BindingFlags PrivateInstance = BindingFlags.NonPublic | BindingFlags.Instance;
    private object? Invoke(string method, params object?[] args) => typeof(DreamEscapeController).GetMethod(method, PrivateInstance)!.Invoke(_controller, args);
    private T Field<T>(string name) => (T)typeof(DreamEscapeController).GetField(name, PrivateInstance)!.GetValue(_controller)!;
    private static void Require(bool value, string error) { if (!value) throw new InvalidOperationException(error); }
    private async Task Frames(int count = 4) { for (var i = 0; i < count; i++) await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame); }
    private static Button Button(Node root, string name) => UiUxCapture.Find<Button>(root).Single(b => b.Name == name);

    public override async void _Ready()
    {
        try
        {
            var session = GetNode<GameSession>("/root/GameSession");
            using var suppression = session.SuppressPersistence();
            session.NewGame("小岚", "同学");
            var state = session.State.DreamEscape!;
            session.State.CompletedEvents.Add(DreamPrologueCatalog.EventId("intro"));
            var args = OS.GetCmdlineUserArgs();
            _output = args.FirstOrDefault(a => a.StartsWith("--output="))?[9..];
            _width = int.Parse(args.FirstOrDefault(a => a.StartsWith("--width="))?[8..] ?? "1280");
            var scale = float.Parse(args.FirstOrDefault(a => a.StartsWith("--scale="))?[8..] ?? "1", System.Globalization.CultureInfo.InvariantCulture);
            session.State.Settings.FontScale = scale;
            if (_output is not null)
            {
                Directory.CreateDirectory(_output);
                session.State.Settings.WindowWidth = _width; session.State.Settings.WindowHeight = _width * 9 / 16;
                session.State.Settings.BorderlessFullscreen = _width == 1920;
                DisplaySettingsController.Apply(session.State.Settings);
            }
            _controller = ResourceLoader.Load<PackedScene>("res://scenes/dream/DreamEscape.tscn").Instantiate<DreamEscapeController>();
            AddChild(_controller); _controller.SetProcess(false); _controller.ApplySettings(session.State.Settings);
            await Frames(12);
            Input.MouseMode = Input.MouseModeEnum.Visible;
            foreach (var camera in UiUxCapture.Find<ThirdPersonCameraRig>(_controller)) camera.SetInputActive(false);
            var mechanism = UiUxCapture.Find<DreamMechanismPanel>(_controller).Single();
            var sequence = UiUxCapture.Find<DreamPuzzlePanel>(_controller).Single();
            var inventory = UiUxCapture.Find<DreamInventoryPanel>(_controller).Single();
            var toast = Field<Label>("_toast");
            if (args.Contains("--music-only"))
            {
                await MusicMatrix(session, state, mechanism, sequence, inventory, toast);
                GD.Print($"MUSIC_UI_PROBE_PASS width={_width} scale={scale} captures={_captures} door_observation preview interrupted records hints no_references");
                _controller.QueueFree(); await Frames(8);
                GC.Collect(); GC.WaitForPendingFinalizers(); await Frames(4);
                GetTree().Quit(); return;
            }

            // Submit through the real panel event, after discovery, and ensure exactly one feedback.
            Invoke("HandleInteraction", "puzzle:window_latches");
            var before = SaveGameSerializer.Serialize(session.State);
            var version = Field<int>("_toastVersion");
            Button(mechanism, "MechanismConfirm").EmitSignal(BaseButton.SignalName.Pressed);
            Require(Field<int>("_toastVersion") == version + 1 && toast.Text.StartsWith("小岚："), "Failure did not produce one named thought.");
            Require(before == SaveGameSerializer.Serialize(session.State), "Failed submission changed state.");
            var first = toast.Text;
            await Capture("thought-none");
            Invoke("SubmitPuzzle", PuzzleWindowLatches, new[] { "middle", "middle", "middle" });
            Require(toast.Text != first && !toast.Text.Contains("维护卡"), "Unseen material leaked or repeat did not vary.");
            await Capture("thought-repeat");
            state.DiscoveredClues.Add(ClueClassroomWindow);
            Invoke("SubmitPuzzle", PuzzleWindowLatches, new[] { "middle", "middle", "middle" });
            Require(toast.Text.Contains("维护卡") && !toast.Text.Contains("上、下、中"), "Known material recall is missing or gives the answer.");
            await Capture("thought-known");

            state.DiscoveredClues.Add(ClueClassroomClock);
            version = Field<int>("_toastVersion");
            Invoke("HandleInteraction", "target.classroom.clock");
            Require(Field<int>("_toastVersion") == version + 1 && toast.Text.Contains("作息") && !toast.Text.Contains("钥匙"), "Partial knowledge guessed an unseen tool or emitted twice.");
            await Capture("thought-partial");
            state.Items.Add(ItemClockHand);
            version = Field<int>("_toastVersion");
            Invoke("HandleInteraction", "target.classroom.clock");
            Require(Field<int>("_toastVersion") == version + 1 && state.InstalledParts.Contains(SlotClassroomClockHand) && !state.Items.Contains(ItemClockHand), "Partial installation feedback duplicated or lost the actual installation.");
            state.Items.Add(ItemClockKey); state.DiscoveredClues.Add(ClueClassroomDutyRoster);
            Invoke("HandleInteraction", "target.classroom.clock");
            Require(mechanism.Visible && state.InstalledParts.Contains(SlotClassroomClockKey), "Automatic installation no longer opens the clock.");
            before = SaveGameSerializer.Serialize(session.State);
            Button(mechanism, "MechanismConfirm").EmitSignal(BaseButton.SignalName.Pressed);
            Require(before == SaveGameSerializer.Serialize(session.State), "Complete-material failure changed progress.");
            await Capture("thought-complete");

            state.UnlockedAreas.Add(AreaMusic);
            session.MoveDreamToArea(AreaMusic, DreamAreaSpawnCatalog.MusicDoorEntryStored.X, DreamAreaSpawnCatalog.MusicDoorEntryStored.Y);
            Invoke("LoadArea", AreaMusic);
            Invoke("HandleInteraction", "puzzle:music_cage");
            await Frames(); AssertWorkspace(sequence);
            Require(!state.XiaoyuJoined && state.DiscoveredClues.Contains(ClueDoorMapping) &&
                UiUxCapture.Find<Label>(sequence).Any(label => label.IsVisibleInTree() && label.Text.Contains("门后的女孩") && label.Text.Contains("一号是圆形")),
                "Door girl's necessary observation was removed with references.");
            toast.Visible = false;
            await Capture("door-observation"); sequence.ClosePanel();
            state.XiaoyuJoined = true;
            session.MoveDreamToArea(AreaCorridor, 320, 280);
            state.UnlockedAreas.Add(AreaArchive);
            session.MoveDreamToArea(AreaArchive, 320, 280);
            Invoke("LoadArea", AreaArchive);
            state.Items.Add(ItemCalibrationReel);
            version = Field<int>("_toastVersion");
            Invoke("HandleInteraction", "target.archive.recorder");
            Require(Field<int>("_toastVersion") == version + 1 && state.InstalledParts.Contains(SlotArchiveRecorderReel), $"Recorder partial install failed: feedbacks={Field<int>("_toastVersion") - version}, text={toast.Text}");
            session.MoveDreamToArea(AreaClassroom, DreamEscapeState.NewGameClassroomPositionX, DreamEscapeState.NewGameClassroomPositionY);
            Invoke("LoadArea", AreaClassroom);
            state.XiaoyuJoined = true;
            before = SaveGameSerializer.Serialize(session.State);
            Invoke("SubmitPuzzle", PuzzleWindowLatches, new[] { "middle", "middle", "middle" });
            Require(before == SaveGameSerializer.Serialize(session.State) && !toast.Text.Contains("维护卡"), "Joined automatic failure advanced help or recited material guidance.");
            await Capture("thought-joined");
            state.DiscoveredPuzzles.Clear(); state.DiscoveredPuzzles.Add(PuzzleAuditoriumStageAlignment);
            for (var i = 0; i < 3; i++) Invoke("RequestHint", PuzzleAuditoriumStageAlignment);
            Require(state.HintLevels[PuzzleAuditoriumStageAlignment] == 3 && toast.Text.StartsWith("林晓雨：“") && !toast.Text.Contains("3/1/4/2"), "Third-level help lost its speaker or reveals a complete answer.");
            toast.Visible = false;
            inventory.Open(DreamInventoryPage.Xiaoyu); await Capture("xiaoyu");
            foreach (var scroll in UiUxCapture.Find<ScrollContainer>(inventory)) scroll.ScrollVertical = int.MaxValue;
            await Capture("xiaoyu-help"); inventory.ClosePanel();
            state.DiscoveredClues.UnionWith(Clues);
            inventory.Open(DreamInventoryPage.Clues); await Frames();
            Require(UiUxCapture.Find<ArchiveReader>(inventory).Any(), "Independent records were removed.");
            await Capture("records"); inventory.ClosePanel();

            foreach (var (id, mode, name) in new[]
            {
                (PuzzleWindowLatches, DreamMechanismMode.WindowLatches, "latches"),
                (PuzzleClockRestore, DreamMechanismMode.Clock, "clock"),
                (PuzzleRosterSlider, DreamMechanismMode.Roster, "roster"),
                (PuzzleMusicPianoRoll, DreamMechanismMode.PaperRoll, "paper"),
                (PuzzleMusicBackstagePhoto, DreamMechanismMode.PhotoOverlay, "photo"),
                (PuzzleAuditoriumStageAlignment, DreamMechanismMode.StageAlignment, "stage")
            })
            {
                mechanism.Open(id, mode); await Frames(); AssertWorkspace(mechanism);
                if (mode == DreamMechanismMode.PhotoOverlay)
                    for (var i = 0; i < 3; i++) Button(mechanism, "MechanismValue" + i).EmitSignal(BaseButton.SignalName.Pressed);
                await Capture(name);
                if (mode == DreamMechanismMode.StageAlignment)
                {
                    UiUxCapture.Find<Button>(mechanism).Single(b => b.Text == UiText.Get("UI_SIDE_VIEW")).EmitSignal(BaseButton.SignalName.Pressed);
                    await Capture("stage-side");
                }
                mechanism.ClosePanel();
            }
            sequence.Open(PuzzleMusicCage, DreamPrologueText.Puzzle(PuzzleMusicCage), DreamPrologueText.Prompt(PuzzleMusicCage), [("1", "1"), ("2", "2"), ("3", "3"), ("4", "4")], 4);
            await Frames(); AssertWorkspace(sequence); await Capture("sequence"); sequence.ClosePanel();
            sequence.Open(PuzzleMusicChannelCalibration, DreamPrologueText.Puzzle(PuzzleMusicChannelCalibration), DreamPrologueText.Prompt(PuzzleMusicChannelCalibration), [("woodblock", "木鱼"), ("piano", "钢琴"), ("triangle", "三角铁"), ("handbell", "手铃")], 4);
            await Frames(); AssertWorkspace(sequence); await Capture("calibration");
            UiUxCapture.Find<Button>(sequence).Single(b => b.Text == UiText.Get("UI_COOP_OBSERVATIONS")).EmitSignal(BaseButton.SignalName.Pressed);
            await Frames();
            Require(UiUxCapture.Find<DreamPuzzleDiagram>(sequence).Single().IsVisibleInTree(), "Necessary cooperative observation disappeared.");
            await Capture("calibration-observation"); sequence.ClosePanel();
            Require(!session.Modal.IsBlocked, "A modal lease leaked after closing workspaces.");
            GD.Print($"DREAM_THOUGHT_PROBE_PASS actual_failures partial_installs known_only variants no_mutations no_references width={_width} scale={scale} captures={_captures}");
            _controller.QueueFree(); await Frames(); GetTree().Quit();
        }
        catch (Exception e) { GD.PushError(e.ToString()); GetTree().Quit(1); }
    }

    private async Task MusicMatrix(GameSession session, DreamEscapeState state, DreamMechanismPanel mechanism,
        DreamPuzzlePanel sequence, DreamInventoryPanel inventory, Label toast)
    {
        session.State.CompletedEvents.UnionWith(DreamPrologueCatalog.Groups.Select(g => DreamPrologueCatalog.EventId(g.Id)));
        state.UnlockedAreas.Add(AreaMusic);
        session.MoveDreamToArea(AreaMusic, DreamAreaSpawnCatalog.MusicDoorEntryStored.X, DreamAreaSpawnCatalog.MusicDoorEntryStored.Y);
        Invoke("LoadArea", AreaMusic); await Frames(12);
        foreach (var camera in UiUxCapture.Find<ThirdPersonCameraRig>(_controller)) camera.SetInputActive(false);
        Invoke("HandleInteraction", "puzzle:music_cage"); await Frames(); AssertWorkspace(sequence);
        Require(!state.XiaoyuJoined && state.DiscoveredClues.Contains(ClueDoorMapping) &&
            UiUxCapture.Find<Label>(sequence).Any(l => l.IsVisibleInTree() && l.Text.Contains("一号是圆形")), "Music door observation missing.");
        toast.Visible=false; await Capture("01-door-observation"); sequence.ClosePanel();
        state.SolvedPuzzles.Add(PuzzleMusicCage); state.XiaoyuJoined=true;
        state.DiscoveredClues.UnionWith(Clues);
        var room=UiUxCapture.Find<DreamMusicRoomController3D>(_controller).Single();room.RefreshState();
        state.DiscoveredPuzzles.Add(PuzzleMusicChannelCalibration);
        sequence.Open(PuzzleMusicChannelCalibration, DreamPrologueText.Puzzle(PuzzleMusicChannelCalibration),
            DreamPrologueText.Prompt(PuzzleMusicChannelCalibration), [("woodblock","木鱼"),("piano","钢琴"),("triangle","三角铁"),("handbell","手铃")],4);
        await Frames(); AssertWorkspace(sequence); await Capture("02-calibration");
        UiUxCapture.Find<Button>(sequence).Single(b=>b.Text==UiText.Get("UI_COOP_OBSERVATIONS")).EmitSignal(BaseButton.SignalName.Pressed);
        await Frames(); Require(UiUxCapture.Find<DreamPuzzleDiagram>(sequence).Single().IsVisibleInTree(),"Music cooperative diagram missing.");
        await Capture("03-cooperative-observation");sequence.ClosePanel();
        Require(!session.Modal.IsBlocked,"Closing calibration leaked modal state.");
        foreach(var (id,mode,name) in new[]{(PuzzleMusicPianoRoll,DreamMechanismMode.PaperRoll,"04-paper"),
            (PuzzleMusicBackstagePhoto,DreamMechanismMode.PhotoOverlay,"05-photo-return")})
        {
            mechanism.Open(id,mode);await Frames();AssertWorkspace(mechanism);await Capture(name);mechanism.ClosePanel();
        }
        Invoke("HandleInteraction","puzzle:music_acoustic_wall");await Frames();AssertWorkspace(sequence);
        Require(sequence.Visible,"Acoustic mechanism did not open.");
        await Capture("06-acoustic");sequence.ClosePanel();
        for(var i=0;i<3;i++)Invoke("RequestHint",PuzzleMusicChannelCalibration);
        toast.Visible=false;inventory.Open(DreamInventoryPage.Xiaoyu);await Capture("07-xiaoyu-help");
        foreach(var scroll in UiUxCapture.Find<ScrollContainer>(inventory))scroll.ScrollVertical=int.MaxValue;
        await Capture("08-xiaoyu-help-bottom");inventory.ClosePanel();
        inventory.Open(DreamInventoryPage.Clues);await Frames();
        Require(UiUxCapture.Find<ArchiveReader>(inventory).Any(),"Music independent records missing.");
        await Capture("09-independent-records");inventory.ClosePanel();
        Require(!session.Modal.IsBlocked,"Music UI matrix leaked modal state.");
    }

    private static void AssertWorkspace(Control panel)
    {
        Require(!UiUxCapture.Find<ArchiveReader>(panel).Any() && !UiUxCapture.Find<Button>(panel).Any(b => b.Name == "PuzzleReferences"), "Removed reference UI remains.");
        foreach (var button in UiUxCapture.Find<Button>(panel).Where(b => b.IsVisibleInTree()))
            Require(button.GetGlobalRect().End.Y <= 360.5f && button.GetGlobalRect().End.X <= 640.5f, "Mechanism action exceeds the viewport: " + button.Name);
    }
    private async Task Capture(string name)
    {
        if (_output is null) return;
        await Frames(20);
        await ToSignal(RenderingServer.Singleton, RenderingServer.SignalName.FramePostDraw);
        using var image = GetViewport().GetTexture().GetImage();
        Require(image.GetWidth() == _width && image.GetHeight() == _width * 9 / 16, "Rendered resolution differs from the requested one.");
        Require(image.SavePng(Path.Combine(_output, name + ".png")) == Error.Ok, "Unable to save capture.");
        File.WriteAllText(Path.Combine(_output, name + ".layout.txt"), string.Join("\n", UiUxCapture.Find<Control>(_controller)
            .Where(c => c.IsVisibleInTree() && c is Label or Godot.Button).Select(c => $"{c.GetPath()} {c.GetGlobalRect()} min={c.GetCombinedMinimumSize()}")));
        _captures++;
    }
}
