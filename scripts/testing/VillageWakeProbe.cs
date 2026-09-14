using System.Text.Json;
using System.Security.Cryptography;
using Godot;
using TownRpg.Application;
using TownRpg.Core.Domain;
using TownRpg.Core.Services;
using TownRpg.Presentation;

namespace TownRpg.Testing;

/// <summary>Isolated formal-route integration test; fixtures are explicitly distinguished from full input E2E.</summary>
public partial class VillageWakeProbe : Node
{
    private MainController _main=null!;
    private GameSession Session => GetNode<GameSession>("/root/GameSession");
    private VillageAreaHost Village => _main.VillageRuntime!;
    private readonly List<string> _checks=[];
    private string _output="",_save="";
    private IEnumerable<T> Find<T>() where T:Node => UiUxCapture.Find<T>(_main);
    private void Require(bool condition,string message) { if(!condition) throw new InvalidOperationException(message);_checks.Add(message);GD.Print("WAKE_CHECK "+message); }
    private async Task Frames(int n=5) { for(var i=0;i<n;i++) await ToSignal(GetTree(),SceneTree.SignalName.ProcessFrame); }
    private async Task Key(Key key)
    {
        Input.ParseInputEvent(new InputEventKey { Keycode=key,PhysicalKeycode=key,Pressed=true });await Frames(2);
        Input.ParseInputEvent(new InputEventKey { Keycode=key,PhysicalKeycode=key,Pressed=false });await Frames();
    }
    private async Task Click(string text)
    {
        var button=Find<Button>().First(b=>b.Text==text && b.IsVisibleInTree());
        button.GrabFocus();await Key(Godot.Key.Enter);
    }
    private async Task Settle(bool advance=true)
    {
        for(var i=0;i<1800;i++)
        {
            var button=Find<Button>().FirstOrDefault(b=>b.Name=="DialogueContinue" && b.IsVisibleInTree());
            if(button is not null)
            {
                if(!advance) return;
                button.GrabFocus();await Key(Godot.Key.Enter);
            }
            else await Frames(2);
            if(_main.VillageRuntime is not null && !Session.Modal.IsBlocked && !Village.IsTransitioning) { await Frames();return; }
            if(Find<Control>().Any(c=>c.Name=="WakeRecovery" && c.IsVisibleInTree())) throw new InvalidOperationException("WakeRecovery shown");
        }
        throw new InvalidOperationException("Wake did not settle");
    }
    private async Task LoadFixture(GameState state,bool advance=true)
    {
        if(_main is not null) { _main.QueueFree();await Frames(5); }
        File.WriteAllText(_save,SaveGameSerializer.Serialize(state));
        _main=ResourceLoader.Load<PackedScene>("res://scenes/app/Main.tscn").Instantiate<MainController>();AddChild(_main);await Frames(10);
        await Click("继续");await Settle(advance);
    }
    private async Task Reload()
    {
        await Key(Godot.Key.Escape);await Click("返回标题");await Frames(10);await Click("继续");await Settle();
    }
    private async Task Shot(string name)
    {
        await Frames(5);
        using var image=GetViewport().GetTexture().GetImage();
        var expectedWidth=int.Parse(name.Split('-')[0]);
        Require(image.GetWidth()==expectedWidth && image.GetHeight()==expectedWidth*9/16,"actual capture dimensions "+name);
        image.SavePng(Path.Combine(_output,name+".png"));
    }
    public override void _Ready()=>CallDeferred(MethodName.Run);
    private async void Run()
    {
        IDisposable? saveLease=null;
        try
        {
            if(!OS.GetCmdlineUserArgs().Contains("--prologue-e2e")) throw new InvalidOperationException("Missing isolated save flag");
            _output=ProjectSettings.GlobalizePath("res://outputs/"+(OS.GetCmdlineUserArgs().Contains("--terrain-adoption") ? "village-terrain-adoption/formal/" : "village-wake/integration/")+DateTime.Now.ToString("yyyyMMdd-HHmmss"));Directory.CreateDirectory(_output);
            _save=Path.Combine(_output,"save.json");saveLease=Session.UseTestSavePath(_save);
            var formal=ProjectSettings.GlobalizePath(GameSession.SavePath);
            string Hash(string path)=>File.Exists(path)?Convert.ToHexString(SHA256.HashData(File.ReadAllBytes(path))):"absent";
            var before=new[]{formal,formal+".bak",formal+".tmp"}.ToDictionary(p=>p,Hash);
            foreach(var size in new[]{new Vector2I(1280,720),new Vector2I(1600,900),new Vector2I(1920,1080)})
            {
                var state=GameState.CreateDreamPrologue();state.DreamEscape!.Status=DreamEscapeStatus.PinPendingWake;state.DreamEscape.Items.Clear();
                state.Settings.WindowWidth=size.X;state.Settings.WindowHeight=size.Y;
                state.Settings.BorderlessFullscreen=size.X==1920;
                await LoadFixture(state,false);await Shot($"{size.X}-wake");await Settle();
                Require(Session.State.DreamEscape!.Status==DreamEscapeStatus.CompletedPin && Session.State.VillageExploration!.WakeIntroductionComplete,"pin wake at "+size);
                Require(Village.IsInterior && Village.SafePose(Village.ActivePlayer.GlobalPosition),"safe bedroom spawn");
                Require(Village.Exterior.UseTerrainSample && !Village.Exterior.Surface.Ready,"formal terrain selected and cached collision disabled indoors");
                Require(Math.Abs(Village.ActivePlayer.Speed-3.1f)<.001f,"formal home speed remains 3.1");
                await Shot($"{size.X}-bedroom");
                var start=Village.ActivePlayer.GlobalPosition;
                Input.ActionPress("move_up");await Frames(10);Input.ActionRelease("move_up");await Frames();
                Require(Village.ActivePlayer.GlobalPosition.DistanceTo(start)>.1f,"wake returns movement");
                await Key(Godot.Key.Escape);await Shot($"{size.X}-pause");
                start=Village.ActivePlayer.GlobalPosition;Input.ActionPress("move_up");await Frames(10);Input.ActionRelease("move_up");
                Require(Village.ActivePlayer.GlobalPosition.DistanceTo(start)<.01f,"formal pause locks movement");await Click("继续");
                // Use the real interaction methods at legal fixture test positions.
                Village.ActivePlayer.GlobalPosition=new(-2.65f,0,3.05f);await Frames();
                Require(Village.Interior!.TryOperate("home.light.bedroom"),"bedroom switch interaction");
                Require(Village.Interior.TryOperate("home.door.bedroom"),"bedroom door interaction");await Frames(40);
                Require(Village.Interior.DoorOpen("bedroom"),"door reaches stable open endpoint");
                await Reload();Require(Village.Interior!.LightOn("bedroom") && Village.Interior.DoorOpen("bedroom"),"fixtures persist after reload");
                Require(!Find<DialoguePanel>().Any(p=>p.Visible),"completed wake does not replay");
                Village.ActivePlayer.GlobalPosition=new(0,0,3.05f);await Frames();await Shot($"{size.X}-home-exit");
                Require(Village.Interior.TryOperate("home.door.entry"),"home exit interaction");
                Require(!await Village.SwitchArea(false),"duplicate transition rejected");
                await Frames(60);Require(!Village.IsInterior,"exit arrives outside");
                Require(Village.Exterior.Surface.Ready && Village.Exterior.GetNodeOrNull("VillageGroundCollision") is null,"formal terrain collision active without old ground");
                Require(Math.Abs(Village.ActivePlayer.Speed-4.2f)<.001f,"formal village speed is 4.2");
                await Shot($"{size.X}-village");
                var exteriorPose=Village.ActivePlayer.GlobalPosition;await Reload();
                Require(!Village.IsInterior && Village.ActivePlayer.GlobalPosition.DistanceTo(exteriorPose)<.15f,"exterior pose restores");
                Require(Math.Abs(Village.ActivePlayer.Speed-4.2f)<.001f,"saved exterior restores new speed");
                Require(Village.Exterior.TryEnterHome(),"home entry interaction");await Frames(60);
                Require(Village.IsInterior && Village.Interior!.LightOn("bedroom"),"home return preserves fixtures");
                Require(!Village.Exterior.Surface.Ready,"home return disables terrain collision");
                Require(Session.State.World.Day==1 && Session.State.World.MinuteOfDay==480,"exploration leaves Saturday morning fixed");
                var corrupt=SaveGameSerializer.Deserialize(File.ReadAllText(_save));corrupt.VillageExploration!.X=9999;
                await LoadFixture(corrupt);Require(Village.SafePose(Village.ActivePlayer.GlobalPosition),"invalid spatial pose uses safe point");
                if(size.X==1280)
                {
                    foreach(var (x,z) in new[]{(-65f,58f),(-8f,18f),(40f,22f)})
                    {
                        var oldTerrain=SaveGameSerializer.Deserialize(File.ReadAllText(_save));
                        oldTerrain.Player.LocationId=VillageExplorationState.VillageLocation;
                        oldTerrain.VillageExploration!.X=x;oldTerrain.VillageExploration.Z=z;
                        oldTerrain.VillageExploration.Y=VillageLayout3D.WalkHeight(x,z)+.04f;
                        await LoadFixture(oldTerrain);
                        var restored=Village.ActivePlayer.GlobalPosition;
                        Require(new Vector2(restored.X-x,restored.Z-z).Length()<.1f && Village.SafePose(restored),$"old terrain save retains safe X/Z {x},{z}");
                        Require(Math.Abs(restored.Y-Village.Exterior.WalkHeight(x,z))<.1f,"old save settles on adopted surface");
                        await Reload();
                        Require(Village.ActivePlayer.GlobalPosition.DistanceTo(restored)<.1f,"adopted pose persists after reload");
                    }
                    var blocked=SaveGameSerializer.Deserialize(File.ReadAllText(_save));
                    blocked.VillageExploration!.X=-56;blocked.VillageExploration.Z=40;blocked.VillageExploration.Y=0;
                    await LoadFixture(blocked);
                    Require(Village.SafePose(Village.ActivePlayer.GlobalPosition) && Village.ActivePlayer.GlobalPosition.DistanceTo(Village.Exterior.SurfacePoint(-56,49))<.15f,"building overlap save falls back safely");
                }
            }
            // An actually earned v6 ExitOpened save exercises the existing final-dialogue continuation.
            var earned=ProjectSettings.GlobalizePath("res://outputs/late-rooms-v2/prologue-e2e/20260912-121512/exit-opened.json");
            if(!File.Exists(earned)) throw new FileNotFoundException("Earned ExitOpened fixture required",earned);
            await LoadFixture(SaveGameSerializer.Deserialize(File.ReadAllText(earned)));
            Require(Session.State.DreamEscape!.Status==DreamEscapeStatus.CompletedEscape && Session.State.DreamEscape.Items.Count==0,"earned ExitOpened continues into reality");
            Require(Session.State.CompletedEvents.Count(id=>id.StartsWith("dream."))==13,"13 dream events retained");
            var resume=SaveGameSerializer.Deserialize(File.ReadAllText(_save));resume.VillageExploration!.WakeIntroductionComplete=false;
            await LoadFixture(resume,false);_main.QueueFree();await Frames(5);_main=null!;
            await LoadFixture(SaveGameSerializer.Deserialize(File.ReadAllText(_save)));Require(Session.State.VillageExploration!.WakeIntroductionComplete,"interrupted wake resumes");
            var old=GameState.CreateDreamPrologue();old.DreamEscape!.Status=DreamEscapeStatus.CompletedEscape;old.DreamEscape.Items.Clear();old.DreamEscape.ContentVersion=5;
            await LoadFixture(old);Require(Village.IsInterior,"old completed dream migrates to bedroom");
            var legacy=GameState.CreateVerticalSlice();legacy.World.Facts.Add("slice.d1.awake");
            _main.QueueFree();await Frames(5);File.WriteAllText(_save,SaveGameSerializer.Serialize(legacy));
            _main=ResourceLoader.Load<PackedScene>("res://scenes/app/Main.tscn").Instantiate<MainController>();AddChild(_main);await Frames(10);await Click("继续");await Frames(15);
            Require(_main.VillageRuntime is null && Find<WorldSceneController>().Any(),"Legacy keeps original world");
            foreach(var pair in before) Require(Hash(pair.Key)==pair.Value,"formal save untouched: "+Path.GetFileName(pair.Key));
            _main.QueueFree();await Frames(5);_main=null!;GC.Collect();GC.WaitForPendingFinalizers();await Frames(5);
            File.WriteAllText(Path.Combine(_output,"report.json"),JsonSerializer.Serialize(new { status="PASS",checks=_checks,limitations="Synthetic input and fixture integration; human visual acceptance remains pending." },new JsonSerializerOptions{WriteIndented=true}));
            GD.Print("VILLAGE_WAKE_PASS checks="+_checks.Count);GetTree().Quit();
        }
        catch(Exception e) { GD.PushError("VILLAGE_WAKE_FAIL "+e);GetTree().Quit(1); }
        finally { Input.ActionRelease("move_up");saveLease?.Dispose(); }
    }
}
