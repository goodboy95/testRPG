using Godot;
using System.Security.Cryptography;
using System.Text.Json;
using TownRpg.Application;
using TownRpg.Core.Domain;
using TownRpg.Presentation;

namespace TownRpg.Testing;

// Black-box input route. State is read for assertions only; branch files contain previously earned saves.
public partial class PrologueE2EProbe : Node
{
    private GameSession Session => GetNode<GameSession>("/root/GameSession");
    private MainController _main = null!;
    private string _output = "", _save = "", _step = "startup";
    private int _walks, _clicks, _reloads, _pins;
    private float _meters;
    private readonly List<object> _trace = [];
    private readonly List<object> _saves = [];
    private DreamEscapeState Dream => Session.State.DreamEscape!;
    private IEnumerable<T> Find<T>() where T : Node => UiUxCapture.Find<T>(_main);
    private WorldArea3D Area => Find<WorldArea3D>().Single(n => n.IsInsideTree() && !n.IsQueuedForDeletion());
    private Button Named(string name) => Find<Button>().Single(b => b.Name == name && b.IsVisibleInTree());
    private Button TextButton(string text) => Find<Button>().Single(b => b.Text == text && b.IsVisibleInTree());
    private static void Require(bool ok, string message) { if (!ok) throw new InvalidOperationException(message); }
    private async Task Frames(int n = 5) { for (var i = 0; i < n; i++) await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame); }
    private void Log(string action) { GD.Print("E2E " + action); _trace.Add(new { action, area = Session.State.DreamEscape?.CurrentAreaId, events = Session.State.CompletedEvents.Count, solved = Session.State.DreamEscape?.SolvedPuzzles.Count }); }
    private static void KeyState(Key key, bool down) => Input.ParseInputEvent(new InputEventKey { Keycode = key, PhysicalKeycode = key, Pressed = down });
    private async Task KeyPress(Key key) { KeyState(key, true); await Frames(2); KeyState(key, false); await Frames(); }
    private async Task Joy(JoyButton button) { Input.ParseInputEvent(new InputEventJoypadButton { ButtonIndex = button, Pressed = true }); await Frames(2); Input.ParseInputEvent(new InputEventJoypadButton { ButtonIndex = button, Pressed = false }); await Frames(); }
    private async Task Click(Button button)
    {
        Require(!button.Disabled && button.IsVisibleInTree(), "Cannot click " + button.Name);
        if (button.GetWindow() is AcceptDialog window)
        {
            button.GrabFocus();await Frames();
            window.PushInput(new InputEventKey { Keycode=Key.Enter, Pressed=true },true);await Frames(2);
            window.PushInput(new InputEventKey { Keycode=Key.Enter, Pressed=false },true);await Frames(12);_clicks++;return;
        }
        button.GrabFocus();await Frames(2);await KeyPress(Key.Enter);_clicks++;
    }
    private async Task Settle()
    {
        var quiet = 0;
        for (var i = 0; i < 600; i++)
        {
            var next = Find<Button>().FirstOrDefault(b => b.Name == "DialogueContinue" && b.IsVisibleInTree());
            if (next is not null) { await Click(next); quiet = 0; }
            else
            {
                await Frames(2);
                if (Find<Control>().Any(c=>c.Name=="WakeFade" && !c.IsQueuedForDeletion()) ||
                    Dream.Status is DreamEscapeStatus.CompletedEscape or DreamEscapeStatus.CompletedPin &&
                    (Session.State.VillageExploration?.WakeIntroductionComplete != true || Session.Modal.IsBlocked))
                { quiet=0;continue; }
                if (++quiet > 15) return;
            }
        }
        throw new InvalidOperationException("Dialogue did not settle.");
    }
    private string Fingerprint() => JsonSerializer.Serialize(new {
        Dream.Status, Dream.CurrentAreaId, Items = Dream.Items.Order().ToArray(), Solved = Dream.SolvedPuzzles.Order().ToArray(),
        Installed = Dream.InstalledParts.Order().ToArray(), Clues = Dream.DiscoveredClues.Order().ToArray(),
        Events = Session.State.CompletedEvents.Order().ToArray(), Dream.XiaoyuJoined });
    private Func<Vector3, bool> Blocked(WorldArea3D area)
    {
        var nodes = UiUxCapture.Find<Node>(area).ToArray();
        if (nodes.OfType<DreamClassroomVisual3D>().FirstOrDefault() is { } classroom) return classroom.IsBlocked;
        if (nodes.OfType<DreamMusicVisual3D>().FirstOrDefault() is { } music) return music.IsBlocked;
        if (nodes.OfType<DreamMansionVisual3D>().FirstOrDefault() is { } mansion) return mansion.IsBlocked;
        return nodes.OfType<DreamLateRoomVisual3D>().Single().IsBlocked;
    }
    private static List<Vector3> Route(Vector3 from, Vector3 to, Func<Vector3, bool> blocked)
    {
        bool Clear(Vector3 a, Vector3 b) { var n = Math.Max(1, (int)Math.Ceiling(a.DistanceTo(b) / .08f)); for (var i=0;i<=n;i++) if(blocked(a.Lerp(b,(float)i/n)))return false; return true; }
        if (Clear(from,to)) return [to];
        const float grid=.5f;
        Vector3 Point(Vector2I p) => new(p.X*grid,0,p.Y*grid);
        var points = new List<Vector2I>();
        for(var x=-25;x<=25;x++) for(var z=-25;z<=30;z++) if(!blocked(Point(new(x,z))))points.Add(new(x,z));
        var usable=points.ToHashSet(); var costs=new Dictionary<Vector2I,float>(); var previous=new Dictionary<Vector2I,Vector2I>(); var queue=new PriorityQueue<Vector2I,float>();
        foreach(var p in points.Where(p=>Point(p).DistanceTo(from)<1.4f && Clear(from,Point(p)))) {costs[p]=from.DistanceTo(Point(p));queue.Enqueue(p,costs[p]+Point(p).DistanceTo(to));}
        Vector2I? end=null;
        while(queue.TryDequeue(out var p,out _))
        {
            if(Point(p).DistanceTo(to)<1.4f && Clear(Point(p),to)){end=p;break;}
            for(var dx=-1;dx<=1;dx++)for(var dz=-1;dz<=1;dz++)
            { var next=p+new Vector2I(dx,dz);if((dx==0&&dz==0)||!usable.Contains(next)||!Clear(Point(p),Point(next)))continue;
              var c=costs[p]+Point(p).DistanceTo(Point(next));if(costs.TryGetValue(next,out var old)&&old<=c)continue;
              costs[next]=c;previous[next]=p;queue.Enqueue(next,c+Point(next).DistanceTo(to)); }
        }
        Require(end.HasValue,$"No walk route {from} -> {to}");var path=new List<Vector3>{to};var cursor=end!.Value;path.Add(Point(cursor));
        while(previous.TryGetValue(cursor,out var parent)){cursor=parent;path.Add(Point(cursor));}path.Reverse();
        var simple=new List<Vector3>();var current=from;
        while(path.Count>0){var index=path.FindLastIndex(p=>Clear(current,p));Require(index>=0,"Route smoothing failed");current=path[index];simple.Add(current);path.RemoveRange(0,index+1);}return simple;
    }
    private async Task Walk(string id)
    {
        _step=id;
        if (Input.MouseMode != Input.MouseModeEnum.Captured)
        {
            // A departing camera can release the cursor after an area transition. Use the same
            // click-to-recapture path available to the player before driving captured mouse motion.
            GetWindow().GrabFocus();await Frames(2);
            Input.ParseInputEvent(new InputEventMouseButton { ButtonIndex=MouseButton.Left,Pressed=true });
            Input.ParseInputEvent(new InputEventMouseButton { ButtonIndex=MouseButton.Left,Pressed=false });await Frames(2);
            Require(Input.MouseMode==Input.MouseModeEnum.Captured,"Pointer did not recapture before "+id);
        }
        var area=Area;var player=area.GetNode<PlayerController3D>("Player");var rig=area.GetNode<ThirdPersonCameraRig>("ThirdPersonCameraRig");
        var marker=area.GetNode<Node3D>("InteractionMarkers").GetChildren().OfType<InteractionMarker3D>().Single(m=>m.TargetId==id);
        var blocked=Blocked(area);var goal=new Vector3(marker.GlobalPosition.X,0,marker.GlobalPosition.Z);
        Require(!blocked(goal),"Target is blocked: "+id);
        foreach(var waypoint in Route(player.GlobalPosition,goal,blocked))
        {
            var previous=player.GlobalPosition;var stuck=0;
            for(var frame=0;player.GlobalPosition.DistanceTo(waypoint)>.13f;frame++)
            {
                Require(frame<1800 && stuck<90,$"Walking stuck at {player.GlobalPosition} toward {waypoint}; input={player.InputEnabled} modal={Session.Modal.IsBlocked} mouse={Input.MouseMode} yaw={rig.YawDegrees}");
                var direction=waypoint-player.GlobalPosition; var yaw=Mathf.RadToDeg(Mathf.Atan2(-direction.X,-direction.Z));
                var delta=Mathf.Wrap(yaw-rig.YawDegrees,-180f,180f);
                Input.ParseInputEvent(new InputEventMouseMotion { ScreenRelative=new(-delta/(rig.MouseDegreesPerPixel*rig.LookSensitivity),0) });
                KeyState(Key.W,true);await ToSignal(GetTree(),SceneTree.SignalName.PhysicsFrame);
                var traveled=player.GlobalPosition.DistanceTo(previous);_meters+=traveled;stuck=traveled<.00001f?stuck+1:0;previous=player.GlobalPosition;
            }
            KeyState(Key.W,false);await Frames(3);
        }
        KeyState(Key.W,false);await Frames();_walks++;
        Require(area.CurrentInteractionTargetId==id,$"Focus {area.CurrentInteractionTargetId} instead of {id} at {player.GlobalPosition}");
    }
    private async Task Interact(string id, bool gamepad=false, bool settle=true)
    {
        await Walk(id);Log("interact "+id);if(gamepad)await Joy(JoyButton.A);else await KeyPress(Key.F);
        await Frames(35);if(settle)await Settle();
        if(Find<ReadingPanel>().Any(p=>p.IsVisibleInTree())) {await KeyPress(Key.Escape);await Frames();}
    }
    private async Task Sequence(string id, params string[] values)
    {
        await Interact(id);foreach(var value in values)await Click(Named("Choice_"+value));await Click(Named("PuzzleConfirm"));await Settle();
    }
    private async Task Values(string id, params int[] counts)
    {
        await Interact(id);for(var i=0;i<counts.Length;i++)for(var j=0;j<counts[i];j++)await Click(Named("MechanismValue"+i));
        await Click(Named("MechanismConfirm"));await Settle();
    }
    private async Task Title() { await KeyPress(Key.Escape);await Click(Named("PauseReturnTitle"));await Frames(12); }
    private async Task Reload(string label, bool backup=false)
    {
        var fingerprint=Fingerprint();Require(File.Exists(_save),"No real autosave at "+label);
        File.Copy(_save,Path.Combine(_output,label+".json"),true);await Title();
        if(backup){File.Move(_save,_save+".bak",true);File.WriteAllText(_save+".tmp","interrupted temporary write");}
        await Click(TextButton("继续"));await Frames(40);await Settle();
        Require(fingerprint==Fingerprint(),"Reload changed earned progress at "+label);_reloads++;
        _saves.Add(new{label,backupRecovery=backup,area=Dream.CurrentAreaId,sha256=Convert.ToHexString(SHA256.HashData(File.ReadAllBytes(Path.Combine(_output,label+".json")))),equalProgress=true});
        Log("save/reload "+label+(backup?" backup-recovery":""));
    }
    private async Task PinBranch()
    {
        var area=Dream.CurrentAreaId;var fingerprint=Fingerprint();var earned=File.ReadAllBytes(_save);
        await KeyPress(Key.I);await Click(Named("Item_dream_pin"));await Click(TextButton(DreamPrologueText.Get("DREAM_V6_UI_INVENTORYPANEL_004")));
        var dialog=Find<ConfirmationDialog>().Single(d=>d.Visible && d.Name!="QuitConfirmation");await Click(dialog.GetCancelButton());
        Require(fingerprint==Fingerprint(),"Pin cancel changed progress");
        await KeyPress(Key.I);await Click(Named("Item_dream_pin"));await Click(TextButton(DreamPrologueText.Get("DREAM_V6_UI_INVENTORYPANEL_004")));
        await Click(dialog.GetOkButton());await Frames(30);await Settle();
        Require(Dream.Status==DreamEscapeStatus.CompletedPin && Dream.Items.Count==0 && Session.State.Player.LocationId==VillageExplorationState.HomeLocation,"Pin did not finish cleanly");
        Require(_main.VillageRuntime is { IsInterior:true } && Session.State.VillageExploration!.WakeIntroductionComplete,"Pin wake missing");
        File.Copy(_save,Path.Combine(_output,"pin-"+area+".json"),true);
        await Title();await Frames(15);await Click(TextButton("继续"));await Frames(60);await Settle();
        Require(Dream.Status==DreamEscapeStatus.CompletedPin && Dream.Items.Count==0,"Pin terminal reload failed");
        await Title();await Frames(15);File.WriteAllBytes(_save,earned);
        await Click(TextButton("继续"));await Frames(40);await Settle();Require(fingerprint==Fingerprint(),"Earned branch restoration failed");_pins++;Log("pin cancel/confirm/terminal-reload "+area);
    }
    public override void _Ready()=>CallDeferred(MethodName.Run);
    private async void Run()
    {
        IDisposable? saveLease=null;Dictionary<string,string?>? formal=null;
        try
        {
            Require(OS.GetCmdlineUserArgs().Contains("--prologue-e2e"),"Missing acceptance flag");
            _output=ProjectSettings.GlobalizePath("res://outputs/village-wake/prologue-e2e/"+DateTime.Now.ToString("yyyyMMdd-HHmmss"));Directory.CreateDirectory(_output);_save=Path.Combine(_output,"save.json");
            string? Hash(string p)=>File.Exists(p)?Convert.ToHexString(SHA256.HashData(File.ReadAllBytes(p))):null;
            var formalPath=ProjectSettings.GlobalizePath(GameSession.SavePath);formal=new[]{formalPath,formalPath+".bak",formalPath+".tmp"}.ToDictionary(p=>p,Hash);
            try{Session.UseTestSavePath(formalPath);throw new InvalidOperationException("Unsafe save path accepted");}catch(ArgumentException){}
            saveLease=Session.UseTestSavePath(_save);_main=ResourceLoader.Load<PackedScene>("res://scenes/app/Main.tscn").Instantiate<MainController>();AddChild(_main);await Frames(20);
            await Click(TextButton("开始新游戏"));await Click(TextButton("开始游戏"));await Frames(20);
            var reminder=Find<AcceptDialog>().Single(d=>d.Name=="EntryControlReminder");await Click(reminder.GetOkButton());await Frames(10);
            var events=Session.State.CompletedEvents.Count;await KeyPress(Key.Escape);Require(Session.State.CompletedEvents.Count==events,"Canceled intro committed an event");await Settle();
            await PinBranch();
            await Interact("target.classroom.clock");Require(!Find<DreamMechanismPanel>().Any(p=>p.Visible),"Missing clock parts opened mechanism");
            foreach(var clue in new[]{"maintenance_card","morning_schedule","duty_note"})await Interact("observe:target.classroom."+clue);
            await Interact("puzzle:window_latches");var before=Fingerprint();await Click(Named("MechanismConfirm"));await Settle();Require(before==Fingerprint(),"Wrong window answer changed state");
            await Values("puzzle:window_latches",2,1,0);await Interact("pickup:dream.clock_hand");await Interact("target.classroom.blackboard");
            await Values("target.classroom.clock",6,0);await Values("target.classroom.duty_roster",2,0,1);await Sequence("puzzle:teacher_desk","3","7","2");
            await Interact("area:dream.corridor");await Interact("target.corridor.room_two_door");await Interact("area:dream.music");
            await Interact("target.music.score");await Sequence("puzzle:music_cage","1","4","2","3");await Reload("music-open",true);await PinBranch();
            foreach(var item in new[]{"rubber_mallet","listening_cone","metronome_weight","torn_piano_roll","wax_patch"})await Interact("pickup:dream."+item);
            foreach(var clue in new[]{"tempo_notice","fork_reference","roll_template"})await Interact("observe:target.music."+clue);
            await Interact("target.music.metronome");await Sequence("puzzle:music_channel_calibration","woodblock","piano","triangle","handbell");
            await Interact("puzzle:music_piano_roll");foreach(var index in new[]{0,1,1,3,2,3})await Click(Named("MechanismValue"+index));await Click(Named("MechanismConfirm"));await Settle();
            await Sequence("puzzle:music_acoustic_wall","woodblock","piano","triangle","handbell");
            await Interact("area:dream.corridor");await Interact("target.corridor.room_three_door");await Interact("area:dream.archive");await PinBranch();
            foreach(var clue in new[]{"registry_rule","registry_choir","registry_broadcast","registry_art","registry_track"})await Interact("observe:target.archive."+clue);
            await Interact("pickup:dream.archive_index_strip");await Sequence("puzzle:archive_retrieval","C1","A4","D2","B3");await Interact("observe:target.archive.fuse_route");
            await Interact("area:dream.corridor");await Interact("area:dream.classroom");await Interact("target.classroom.map_cabinet");await Reload("concentrated-backtrack");
            await Interact("area:dream.corridor");await Interact("area:dream.music");await Interact("puzzle:music_backstage_photo");
            for(var i=0;i<3;i++)await Click(Named("MechanismValue"+i));await Click(Named("PhotoCandidate2"));await Click(Named("MechanismConfirm"));await Settle();
            await Interact("area:dream.corridor");await Interact("area:dream.archive");await Interact("target.archive.recorder");
            Require(Dream.InstalledParts.Contains(DreamEscapeIds.SlotArchiveRecorderReel)&&!Dream.InstalledParts.Contains(DreamEscapeIds.SlotArchiveRecorderFuse),"Partial recorder fixture not reached by input");await Reload("recorder-partial");
            await Interact("assembly:archive_fuse");await Interact("target.archive.recorder");
            var installed=Fingerprint();Require(!Area.ActiveInteractionIds.Contains("target.archive.recorder"),"Solved recorder still offers installation");await KeyPress(Key.F);await Settle();Require(installed==Fingerprint(),"Repeated installation consumed/rewarded/committed twice");Log("recorder repeat is idempotent");
            await Sequence("puzzle:archive_work_timeline","open","calibrate","score","backstage");
            await Interact("area:dream.corridor");await Interact("target.corridor.room_four_door");await Interact("area:dream.auditorium");await PinBranch();
            await Interact("pickup:dream.curtain_counterweight");await Interact("pickup:dream.projector_lens");
            await Interact("observe:target.auditorium.line_set_notches");await Interact("observe:target.auditorium.projection_order");
            await Interact("target.auditorium.line_set");var mechanism=Find<DreamMechanismPanel>().Single();var initial=mechanism.PreviewTokens.ToArray();
            var focused=GetViewport().GuiGetFocusOwner();await Joy(JoyButton.DpadRight);Require(GetViewport().GuiGetFocusOwner()!=focused,"D-pad did not navigate stage controls");
            await Joy(JoyButton.A);Require(!mechanism.PreviewTokens.SequenceEqual(initial),"Gamepad A did not adjust stage");
            await Joy(JoyButton.B);Require(!mechanism.Visible && !Session.Modal.IsBlocked,"B did not close stage");
            await Interact("target.auditorium.line_set");Require(mechanism.PreviewTokens.SequenceEqual(initial),"Canceled preview persisted");
            await KeyPress(Key.Escape);Require(!mechanism.Visible && !Session.Modal.IsBlocked,"Escape did not close stage");Log("stage gamepad navigation/accept/back and Escape reset");
            await Values("target.auditorium.projector",2,0,3,1,0,1,2);
            await Interact("observe:target.auditorium.tape_order");await Sequence("puzzle:auditorium_tape_reconstruction","classroom","music","archive");await Interact("target.auditorium.master_player");
            await Sequence("puzzle:auditorium_handoff_plan","position","monitor","lights","release");await Reload("handoff-rehearsed");
            await Interact("action:left_lever",true);await Joy(JoyButton.X);Require(Find<DreamInventoryPanel>().Single().Visible,"Gamepad X inventory failed");await Joy(JoyButton.B);
            await Interact("action:open_exit",true);Require(Dream.Status==DreamEscapeStatus.InProgress,"Other modal did not reset cooperation");
            await Interact("action:left_lever",true);await Interact("area:dream.corridor");await Interact("area:dream.auditorium");
            await Interact("action:open_exit",true);Require(Dream.Status==DreamEscapeStatus.InProgress,"Leaving room retained cooperation");
            await Interact("action:left_lever",true);await Reload("cooperation-held");
            Require(!Find<DreamAuditoriumVisual3D>().Single().ExitLeverHeld,"Reload retained temporary lever hold");
            var actor=Area.GetNode<PlayerController3D>("Player");var position=actor.GlobalPosition;
            GetTree().Root.PropagateNotification((int)NotificationWMWindowFocusOut);KeyState(Key.W,true);await Frames(12);
            Require(actor.GlobalPosition.DistanceTo(position)<.01f&&actor.Velocity.Length()<.001f,"Lost focus retained movement");KeyState(Key.W,false);GetTree().Root.PropagateNotification((int)NotificationWMWindowFocusIn);await Frames();
            Log("cooperation reset by inventory and room return; synthetic focus clears movement");
            await Interact("action:left_lever",true);await Interact("action:handoff_monitor",true);await Interact("observe:target.auditorium.handoff_circuit",true);await Interact("action:open_exit",true);
            Require(Dream.Status==DreamEscapeStatus.CompletedEscape,"Escape did not wake");Require(Session.State.CompletedEvents.Count(e=>e.StartsWith("dream."))==13,"Expected 13 prologue events");
            Require(Session.State.World.Facts.Count==0&&Session.State.World.Evidence.Count==0&&Session.State.Investigation.Leads.Count==0,"Dream leaked into waking investigation");
            Require(_main.VillageRuntime is {IsInterior:true} && Session.State.VillageExploration!.WakeIntroductionComplete,"Escape wake missing");
            File.Copy(_save,Path.Combine(_output,"completed-escape.json"),true);var completed=Fingerprint();await Title();await Frames(15);await Click(TextButton("继续"));await Frames(60);await Settle();
            Require(completed==Fingerprint()&&_main.VillageRuntime is {IsInterior:true},"Exit reload did not restore completion");_reloads++;
            await Title();await Frames(10);Require(TextButton("开始新游戏").IsVisibleInTree(),"No title after completion");
            foreach(var pair in formal)Require(Hash(pair.Key)==pair.Value,"Formal save changed: "+pair.Key);
            Log($"PROLOGUE_E2E_PASS walks={_walks} meters={_meters:F2} clicks={_clicks} reloads={_reloads} pins={_pins} events=13 keyboard_mouse joypad isolated_save");
            File.WriteAllText(Path.Combine(_output,"report.json"),JsonSerializer.Serialize(new{status="PASS",input="synthetic keyboard Enter/F/W + captured mouse motion + joypad A/B/X/D-pad; real physics",walks=_walks,meters=_meters,activations=_clicks,reloads=_reloads,pinBranches=_pins,events=13,formalSaveAndBackupAndTemporaryUnchanged=true,saves=_saves,limitations="No human blind test, physical raw mouse delta, real Xbox, target GPU or export acceptance."},new JsonSerializerOptions{WriteIndented=true}));
            File.WriteAllText(Path.Combine(_output,"trace.json"),JsonSerializer.Serialize(_trace,new JsonSerializerOptions{WriteIndented=true}));GetTree().Quit();
        }
        catch(Exception e){GD.PushError($"PROLOGUE_E2E_FAIL step={_step}: {e}");if(_main is not null)GD.Print("VISIBLE "+string.Join(" | ",Find<Button>().Where(b=>b.IsVisibleInTree()).Select(b=>$"{b.Name}:{b.Text}")));if(_output.Length>0)File.WriteAllText(Path.Combine(_output,"trace.json"),JsonSerializer.Serialize(_trace));GetTree().Quit(1);}
        finally {KeyState(Key.W,false);saveLease?.Dispose();}
    }
}
