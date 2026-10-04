using Godot;
using System.Text.Json;
using TownRpg.Application;
using TownRpg.Core.Services;
using TownRpg.Presentation;

namespace TownRpg.Testing;

/// <summary>Graphical NPC input, lifecycle, route, state-isolation and performance acceptance.</summary>
public partial class VillageNpcProbe : Node
{
    private VillageNpcPreviewController _host = null!;
    private readonly List<string> _checks = [];
    private readonly List<object> _metrics = [];
    private string _output = "";
    public override void _Ready() => CallDeferred(MethodName.Run);
    private void Require(bool condition, string message)
    {
        if (!condition) throw new InvalidOperationException(message);
        _checks.Add(message);
    }
    private async Task Frames(int count = 3)
    { for (var i = 0; i < count; i++) await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame); }
    private async Task Until(Func<bool> test, string reason, int frames = 300)
    {
        for (var i = 0; i < frames && !test(); i++) await Frames(1);
        Require(test(), reason);
    }
    private static IEnumerable<Node> Descendants(Node parent)
    { foreach (var node in parent.GetChildren()) { yield return node; foreach (var child in Descendants(node)) yield return child; } }
    private Button[] Choices => _host.Panel.VisibleChoices.ToArray();
    private Button Continue => (Button)_host.Panel.FindChild("DialogueContinue", true, false);
    private async Task Key(Key key)
    {
        Input.ParseInputEvent(new InputEventKey { Keycode = key, PhysicalKeycode = key, Pressed = true }); await Frames(1);
        Input.ParseInputEvent(new InputEventKey { Keycode = key, PhysicalKeycode = key, Pressed = false }); await Frames(2);
    }
    private async Task Pad(JoyButton button)
    {
        Input.ParseInputEvent(new InputEventJoypadButton { ButtonIndex = button, Pressed = true }); await Frames(1);
        Input.ParseInputEvent(new InputEventJoypadButton { ButtonIndex = button, Pressed = false }); await Frames(2);
    }
    private async Task Place(VillageNpcActor3D npc)
    {
        _host.ActivePlayer.GlobalPosition = npc.GlobalPosition + new Vector3(0, .03f, 1.35f);
        _host.ActivePlayer.Velocity = Vector3.Zero;
        _host.ActivePlayer.SetFacingImmediate(Vector3.Forward);
        _host.ActiveCamera.SetYawImmediate(13); _host.ActiveCamera.SnapToTarget();
        await Frames(5);
        Require(npc.CanTalk(), "reachable " + npc.Definition.Id);
    }
    private async Task Photo(string name)
    {
        await ToSignal(RenderingServer.Singleton, RenderingServer.SignalName.FramePostDraw);
        using var image = GetViewport().GetTexture().GetImage(); image.SavePng(Path.Combine(_output, name + ".png"));
    }
    private async Task ToChoices()
    {
        for (var i = 0; i < 12 && Choices.Length == 0 && _host.Talking; i++)
        {
            await Until(() => _host.Panel.AwaitingAdvance || _host.Panel.AwaitingChoice || !_host.Talking, "dialogue progresses");
            if (_host.Panel.AwaitingAdvance && _host.Talking) { Continue.GrabFocus(); await Key(Godot.Key.Enter); }
        }
        Require(Choices.Length == 3, "two topics and goodbye; focus=" + GetWindow().HasFocus() + " history=" + _host.Panel.HistoryText);
    }

    private async Task Exercise(VillageNpcActor3D npc)
    {
        await Place(npc); await Photo(npc.Definition.Id + "-approach");
        await Key(Godot.Key.F); await Until(() => _host.Talking, "F starts dialogue");
        var position = _host.ActivePlayer.GlobalPosition; var yaw = _host.ActiveCamera.YawDegrees;
        Input.ActionPress("move_up"); await Frames(8); Input.ActionRelease("move_up");
        Require(_host.ActivePlayer.GlobalPosition.DistanceTo(position) < .02f && _host.ActiveCamera.YawDegrees == yaw, "dialogue holds player and camera");
        Require(npc.Talking && npc.Lod == 0, "talker remains high quality");
        await ToChoices(); await Photo(npc.Definition.Id + "-topics");
        var history = _host.Panel.HistoryText;
        GetTree().Root.PropagateNotification((int)NotificationWMWindowFocusOut);
        await Key(Godot.Key.Enter); await Frames(10);
        Require(_host.Panel.HistoryText == history, "lost focus freezes dialogue input");
        GetTree().Root.PropagateNotification((int)NotificationWMWindowFocusIn);
        await Key(Godot.Key.H);
        var historyButton = (Button)_host.Panel.FindChild("DialogueHistory", true, false);
        historyButton.GrabFocus(); await Key(Godot.Key.Enter);
        Require(Descendants(_host.Panel).OfType<ReadingPanel>().Single().Visible, "history opens");
        Require(!GetNode<GameSession>("/root/GameSession").Modal.IsBlocked, "history uses preview modal gate");
        await Key(Godot.Key.Escape);
        Require(_host.Talking && !Descendants(_host.Panel).OfType<ReadingPanel>().Single().Visible, "history returns to dialogue tools");
        await Key(Godot.Key.Escape); Require(_host.Talking, "tools return preserves conversation");
        for (var choice = 0; choice < 2; choice++)
        {
            Choices[choice].GrabFocus(); await Pad(JoyButton.A); await Frames(3);
            await ToChoices();
        }
        Choices[2].GrabFocus(); await Key(Godot.Key.Space); await Frames(3);
        Continue.GrabFocus(); await Key(Godot.Key.Space);
        await Until(() => !_host.Talking, "goodbye ends dialogue");
        Require(!_host.HostLocked && !npc.Talking, "goodbye releases all locks");
        await Place(npc);
        await Pad(JoyButton.A); await Until(() => _host.Talking, "controller A starts dialogue");
        await Pad(JoyButton.B); await Until(() => !_host.Talking, "controller B cancels");
        for (var i = 0; i < 20; i++)
        {
            await Place(npc); _ = _host.StartTalk(npc.Definition.Id);
            await Until(() => _host.Panel.Visible, "repeat dialogue " + i);
            if (i % 2 == 0) await ToChoices();
            var old = _host.TalkerId;
            await _host.StartTalk(npc.Definition.Id == "aunt_qiao" ? "shen_yan" : "aunt_qiao");
            Require(_host.TalkerId == old, "single conversation owner");
            await Key(Godot.Key.Escape); await Until(() => !_host.Talking, "Esc cancels repeat " + i);
            Require(!_host.IsPaused && !_host.HostLocked, "Esc consumed by conversation");
        }
        await Place(npc);
        var wall = new StaticBody3D { Position = (npc.GlobalPosition + _host.ActivePlayer.GlobalPosition) / 2 + Vector3.Up,
            CollisionLayer = 1 };
        wall.AddChild(new CollisionShape3D { Shape = new BoxShape3D { Size = new(1.5f, 2, .12f) } });
        _host.Exterior.AddChild(wall); await Frames(4);
        Require(!npc.CanTalk(), "solid obstruction prevents conversation");
        await _host.StartTalk(npc.Definition.Id); Require(!_host.Talking, "blocked trigger rejected");
        wall.QueueFree(); await Frames(4);
    }

    private async Task PerformanceSamples()
    {
        RenderingServer.ViewportSetMeasureRenderTime(GetViewport().GetViewportRid(), true);
        foreach (var size in new[] { new Vector2I(1280, 720), new Vector2I(1600, 900), new Vector2I(1920, 1080) })
        {
            DisplayServer.WindowSetMode(DisplayServer.WindowMode.Windowed);
            GetWindow().Size = size;
            if (size.X == 1920) DisplayServer.WindowSetMode(DisplayServer.WindowMode.Fullscreen);
            await Frames(10);
            foreach (var npc in _host.Residents)
            {
                await Place(npc);
                await Until(() => _host.Exterior.Streaming!.PendingLoads == 0, "performance detail loads finish", 1200);
                await Frames(90);
                for (var i = 0; i < 180; i++)
                {
                    _host.ActiveCamera.SetYawImmediate(13 + i * 2);
                    await Frames(1);
                    var rid = GetViewport().GetViewportRid();
                    var gpu = RenderingServer.ViewportGetMeasuredRenderTimeGpu(rid);
                    _metrics.Add(new { width = size.X, height = size.Y, npc = npc.Definition.Id,
                        updateMs = _host.UpdateMs, gpuMs = gpu > 0 ? (double?)gpu : null,
                        primitives = Performance.GetMonitor(Performance.Monitor.RenderTotalPrimitivesInFrame),
                        draws = Performance.GetMonitor(Performance.Monitor.RenderTotalDrawCallsInFrame), snapshot = _host.Snapshot() });
                }
            }
        }
    }

    private async void Run()
    {
        try
        {
            _output = ProjectSettings.GlobalizePath("res://outputs/village-npc/" + DateTime.Now.ToString("yyyyMMdd-HHmmss") + "-probe");
            Directory.CreateDirectory(_output);
            var session = GetNode<GameSession>("/root/GameSession"); var before = SaveGameSerializer.Serialize(session.State);
            var save = ProjectSettings.GlobalizePath(GameSession.SavePath); var bytes = File.Exists(save) ? File.ReadAllBytes(save) : null;
            _host = new VillageNpcPreviewController(); AddChild(_host);
            GetWindow().GrabFocus();
            await Until(() => _host.ReadyForInteraction || _host.LastError is not null, "NPC initialization finished", 1800);
            Require(_host.LastError is null && _host.ReadyForInteraction, "NPC ready: " + _host.LastError);
            await Frames(30);
            Require(_host.Residents.Count == 2 && _host.Exterior.InteractionMarkerCount == 3, "two residents and home marker");
            var duplicate = new InteractionMarker3D { TargetId = "npc:aunt_qiao" };
            var rejected = false;
            try { _host.Exterior.RegisterInteractionMarker(duplicate); } catch (InvalidOperationException) { rejected = true; }
            duplicate.Free(); Require(rejected, "duplicate interaction rejected");
            var empty = new InteractionMarker3D(); rejected = false;
            try { _host.Exterior.RegisterInteractionMarker(empty); } catch (InvalidOperationException) { rejected = true; }
            empty.Free(); Require(rejected, "empty interaction ID rejected");
            var first = _host.Residents[0]; await Place(first);
            _host.Exterior.UnregisterInteractionMarker(first.Marker); await Frames(2);
            Require(_host.Exterior.InteractionMarkerCount == 2 && _host.Exterior.CurrentInteractionTargetId != first.Marker.TargetId,
                "unregister clears active target");
            _host.Exterior.RegisterInteractionMarker(first.Marker);
            foreach (var npc in _host.Residents) await Exercise(npc);
            var shen = _host.Residents.Single(n => n.Definition.Id == "shen_yan");
            _host.ActivePlayer.GlobalPosition = _host.Exterior.SurfacePoint(-50, 30, .04f);
            shen.ResetResident(); var origin = shen.GlobalPosition;
            for (var i=0;i<900 && shen.GlobalPosition.DistanceTo(origin)<=.3f;i++) await Frames(1);
            Require(shen.GlobalPosition.DistanceTo(origin)>.3f,"patrol leaves anchor: " + shen.MotionDiagnostic);
            var ahead = (shen.Route[shen.RouteIndex] - shen.GlobalPosition).Normalized();
            _host.ActivePlayer.GlobalPosition = shen.GlobalPosition + ahead * .65f;
            await Until(() => shen.Obstructed, "patrol yields to player");
            var blocked = shen.GlobalPosition; await Frames(45);
            Require(shen.GlobalPosition.DistanceTo(blocked) < .03f, "obstructed patrol stays still");
            _host.ActivePlayer.GlobalPosition = _host.Exterior.SurfacePoint(-50, 30, .04f);
            await Until(() => shen.GlobalPosition.DistanceTo(blocked) > .15f, "patrol resumes without teleport");
            await Until(() => shen.RouteIndex == 2, "patrol reaches middle waypoint", 1000);
            await Until(() => shen.RouteIndex == 1 && shen.GlobalPosition.DistanceTo(shen.Route[2]) < .5f,
                "patrol reaches last waypoint and reverses", 1000);
            await Until(() => shen.RouteIndex == 0, "patrol returns via middle waypoint", 1000);
            await Until(() => shen.RouteIndex == 1 && shen.GlobalPosition.DistanceTo(shen.Route[0]) < .5f,
                "patrol completes a round trip", 1000);
            Require(shen.IsOnFloor(), "patrol remains grounded");
            _host.SetPaused(true); await Frames(3); var paused = shen.GlobalPosition; await Frames(30);
            Require(shen.GlobalPosition.DistanceTo(paused) < .01f, "pause stops patrol"); _host.SetPaused(false);
            await Place(shen); _ = _host.StartTalk("shen_yan"); await Until(() => _host.Panel.Visible, "reset during dialogue starts");
            await _host.ResetPreview(); Require(!_host.Talking && !_host.HostLocked && _host.DialogueState.ReadLines.Count == 0, "reset clears transient dialogue");
            await Frames(4); Require(await _host.SwitchArea(true), "enter home with NPC system"); await Frames(60);
            Require(_host.Residents.All(n => n.Held), "NPCs suspended indoors");
            Require(await _host.SwitchArea(false), "return to village"); await Frames(5);
            Require(_host.Residents.Count == 2 && _host.Exterior.InteractionMarkerCount == 3, "no duplicate NPC on reentry");
            await Place(shen); var route = shen.RouteIndex;
            _host.ActivePlayer.GlobalPosition = _host.Exterior.SurfacePoint(95, 90, .04f); await Frames(5);
            Require(_host.Residents.All(n => n.Lod == 2), "distant NPC visuals hidden");
            await Place(shen); Require(shen.Lod == 0, "NPC returns to near quality");
            await PerformanceSamples();
            Require(SaveGameSerializer.Serialize(session.State) == before, "formal game state unchanged");
            Require(bytes is null ? !File.Exists(save) : bytes.SequenceEqual(File.ReadAllBytes(save)), "formal save bytes unchanged");
            Require(_host.LastError is null, "no NPC runtime error");
            File.WriteAllText(Path.Combine(_output, "results.json"), JsonSerializer.Serialize(new { checks = _checks, samples = _metrics,
                device = RenderingServer.GetVideoAdapterName(), targetHardwareAcceptance = false }, new JsonSerializerOptions { WriteIndented = true }));
            GD.Print($"VILLAGE_NPC_PROBE_PASS checks={_checks.Count} samples={_metrics.Count} output={_output}");
            _host.QueueFree(); await Frames(4); GetTree().Quit();
        }
        catch (Exception error) { GD.PushError(error.ToString()); GD.Print("VILLAGE_NPC_PROBE_FAIL " + error.Message); GetTree().Quit(1); }
    }
}
