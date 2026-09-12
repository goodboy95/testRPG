using Godot;
using TownRpg.Application;
using TownRpg.Core.Domain;
using TownRpg.Core.Services;
using TownRpg.Presentation;

namespace TownRpg.Testing;

/// <summary>Real plugin traversal and button controls; isolated from the player's persisted save.</summary>
public partial class PrologueV6Probe : Node
{
    [Export] public bool AutoCapture { get; set; }
    private Control _ui = null!;
    private DreamMechanismPanel _mechanism = null!;
    private string[] _preview = [];

    public override async void _Ready()
    {
        if (!AutoCapture) return;
        try { await Run(true); GetTree().Quit(); }
        catch (Exception error) { GD.PushError(error.ToString()); GetTree().Quit(1); }
    }

    public async Task Run(bool capture = false)
    {
        var session = GetNode<GameSession>("/root/GameSession");
        using var suppression = session.SuppressPersistence();
        session.NewGame("序章验证", "同学");
        var layer = new CanvasLayer(); AddChild(layer);
        _ui = new Control { Theme = UiStyles.CreateTheme() };
        _ui.SetAnchorsAndOffsetsPreset(Control.LayoutPreset.FullRect); layer.AddChild(_ui);
        _mechanism = new DreamMechanismPanel(); _ui.AddChild(_mechanism);
        _mechanism.PreviewChanged += (_, values) => _preview = values;
        string[]? submitted = null;
        _mechanism.Submitted += (_, values) => submitted = values;
        try
        {
            foreach (var definition in session.Content.Events.Events)
            {
                using var resource = ResourceLoader.Load<Resource>(definition.DialoguePath);
                Require(resource is not null && resource.Get("lines").AsGodotDictionary().Count > 0,
                    "Dialogue import failed: " + definition.DialoguePath);
            }
            var dream = session.State.DreamEscape!;
            dream.SolvedPuzzles.UnionWith(DreamEscapeIds.Puzzles);
            dream.XiaoyuJoined = true;
            var story = new DreamStoryPlayer(); AddChild(story); story.Configure(_ui);
            using (var cancellation = new CancellationTokenSource())
            {
                var cancelled = story.PlayPendingAsync(cancellation.Token);
                await Frames(3);
                Require(story.Running && session.CompletedStoryCount() == 0, "Story committed before END.");
                cancellation.Cancel();
                try { await cancelled; throw new InvalidOperationException("Story ignored cancellation."); }
                catch (OperationCanceledException) { }
                Require(!session.Modal.IsBlocked && session.CompletedStoryCount() == 0,
                    "Cancelled story committed or retained its modal lease.");
            }
            var playback = story.PlayPendingAsync(CancellationToken.None);
            for (var frame = 0; frame < 1000 && !playback.IsCompleted; frame++)
            {
                await Frames(1);
                var next = UiUxCapture.Find<Button>(_ui).FirstOrDefault(button => button.IsVisibleInTree() &&
                    !button.Disabled && (button.Text.StartsWith("继续") || button.Text.StartsWith("下一页")));
                next?.EmitSignal(BaseButton.SignalName.Pressed);
            }
            Require(playback.IsCompleted, "Prologue dialogue traversal did not terminate.");
            await playback;
            foreach (var entry in session.Content.Events.Events.Where(e => e.Id.StartsWith("dream.prologue.", StringComparison.Ordinal)))
            {
                using var resource = ResourceLoader.Load<Resource>(entry.DialoguePath);
                Require(resource.Get("lines").AsGodotDictionary().Values.All(value =>
                    !value.AsGodotDictionary().ContainsKey("resource")), "Dialogue cache retains its own resource owner.");
            }
            Require(session.CompletedStoryCount() == 13 && !session.Modal.IsBlocked,
                "Not all thirteen stories completed and released input.");
            Require(session.State.World.Facts.Count == 0 && session.State.World.Evidence.Count == 0 &&
                    session.State.Investigation.Leads.Count == 0, "Dream stories wrote real-world truth.");

            dream.DiscoveredClues.UnionWith(DreamEscapeIds.Clues);
            _mechanism.Open(DreamEscapeIds.PuzzleMusicPianoRoll, DreamMechanismMode.PaperRoll);
            await Frames(2);
            Cycle(0, 1); Cycle(1, 1); Cycle(1, 1); Cycle(3, 1); Cycle(2, 1); Cycle(3, 1);
            Require(_preview.SequenceEqual(["2", "4", "1", "3"]), "Paper controls cannot form matching seams.");
            Input.ParseInputEvent(new InputEventJoypadButton { ButtonIndex = JoyButton.X, Pressed = true });
            Input.ParseInputEvent(new InputEventJoypadButton { ButtonIndex = JoyButton.X, Pressed = false });
            await Frames(2);
            Require(_preview.SequenceEqual(["2", "4", "3", "1"]), "Gamepad undo failed.");
            Cycle(2, 1); Cycle(3, 1);
            Submit();
            Require(submitted is not null && submitted.SequenceEqual(["2", "4", "1", "3"]), "Paper submission changed the arrangement.");

            _mechanism.Open(DreamEscapeIds.PuzzleMusicBackstagePhoto, DreamMechanismMode.PhotoOverlay);
            Require(ConfirmButton().Disabled, "Empty photo layers allow confirmation.");
            Cycle(0, 1); Cycle(1, 1); Cycle(2, 1); UiUxCapture.Find<Button>(_mechanism).Single(b => b.Name == "PhotoCandidate2").EmitSignal(BaseButton.SignalName.Pressed);
            Require(!ConfirmButton().Disabled && _preview.SequenceEqual(["C4"]), "Photo controls cannot reveal the side-stage position.");
            _mechanism.ClosePanel();

            _mechanism.Open(DreamEscapeIds.PuzzleAuditoriumStageAlignment, DreamMechanismMode.StageAlignment);
            Cycle(0, 2); Cycle(2, 3); Cycle(3, 1); Cycle(5, 1); Cycle(6, 2);
            Require(DreamStageRules.Observe(_preview).Aligned, "Stage preview disagrees with Core.");
            _mechanism.ClosePanel();
            Require(!session.Modal.IsBlocked, "Mechanism cancellation retained modal input.");

            if (capture)
            {
                var puzzle = new DreamPuzzlePanel(); _ui.AddChild(puzzle);
                foreach (var scale in new[] { 1f, 1.5f })
                {
                    session.State.Settings.FontScale = scale;
                    UiStyles.ApplyScale(_ui, scale);
                    _mechanism.Open(DreamEscapeIds.PuzzleRosterSlider, DreamMechanismMode.Roster);
                    Cycle(0, 2); Cycle(2, 1); await Capture("roster", scale);
                    _mechanism.ClosePanel();
                    _mechanism.Open(DreamEscapeIds.PuzzleMusicPianoRoll, DreamMechanismMode.PaperRoll);
                    Cycle(0, 1); Cycle(1, 1); Cycle(1, 1); Cycle(3, 1); Cycle(2, 1); Cycle(3, 1); await Capture("paper", scale);
                    _mechanism.ClosePanel();
                    _mechanism.Open(DreamEscapeIds.PuzzleMusicBackstagePhoto, DreamMechanismMode.PhotoOverlay);
                    Cycle(0, 1); Cycle(1, 1); Cycle(2, 1); UiUxCapture.Find<Button>(_mechanism).Single(b => b.Name == "PhotoCandidate2").EmitSignal(BaseButton.SignalName.Pressed); await Capture("photo", scale);
                    _mechanism.ClosePanel();
                    _mechanism.Open(DreamEscapeIds.PuzzleAuditoriumStageAlignment, DreamMechanismMode.StageAlignment);
                    Cycle(0, 2); Cycle(2, 3); Cycle(3, 1); Cycle(5, 1); Cycle(6, 2); await Capture("stage", scale);
                    _mechanism.ClosePanel();
                    foreach (var id in new[] { DreamEscapeIds.PuzzleMusicChannelCalibration, DreamEscapeIds.PuzzleArchiveRetrieval })
                    {
                        IReadOnlyList<(string, string)> choices = id == DreamEscapeIds.PuzzleArchiveRetrieval
                            ? [("A4", "A4"), ("B3", "B3"), ("C1", "C1"), ("D2", "D2")]
                            : [("woodblock", "木鱼"), ("piano", "钢琴"), ("triangle", "三角铁"), ("handbell", "手铃")];
                        puzzle.Open(id, DreamPrologueText.Puzzle(id), DreamPrologueText.Prompt(id), choices, 4);
                        await Capture(id == DreamEscapeIds.PuzzleArchiveRetrieval ? "registry" : "calibration", scale, puzzle);
                        puzzle.ClosePanel();
                    }
                }
            }
            GD.Print("PROLOGUE_V6_PASS groups=12 stories=13 imported=21 cancellation paper photo stage gamepad_undo");
        }
        finally { layer.QueueFree(); await Frames(1); }
    }

    private void Cycle(int index, int times)
    {
        var button = UiUxCapture.Find<Button>(_mechanism).Single(b => b.Name == "MechanismValue" + index);
        for (var count = 0; count < times; count++) button.EmitSignal(BaseButton.SignalName.Pressed);
    }
    private Button ConfirmButton() => UiUxCapture.Find<Button>(_mechanism).Single(b => b.Text == "确认");
    private void Submit() => ConfirmButton().EmitSignal(BaseButton.SignalName.Pressed);
    private async Task Frames(int count) { for (var i = 0; i < count; i++) await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame); }
    private static void Require(bool condition, string message) { if (!condition) throw new InvalidOperationException(message); }
    private async Task Capture(string name, float scale, Control? panel = null)
    {
        var surface = UiUxCapture.Find<PanelContainer>(panel ?? _mechanism).First(p => p.Name == "WorkspaceFrame");
        await Frames(15);
        await ToSignal(RenderingServer.Singleton, RenderingServer.SignalName.FramePostDraw);
        var directory = "outputs/prologue-v6/ui"; Directory.CreateDirectory(directory);
        GetViewport().GetTexture().GetImage().SavePng($"{directory}/{name}-{scale * 100:0}.png");
        Require(surface.GetGlobalRect().End.Y <= 360 && surface.GetGlobalRect().End.X <= 640,
            "Mechanism extends beyond the logical viewport.");
        Require(!UiUxCapture.Find<ArchiveReader>(panel ?? _mechanism).Any(), "Mechanism still embeds reference documents.");
    }
}

internal static class PrologueProbeExtensions
{
    public static int CompletedStoryCount(this GameSession session) =>
        session.State.CompletedEvents.Count(id => id.StartsWith("dream.prologue.", StringComparison.Ordinal));
}
