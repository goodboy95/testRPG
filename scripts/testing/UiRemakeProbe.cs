using Godot;
using TownRpg.Application;
using TownRpg.Core.Domain;
using TownRpg.Presentation;

namespace TownRpg.Testing;

/// <summary>Presentation transactions and native accept/cancel input, without save writes.</summary>
public partial class UiRemakeProbe : Node
{
    [Export] public bool AutoRun { get; set; }
    public override async void _Ready()
    {
        if (!AutoRun) return;
        try { await Run(); GetTree().Quit(); }
        catch (Exception e) { GD.PushError(e.ToString()); GetTree().Quit(1); }
    }
    private async Task Frames(int count = 4) { for (var i = 0; i < count; i++) await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame); }
    private static void Require(bool condition, string message) { if (!condition) throw new InvalidOperationException(message); }
    private static Button Button(Node root, string name) => UiUxCapture.Find<Button>(root).Single(b => b.Name == name);
    private async Task Accept(Control target, bool gamepad)
    {
        target.GrabFocus(); await Frames();
        InputEvent down = gamepad ? new InputEventJoypadButton { ButtonIndex = JoyButton.A, Pressed = true } : new InputEventKey { Keycode = Key.Enter, Pressed = true };
        Require(down.IsActionPressed("ui_accept"), "Menu accept input is not mapped.");
        Input.ParseInputEvent(down); await Frames();
        InputEvent up = gamepad ? new InputEventJoypadButton { ButtonIndex = JoyButton.A, Pressed = false } : new InputEventKey { Keycode = Key.Enter, Pressed = false };
        Input.ParseInputEvent(up); await Frames();
    }
    private async Task Drag(Control from, Control to, bool cancel = false)
    {
        var start = from.GetGlobalRect().GetCenter(); var end = to.GetGlobalRect().GetCenter();
        GetViewport().PushInput(new InputEventMouseMotion { Position = start }, true); await Frames();
        GetViewport().PushInput(new InputEventMouseButton { Position = start, ButtonIndex = MouseButton.Left, Pressed = true }, true); await Frames();
        GetViewport().PushInput(new InputEventMouseMotion { Position = start + new Vector2(16, 0), Relative = new Vector2(16, 0), ButtonMask = MouseButtonMask.Left }, true); await Frames();
        GetViewport().PushInput(new InputEventMouseMotion { Position = end, Relative = end - start, ButtonMask = MouseButtonMask.Left }, true); await Frames();
        if (cancel) GetViewport().PushInput(new InputEventKey { Keycode = Key.Escape, Pressed = true }, true);
        GetViewport().PushInput(new InputEventMouseButton { Position = end, ButtonIndex = MouseButton.Left, Pressed = false }, true); await Frames();
    }
    public async Task Run()
    {
        var session = GetNode<GameSession>("/root/GameSession"); using var suppression = session.SuppressPersistence();
        session.NewGame("界面验证", "同学");
        Input.MouseMode = Input.MouseModeEnum.Visible;
        var layer = new CanvasLayer(); AddChild(layer);
        var ui = new Control { Theme = UiStyles.CreateTheme() }; layer.AddChild(ui); ui.SetAnchorsAndOffsetsPreset(Control.LayoutPreset.FullRect);
        var mechanism = new DreamMechanismPanel(); ui.AddChild(mechanism);
        var sequence = new DreamPuzzlePanel(); ui.AddChild(sequence);
        var submissions = 0; mechanism.Submitted += (_, _) => submissions++; sequence.Submitted += (_, _) => submissions++;
        try
        {
            foreach (var scale in new[] { 1f, 1.25f, 1.5f })
            {
                UiStyles.ApplyScale(ui, scale);
                mechanism.Open(DreamEscapeIds.PuzzleMusicPianoRoll, DreamMechanismMode.PaperRoll); await Frames();
                Input.ParseInputEvent(new InputEventJoypadButton { ButtonIndex = JoyButton.DpadRight, Pressed = true });
                Input.ParseInputEvent(new InputEventJoypadButton { ButtonIndex = JoyButton.DpadRight, Pressed = false }); await Frames();
                Require(GetViewport().GuiGetFocusOwner() == Button(mechanism, "MechanismValue1"), "Gamepad direction did not navigate the diagram.");
                await Accept(Button(mechanism, "MechanismValue0"), false);
                await Accept(Button(mechanism, "MechanismValue1"), true);
                Require(mechanism.PreviewTokens.SequenceEqual(["2", "1", "3", "4"]), $"Native Enter/A paper exchange failed: {string.Join(',', mechanism.PreviewTokens)} focus={GetViewport().GuiGetFocusOwner()?.Name} selected={UiUxCapture.Find<DreamPuzzleDiagram>(mechanism).Single().SelectedPiece}");
                var origin = Button(mechanism, "MechanismValue2"); origin.GrabFocus(); await Frames();
                Require(session.Modal.IsBlocked && mechanism.Workspace.Work.Visible, "Mechanism workspace lost its modal or controls.");
                Require(!UiUxCapture.Find<Button>(mechanism).Any(b => b.Name == "PuzzleReferences") &&
                    !UiUxCapture.Find<ArchiveReader>(mechanism).Any(), "Reference UI remains in a mechanism.");
                Require(GetViewport().GuiGetFocusOwner() == origin && mechanism.PreviewTokens.SequenceEqual(["2", "1", "3", "4"]), "Focus or editing changed unexpectedly.");
                var target = (PuzzleHitButton)Button(mechanism, "MechanismValue3");
                Variant payload = new Godot.Collections.Dictionary { ["owner"] = origin.GetParent().GetInstanceId().ToString(), ["slot"] = 2 };
                Require(target._CanDropData(Vector2.Zero, payload), "A valid paper drop was rejected.");
                target._DropData(Vector2.Zero, payload);
                Require(mechanism.PreviewTokens.SequenceEqual(["2", "1", "4", "3"]), "Drop did not exchange pieces atomically.");
                Variant foreign = new Godot.Collections.Dictionary { ["owner"] = "foreign", ["slot"] = 0 };
                Require(!target._CanDropData(Vector2.Zero, foreign), "Foreign drag payload accepted.");
                await Drag(origin, target);
                Require(mechanism.PreviewTokens.SequenceEqual(["2", "1", "3", "4"]), "Native mouse drag did not swap paper.");
                await Drag(origin, target, true);
                Require(mechanism.Visible && mechanism.PreviewTokens.SequenceEqual(["2", "1", "3", "4"]), "Escape during native drag changed the arrangement or closed the workspace.");
                foreach (var frame in UiUxCapture.Find<PanelContainer>(mechanism).Where(p => p.Name == "WorkspaceFrame"))
                    Require(frame.GetGlobalRect().End.X <= 640 && frame.GetGlobalRect().End.Y <= 360, "Workspace exceeds viewport.");
                Input.ParseInputEvent(new InputEventJoypadButton { ButtonIndex = JoyButton.B, Pressed = true });
                Input.ParseInputEvent(new InputEventJoypadButton { ButtonIndex = JoyButton.B, Pressed = false }); await Frames();
                Require(!mechanism.Visible && !session.Modal.IsBlocked && submissions == 0, "Gamepad cancel submitted or retained input.");
            }
            // Focused native buttons can consume Escape before _UnhandledInput. Cancellation must win.
            mechanism.Open(DreamEscapeIds.PuzzleAuditoriumStageAlignment, DreamMechanismMode.StageAlignment); await Frames();
            Button(mechanism, "MechanismValue0").GrabFocus();
            Input.ParseInputEvent(new InputEventKey { Keycode=Key.Escape, PhysicalKeycode=Key.Escape, Pressed=true });await Frames();
            Input.ParseInputEvent(new InputEventKey { Keycode=Key.Escape, PhysicalKeycode=Key.Escape, Pressed=false });await Frames();
            Require(!mechanism.Visible && !session.Modal.IsBlocked,"Focused stage did not close on Escape.");
            sequence.Open(DreamEscapeIds.PuzzleAuditoriumHandoffPlan,"排演","操作",[("position","站位")],1);await Frames();
            Button(sequence,"Choice_position").GrabFocus();
            Input.ParseInputEvent(new InputEventKey { Keycode=Key.Escape, PhysicalKeycode=Key.Escape, Pressed=true });await Frames();
            Input.ParseInputEvent(new InputEventKey { Keycode=Key.Escape, PhysicalKeycode=Key.Escape, Pressed=false });await Frames();
            Require(!sequence.Visible && !session.Modal.IsBlocked,"Focused sequence did not close on Escape.");
            mechanism.Open(DreamEscapeIds.PuzzleClockRestore, DreamMechanismMode.Clock); await Frames();
            var diagram = UiUxCapture.Find<DreamPuzzleDiagram>(mechanism).Single(); var before = mechanism.PreviewTokens.ToArray();
            diagram._GuiInput(new InputEventMouseButton { ButtonIndex = MouseButton.Left, Pressed = true, Position = new Vector2(diagram.Size.X * .5f, diagram.Size.Y * .5f) });
            diagram.CancelGesture();
            Require(mechanism.PreviewTokens.SequenceEqual(before), "Interrupted clock drag was committed.");
            mechanism.ClosePanel();
            sequence.Open(DreamEscapeIds.PuzzleMusicCage, "音板", "按图形排列。", [("1", "1"), ("2", "2")], 2);
            Button(sequence, "Choice_1").EmitSignal(BaseButton.SignalName.Pressed); Button(sequence, "Choice_1").EmitSignal(BaseButton.SignalName.Pressed);
            Require(sequence.PreviewTokens.Count == 1 && Button(sequence, "PuzzleConfirm").Disabled, "Unique sequence accepted a duplicate or early submit.");
            Button(sequence, "Choice_2").EmitSignal(BaseButton.SignalName.Pressed);
            await Accept(Button(sequence, "AnswerSlot0"), false); await Accept(Button(sequence, "AnswerSlot1"), true);
            Require(sequence.PreviewTokens.SequenceEqual(["2", "1"]), "Sequence slot exchange failed.");
            UiUxCapture.Find<Button>(sequence).Single(b => b.Text == UiText.Get("UI_UNDO")).EmitSignal(BaseButton.SignalName.Pressed);
            Require(sequence.PreviewTokens.SequenceEqual(["1", "2"]), "Sequence undo failed.");
            await Accept(Button(sequence, "PuzzleConfirm"), false);
            Require(submissions == 1 && !session.Modal.IsBlocked, "Explicit submit did not release input exactly once.");
            sequence.Open(DreamEscapeIds.PuzzleMusicCage, "音板", "允许重复。", [("1", "1")], 2, true);
            Button(sequence, "Choice_1").EmitSignal(BaseButton.SignalName.Pressed); Button(sequence, "Choice_1").EmitSignal(BaseButton.SignalName.Pressed);
            Require(sequence.PreviewTokens.SequenceEqual(["1", "1"]), "Repeat-enabled sequence rejected a repeat.");
            sequence.ClosePanel();
            GD.Print("UI_REMAKE_PASS scales=3 native_enter native_a mouse_drag no_references focus drag_payload cancel_drag sequence_undo explicit_submit");
        }
        finally { layer.QueueFree(); await Frames(); }
    }
}
