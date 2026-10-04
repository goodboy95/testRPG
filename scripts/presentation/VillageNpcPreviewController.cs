using Godot;
using DialogueManagerRuntime;
using System.Text.Json;
using TownRpg.Application;

namespace TownRpg.Presentation;

/// <summary>Isolated conversation pilot. Never submits narrative commands or writes a game session.</summary>
public partial class VillageNpcPreviewController : VillageStreamingPreviewController
{
    public List<VillageNpcActor3D> Residents { get; } = [];
    public bool ReadyForInteraction { get; private set; }
    public bool Talking { get; private set; }
    public string? TalkerId { get; private set; }
    public string? LastError { get; private set; }
    public DialoguePanel Panel { get; private set; } = null!;
    public double LoadMs { get; private set; }
    public double UpdateMs => _managerMs + Residents.Sum(n => n.UpdateMs);
    private double _managerMs;
    public DialogueContext DialogueState { get; } = new();
    protected override bool HasExternalModal => Talking;
    private ModalGate Modal => DialogueState.Modal;
    private readonly Dictionary<string, Resource> _dialogues = [];
    private readonly List<PackedScene> _models = [];
    private CancellationTokenSource? _conversation;
    private Task _talkTask = Task.CompletedTask;
    private Label? _hud;
    private StreamWriter? _log;
    private double _logClock;
    private bool _leaving;
    protected virtual string NpcCatalogPath => VillageNpcPolicy.CatalogPath;

    public override void _Ready()
    {
        base._Ready();
        var layer = new CanvasLayer { Name = "NpcUi", Layer = 96 }; AddChild(layer);
        var root = new Control { Name = "NpcUiRoot", MouseFilter = Control.MouseFilterEnum.Ignore };
        root.SetAnchorsAndOffsetsPreset(Control.LayoutPreset.FullRect); layer.AddChild(root);
        Panel = new DialoguePanel { Name = "NpcDialogue", Context = DialogueState }; root.AddChild(Panel);
        if (OS.GetCmdlineUserArgs().Contains("--village-budget-hud"))
        {
            _hud = new Label { Position = new(8, 120), MouseFilter = Control.MouseFilterEnum.Ignore };
            UiStyles.Font(_hud, 9); root.AddChild(_hud);
        }
        if (OS.GetCmdlineUserArgs().Contains("--village-budget-log"))
        {
            var path = ProjectSettings.GlobalizePath("res://outputs/village-npc"); Directory.CreateDirectory(path);
            _log = new StreamWriter(Path.Combine(path, DateTime.Now.ToString("yyyyMMdd-HHmmss") + "-npc.jsonl"));
        }
        Exterior.AdditionalInteractionActive = marker => ReadyForInteraction && Residents.Any(n => n.Marker == marker && n.CanTalk());
        Exterior.InteractionRequested += Interact;
        SetHostLocked(true);
        CallDeferred(MethodName.InitializeResidents);
    }

    private async void InitializeResidents()
    {
        var start = Time.GetTicksUsec();
        try
        {
            var definitions = VillageNpcPolicy.Parse(Godot.FileAccess.GetFileAsString(NpcCatalogPath));
            var ids = new HashSet<string>(StringComparer.Ordinal);
            foreach (var definition in definitions)
            {
                foreach (var id in VillageNpcPolicy.ValidateDialogue(Godot.FileAccess.GetFileAsString(definition.Dialogue)))
                    if (!ids.Add(id)) throw new InvalidDataException("Duplicate dialogue text ID: " + id);
                _models.Add(ResourceLoader.Load<PackedScene>(definition.Model) ?? throw new InvalidDataException("Missing NPC model."));
                _dialogues.Add(definition.Id, ResourceLoader.Load<Resource>(definition.Dialogue) ?? throw new InvalidDataException("Missing NPC dialogue."));
            }
            await ToSignal(GetTree(), SceneTree.SignalName.PhysicsFrame);
            for (var i = 0; i < definitions.Length; i++)
            {
                if (_leaving) return;
                var actor = new VillageNpcActor3D();
                actor.Initialize(definitions[i], Exterior, _models[i]);
                if (Residents.Count > 0) actor.UseSharedMaterial(Residents[0].SharedMaterial);
                Exterior.AddChild(actor); Residents.Add(actor); Exterior.RegisterInteractionMarker(actor.Marker);
                await ToSignal(GetTree(), SceneTree.SignalName.PhysicsFrame);
                if (_leaving) return;
                ValidateRoute(actor);
                await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame);
            }
            ReadyForInteraction = true;
        }
        catch (Exception error) { Report(error); }
        finally
        {
            LoadMs = (Time.GetTicksUsec() - start) / 1000d;
            if (!_leaving) SetHostLocked(false);
        }
    }

    private void ValidateRoute(VillageNpcActor3D npc)
    {
        foreach (var point in npc.Route)
        {
            using var shape = new CapsuleShape3D { Height = 1.5f, Radius = .28f };
            using var query = new PhysicsShapeQueryParameters3D { Shape = shape, CollisionMask = 1,
                Transform = new Transform3D(Basis.Identity, point + Vector3.Up * .87f),
                Exclude = new Godot.Collections.Array<Rid> { npc.GetRid() } };
            if (Exterior.GetWorld3D().DirectSpaceState.IntersectShape(query).Count > 0)
                throw new InvalidDataException("NPC anchor is obstructed: " + npc.Definition.Id + " " + point);
        }
        for (var i = 1; i < npc.Route.Length; i++)
        {
            var start = npc.Route[i - 1]; var end = npc.Route[i];
            var steps = (int)Math.Ceiling(start.DistanceTo(end) / .2f);
            for (var sample = 0; sample < steps; sample++)
            {
                var a = start.Lerp(end, (float)sample / steps);
                var b = start.Lerp(end, (float)(sample + 1) / steps);
                a.Y = Exterior.WalkHeight(a.X, a.Z) + .04f;
                b.Y = Exterior.WalkHeight(b.X, b.Z) + .04f;
                if (Math.Abs(a.Y-b.Y) > .2f || !npc.SegmentClear(a, b))
                    throw new InvalidDataException("NPC route is obstructed: " + npc.Definition.Id + " " + a);
            }
        }
    }

    public override void _Process(double delta)
    {
        var start = Time.GetTicksUsec();
        ApplyVisualBudget();
        foreach (var npc in Residents) npc.Held = IsPaused || IsInterior || IsTransitioning || HostLocked && !Talking;
        _logClock += delta;
        if (_logClock < .5) { _managerMs = (Time.GetTicksUsec() - start) / 1000d; return; }
        _logClock = 0;
        if (_hud is not null) _hud.Text = $"NPC {Residents.Count(n => n.VisibleTriangles > 0)} | {Residents.Sum(n => n.VisibleTriangles)} tris | {Residents.Sum(n => n.VisibleSurfaces)} surfaces\nupdate {UpdateMs:F3} ms | load {LoadMs:F1} ms";
        _log?.WriteLine(JsonSerializer.Serialize(Snapshot()));
        _managerMs = (Time.GetTicksUsec() - start) / 1000d;
    }

    private void ApplyVisualBudget()
    {
        var art = Residents.Where(n => n.VisualProfile == VillageNpcArtPolicy.Art).ToArray();
        if (art.Length == 0) return;
        var caps = VillageNpcArtPolicy.AllocateCaps(
            art.Select(n => ActivePlayer.GlobalPosition.DistanceTo(n.GlobalPosition)).ToArray(),
            art.Select(n => n.Talking).ToArray());
        for (var i = 0; i < art.Length; i++) art[i].SetLodCap(caps[i]);
    }

    public override void _Input(InputEvent inputEvent)
    {
        if (Talking && !Panel.Visible && inputEvent.IsActionPressed("ui_cancel"))
        {
            _conversation?.Cancel(); GetViewport().SetInputAsHandled(); return;
        }
        base._Input(inputEvent);
    }

    public object Snapshot() => new { loadMs = LoadMs, updateMs = UpdateMs, talking = TalkerId,
        npcs = Residents.Select(n => new { id = n.Definition.Id, lod = n.Lod, triangles = n.VisibleTriangles,
            surfaces = n.VisibleSurfaces, held = n.Held, obstructed = n.Obstructed, routeIndex = n.RouteIndex,
            updateMs = n.UpdateMs, motionMs = n.MotionMs, poseMs = n.PoseMs }).ToArray() };

    private void Interact(string id)
    {
        if (!id.StartsWith("npc:", StringComparison.Ordinal)) return;
        _talkTask = TalkAsync(id[4..]);
    }

    public Task StartTalk(string id)
    {
        if (Talking) return Task.CompletedTask;
        _talkTask = TalkAsync(id); return _talkTask;
    }

    private async Task TalkAsync(string id)
    {
        var npc = Residents.SingleOrDefault(n => n.Definition.Id == id);
        if (!ReadyForInteraction || Modal.IsBlocked || HostLocked || IsPaused || IsInterior || IsTransitioning || npc?.CanTalk() != true) return;
        Talking = true; TalkerId = id;
        using var lease = Modal.Acquire("npc-conversation");
        using var cancellation = new CancellationTokenSource(); _conversation = cancellation;
        SetHostLocked(true); npc.BeginTalk(ActivePlayer.GlobalPosition);
        ActivePlayer.SetFacingImmediate((npc.GlobalPosition - ActivePlayer.GlobalPosition) with { Y = 0 });
        ActivePlayer.PlayInteractionPose("talk", npc.GlobalPosition);
        try
        {
            var next = npc.Definition.StartTitle;
            while (true)
            {
                cancellation.Token.ThrowIfCancellationRequested();
                // A button's press can complete its task before the release reaches the GUI.
                // Do not let that release activate the first button of the following node.
                do
                {
                    await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame);
                    cancellation.Token.ThrowIfCancellationRequested();
                } while (Input.IsActionPressed("ui_accept") || Input.IsActionPressed("interact"));
                await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame);
                cancellation.Token.ThrowIfCancellationRequested();
                using var line = await DialogueManager.GetNextDialogueLine(_dialogues[id], next,
                    mutation_behaviour: MutationBehaviour.Skip).WaitAsync(cancellation.Token);
                if (line is null) break;
                var speaker = line.Character == "主角" ? DialogueState.PlayerName : line.Character;
                var responses = line.Responses.Where(r => r.IsAllowed).ToArray();
                if (responses.Length > 0)
                {
                    var choice = await Panel.ShowChoiceAsync(line.TranslationKey, speaker, line.Text,
                        responses.Select(r => r.Text).ToArray()).WaitAsync(cancellation.Token);
                    next = responses[choice].NextId;
                }
                else
                {
                    await Panel.ShowLineAsync(line.TranslationKey, speaker, line.Text).WaitAsync(cancellation.Token);
                    next = line.NextId;
                }
            }
        }
        catch (OperationCanceledException) { }
        catch (Exception error) { Report(error); }
        finally
        {
            if (IsInstanceValid(Panel) && Panel.IsInsideTree()) Panel.CloseDialogue();
            if (IsInstanceValid(npc)) npc.EndTalk();
            Talking = false; TalkerId = null; _conversation = null;
            if (!_leaving) SetHostLocked(false);
        }
    }

    private void Report(Exception error)
    {
        LastError = error.Message; GD.PushError(error.ToString());
        if (_hud is not null) _hud.Text = "NPC preview error: " + error.Message;
    }

    public async Task CancelConversation()
    {
        _conversation?.Cancel();
        if (IsInstanceValid(Panel) && Panel.IsInsideTree()) Panel.CloseDialogue();
        await _talkTask;
    }

    public override async Task ResetPreview()
    {
        await CancelConversation(); await base.ResetPreview();
        foreach (var npc in Residents) npc.ResetResident();
        DialogueState.ReadLines.Clear();
        Panel.ClearHistory();
    }

    public override void _ExitTree()
    {
        _leaving = true; _conversation?.Cancel();
        if (IsInstanceValid(Panel) && Panel.IsInsideTree()) Panel.CloseDialogue();
        Exterior.InteractionRequested -= Interact; Exterior.AdditionalInteractionActive = null;
        foreach (var npc in Residents) Exterior.UnregisterInteractionMarker(npc.Marker);
        _log?.Dispose();
        base._ExitTree();
    }
}
