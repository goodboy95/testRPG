using Godot;
using TownRpg.Application;
using TownRpg.Core.Domain;
using TownRpg.Core.Services;
using TownRpg.Presentation;

namespace TownRpg.Testing;

/// <summary>Formal route + real UI/physics, using only an explicitly redirected disposable save.</summary>
public partial class VillageOpeningProbe : Node
{
    private GameSession Session => GetNode<GameSession>("/root/GameSession");
    private MainController _main = null!;
    private VillageAreaHost Host => _main.VillageRuntime!;
    private VillageOpeningController Opening => Host.GetNode<VillageOpeningController>("VillageOpening");
    private string _save = "";
    private int _checks;
    private string _formalHash = "";
    private static string Hash(string p)=>File.Exists(p)?Convert.ToHexString(System.Security.Cryptography.SHA256.HashData(File.ReadAllBytes(p))):"absent";
    private void Check(bool value,string message) { if (!value) throw new InvalidOperationException(message); GD.Print("OPENING_CHECK "+message); _checks++; }
    private async Task Frames(int n=3) { for(var i=0;i<n;i++) await ToSignal(GetTree(),SceneTree.SignalName.ProcessFrame); }
    private async Task Key(Key k)
    { Input.ParseInputEvent(new InputEventKey { Keycode=k,PhysicalKeycode=k,Pressed=true }); await Frames(2); Input.ParseInputEvent(new InputEventKey { Keycode=k,PhysicalKeycode=k,Pressed=false }); await Frames(); }
    private async Task Pad(JoyButton b)
    { Input.ParseInputEvent(new InputEventJoypadButton { ButtonIndex=b,Pressed=true }); await Frames(2); Input.ParseInputEvent(new InputEventJoypadButton { ButtonIndex=b,Pressed=false }); await Frames(); }
    private IEnumerable<T> Find<T>() where T:Node => UiUxCapture.Find<T>(_main);
    private async Task Click(string name)
    { var b=Find<Button>().First(b=>b.IsVisibleInTree() && b.Text == name); b.GrabFocus(); await Key(Godot.Key.Enter); }
    private async Task Load(GameState game)
    {
        if (_main is not null) { _main.QueueFree(); await Frames(5); }
        File.WriteAllText(_save,SaveGameSerializer.Serialize(game));
        _main=ResourceLoader.Load<PackedScene>("res://scenes/app/Main.tscn").Instantiate<MainController>(); AddChild(_main); await Frames(10); await Click("继续");
        for(var i=0;i<800;i++)
        {
            foreach(var b in Find<Button>().Where(b=>b.Name == "DialogueContinue" && b.IsVisibleInTree()).ToArray()) b.EmitSignal(BaseButton.SignalName.Pressed);
            await Frames(2);
            if (_main.VillageRuntime is not null && !Session.Modal.IsBlocked && !Host.IsTransitioning) return;
            if (Find<Control>().Any(c=>c.Name=="WakeRecovery" && c.IsVisibleInTree())) throw new InvalidOperationException("Wake recovery screen shown");
        }
        throw new TimeoutException("Formal wake timeout");
    }
    private async Task Talk(string target,params string[] choices)
    {
        var task=Opening.Conversation(target); var next=0;
        for(var i=0;i<1200 && !task.IsCompleted;i++)
        {
            await Frames(2);
            if (Opening.Panel.AwaitingChoice)
            {
                if (next>=choices.Length) throw new InvalidOperationException("Unexpected choices: "+string.Join(" / ",Opening.Panel.VisibleChoices.Select(b=>b.Text)));
                var b=Opening.Panel.VisibleChoices.FirstOrDefault(b=>b.Text.Contains(choices[next],StringComparison.Ordinal)) ?? throw new InvalidOperationException("Choice missing: "+choices[next]);
                next++; b.EmitSignal(BaseButton.SignalName.Pressed);
            }
            else if (Opening.Panel.AwaitingAdvance)
                Find<Button>().First(b=>b.Name=="DialogueContinue" && b.IsVisibleInTree()).EmitSignal(BaseButton.SignalName.Pressed);
        }
        await task; await Frames(); Check(next==choices.Length,"conversation "+target+" choices="+next);
    }
    private void MovePlayer(Vector3 p) { Host.Exterior.PlayerActor.GlobalPosition=p; Host.Exterior.PlayerActor.Velocity=Vector3.Zero; }
    public override void _Ready()=>CallDeferred(MethodName.Run);
    private async void Run()
    {
        IDisposable? isolated=null;
        try
        {
            _formalHash=Hash(ProjectSettings.GlobalizePath(GameSession.SavePath));
            isolated=Session.UseTestSavePath("res://outputs/village-opening/acceptance-save.json"); _save=ProjectSettings.GlobalizePath("res://outputs/village-opening/acceptance-save.json");
            foreach(var ending in new[]{ DreamEscapeStatus.CompletedPin,DreamEscapeStatus.CompletedEscape })
            {
                var g=GameState.CreateDreamPrologue(); g.DreamEscape!.Status=ending; g.DreamEscape.Items.Clear();
                await Load(g); Check(Host.IsInterior && Session.State.VillageOpening is not null,"formal wake initializes opening "+ending);
                await Talk("opening:breakfast","回来再吃"); Check(!Session.VillageOpening.State.BreakfastEaten,"optional breakfast deferred");
                await Talk("opening:wash"); Check(Session.VillageOpening.State.Washed,"optional washing");
                Check(await Host.SwitchArea(false),"leave home"); await Frames(20);
                Check(Opening.WorldReady && Opening.Residents.Count==7,"seven NPCs in formal v6");
            }
            var old=SaveGameSerializer.Deserialize(SaveGameSerializer.Serialize(Session.State)); old.VillageOpening=null;
            var oldText=SaveGameSerializer.Serialize(old); var oldPose=old.VillageExploration!.X;
            await Load(old);
            Check(Directory.GetFiles(Path.GetDirectoryName(_save)!,"acceptance-save.json.before-village-opening.*.bak").Any(p=>File.ReadAllText(p)==oldText),"old awake save backed up byte for byte before upgrade");
            Check(Session.State.VillageExploration!.X==oldPose && Session.State.VillageExploration.WakeIntroductionComplete,"migration preserves pose and skips waking");
            await Reachability();
            await NpcBlocking();
            var service=Session.VillageOpening;
            await Talk("npc:shen_yan","聊聊近况"); Check(!service.State.Invited,"early Shen visit does not join");
            MovePlayer(Opening.MarkerPosition("board")+Vector3.Right); await Frames();
            var canceled=Opening.Conversation("opening:board"); await Frames(8); await Key(Godot.Key.Escape); await canceled; await Frames();
            Check(!Session.State.CompletedEvents.Contains(VillageOpeningIds.Event("posting")),"canceled story does not complete event");
            await Talk("opening:board","送还木凳");
            await Talk("opening:board","三户订货");
            await TrackingChecks();
            service=Session.VillageOpening;
            MovePlayer(Opening.MarkerPosition("stool")+new Vector3(-1.1f,0,0)); await Frames();
            await Talk("opening:stool","双手拿起"); Check(service.State.Stool==StoolLocation.Carried,"physical pickup "+Opening.ClearanceDiagnostic(Host.ActivePlayer.Position,.68f));
            await Pad(JoyButton.A); Check(Opening.Placing,"synthetic A enters placement"); await Pad(JoyButton.B); Check(!Opening.Placing && service.State.Stool==StoolLocation.Carried,"synthetic B returns carrying");
            MovePlayer(Host.Exterior.HomeReturnPoint+new Vector3(0,0,3)); await Frames(); await Key(Godot.Key.F); await Frames(6); await Key(Godot.Key.F);
            Check(service.State.Stool==StoolLocation.Ground,"temporary safe placement");
            Check(service.State.Tracking!.Target=="stool","tracking temporary stool");
            await Talk("opening:stool","双手拿起");
            var porch=new System.Numerics.Vector2(Opening.StoolPorch.X,Opening.StoolPorch.Z);
            MovePlayer(Host.Exterior.SurfacePoint(porch.X,porch.Y+1.1f,.04f)); await Frames(); await Key(Godot.Key.F); await Frames(5); await Key(Godot.Key.F);
            Check(service.State.Stool==StoolLocation.Porch,"assisted porch placement "+Opening.ClearanceDiagnostic(Host.Exterior.SurfacePoint(porch.X,porch.Y,.04f),.41f)+" state="+service.State.Stool+" player="+Host.ActivePlayer.Position);
            await Talk("npc:grandma_xu","试坐"); Check(service.State.Stool==StoolLocation.Accepted,"stool stays after receipt");
            Check(service.State.Tracking!.Target=="npc:worker_peng","tracking report after trial sit");
            await Talk("npc:worker_peng","试坐的结果");
            for(var i=0;i<3;i++) await Talk("opening:table",new[]{"许婆婆 · 洗衣皂","陈叔 · 棉绳","刘伯 · 毛巾"}[i],new[]{"许婆婆","陈叔","刘伯"}[i]);
            Check(service.State.Packed.Count==3,"three packs prepared together");
            Opening.OpenJournal(); Opening.JournalPanel.Select(OpeningJob.Orders); await Frames();
            var nearest=service.Objective(OpeningJob.Orders).Targets.OrderBy(t=>Opening.TargetPosition(t.Id).DistanceSquaredTo(Host.ActivePlayer.GlobalPosition)).First().Id;
            await Click("跟踪这件事"); Check(service.State.Tracking!.Target==nearest,"journal chooses nearest eligible household");
            var targetButton=Find<Button>().Single(b=>b.Name=="Target_receive_house13");
            targetButton.GrabFocus(); await Frames();
            var clickPoint=targetButton.GetGlobalRect().GetCenter();
            GetViewport().PushInput(new InputEventMouseMotion { Position=clickPoint },true);
            GetViewport().PushInput(new InputEventMouseButton { ButtonIndex=MouseButton.Left, Position=clickPoint, Pressed=true },true); await Frames(2);
            GetViewport().PushInput(new InputEventMouseButton { ButtonIndex=MouseButton.Left, Position=clickPoint, Pressed=false },true); await Frames();
            Check(service.State.Tracking.Target=="receive_house13","mouse chooses another household");
            await Key(Godot.Key.Escape); MovePlayer(Opening.MarkerPosition("receive_house15")+Vector3.Back); await Frames();
            Check(service.State.Tracking.Target=="receive_house13","movement does not replace selected household");
            await Talk("opening:receive_house02","核对小件","陈叔"); Check(service.State.Delivered.Count==0,"wrong pack preserved");
            foreach(var h in new[]{"house15","house02","house13"}) await Talk("opening:receive_"+h,"核对小件",VillageOpeningService.HouseName(h));
            await Talk("npc:aunt_qiao","交回订单"); Check(service.CanInvite && service.Payable==50,"reports unlock invitation without money collection");
            await Talk("npc:shen_yan","邀请一起","待会再来"); Check(!service.State.Invited,"deferred invitation");
            await Talk("npc:shen_yan","邀请一起","现在一起");
            var shen=Opening.Residents.Single(n=>n.Definition.Id=="shen_yan");
            shen.Position=Host.Exterior.SurfacePoint(-90,75,.04f);
            Opening._PhysicsProcess(9); await Frames(3);
            Check(shen.Position.DistanceTo(Host.ActivePlayer.Position)<6,"blocked companion recovers at checked nearby ground");
            MovePlayer(Opening.MarkerPosition("table")+Vector3.Back); await Frames();
            await Talk("opening:table","检查轮轴");
            foreach(var h in VillageOpeningIds.Houses) await Talk("opening:table",VillageOpeningService.HouseName(h)+" · 米",VillageOpeningService.HouseName(h));
            await Talk("opening:table","收紧绑带");
            service.Track(OpeningJob.Cart,"receive_house13"); Opening.Checkpoint(); await Frames();
            Check(Opening.Tracker.Summary.Contains("推车"),"parked cart recovery target");
            Check(Opening.BeginPush(),"take cart handles "+Opening.ClearanceDiagnostic(Opening.CartBody.ToGlobal(new(0,0,-1.22f)),.28f)); await Frames();
            Host.ActiveCamera.SetYawImmediate(180); Host.ActiveCamera.SnapToTarget(); await Frames(8);
            using (var cartImage=GetViewport().GetTexture().GetImage()) cartImage.SavePng(ProjectSettings.GlobalizePath("res://outputs/village-opening/cart-loaded.png"));
            var held=Opening.CartBody.Position; Opening.Notification((int)NotificationWMWindowFocusOut); Input.ActionPress("move_up"); await Frames(12); Input.ActionRelease("move_up"); Opening.Notification((int)NotificationWMWindowFocusIn);
            Check(Opening.CartBody.Position.DistanceTo(held)<.001f,"focus loss stops cart");
            await Key(Godot.Key.Escape); held=Opening.CartBody.Position; Input.ActionPress("move_up"); await Frames(12); Input.ActionRelease("move_up"); Check(Opening.CartBody.Position.DistanceTo(held)<.001f,"pause stops cart"); await Click("继续"); await Frames();
            await Key(Godot.Key.Escape); await Click("脱离卡住：回安全点"); await Frames();
            Check(!Opening.Pushing && !Host.ActivePlayer.ExternalMotion && Host.ActivePlayer.Position.DistanceTo(Opening.CartBody.Position)>4,"unstick parks labor before moving player");
            MovePlayer(Opening.CartBody.Position+Vector3.Right*1.8f); Opening.BeginPush(); await Frames();
            var start=Opening.CartBody.Position; Input.ActionPress("move_up"); await Frames(45); Input.ActionRelease("move_up");
            Check(Opening.CartBody.Position.DistanceTo(start)>.15f,"cart advances with actual physics");
            start=Opening.CartBody.Position; Input.ActionPress("move_down"); await Frames(30); Input.ActionRelease("move_down");
            Check(Opening.CartBody.Position.DistanceTo(start)>.1f,"cart reverses");
            Input.ActionPress("move_right"); await Frames(25); Input.ActionRelease("move_right"); Check(MathF.Abs(Opening.CartBody.Rotation.Y)>.05f,"cart turns"); await Key(Godot.Key.F); Check(!Opening.Pushing,"F parks cart");
            await BridgeChecks();
            foreach(var h in new[]{"house13","house15","house02"})
            {
                Opening.CartBody.Position=Opening.ReceivingPoints[h]; MovePlayer(Opening.ReceivingPoints[h]+Vector3.Right*1.7f); await Frames();
                await Talk("opening:receive_"+h,"卸下米面");
            }
            Opening.CartBody.Position=Opening.MarkerPosition("table")+new Vector3(6.1f,0,1); MovePlayer(Opening.CartBody.Position+Vector3.Right*1.7f); await Frames();
            await Talk("opening:cart","停回原处"); await Talk("npc:aunt_qiao","归还推车");
            Check(service.Finished && service.Payable==90 && Session.State.Inventory.Money==300,"complete opening, unpaid, no chapter end");
            using (var blocked = new FileStream(_save+".tmp",FileMode.OpenOrCreate,System.IO.FileAccess.ReadWrite,FileShare.None))
            { Opening.Checkpoint(); Check(Opening.FeedbackText.Contains("保存失败"),"failed save reported with retry path"); }
            Opening.Checkpoint(); var saved=SaveGameSerializer.Deserialize(File.ReadAllText(_save));
            Check(saved.VillageOpening!.Jobs[OpeningJob.Cart]==OpeningProgress.Reported,"save retries without repeating labor");
            await Load(saved); service=Session.VillageOpening;
            Check(service.Finished && service.State.Unloaded.Count==3 && service.State.Stool==StoolLocation.Accepted,"full result survives formal reload");
            await Talk("npc:village_mayor","领取已登记","领取"); Check(Session.State.Inventory.Money==390 && service.State.CompanionPaid,"separate wage ledger exactly once");
            Check(await Host.SwitchArea(true),"return home after delivery"); Check(await Host.SwitchArea(false),"return outside after delivery"); Check(service.Finished,"house roundtrip preserves result");
            for(var i=0;i<3;i++) { await Host.SwitchArea(true); await Host.SwitchArea(false); }
            await UiSizes();
            Check(Hash(ProjectSettings.GlobalizePath(GameSession.SavePath))==_formalHash,"user save remains untouched");
            GD.Print($"VILLAGE_OPENING_PASS checks={_checks} isolatedSave=true ui=dialogue physics=cart"); GetTree().Quit(0);
        }
        catch(Exception e) { GD.PushError("VILLAGE_OPENING_FAIL "+e); GetTree().Quit(1); }
        finally { isolated?.Dispose(); }
    }
    private async Task TrackingChecks()
    {
        var s=Session.VillageOpening;
        Check(s.State.Tracking?.Job==OpeningJob.Stool,"new task does not steal tracking");
        await Frames(); Check(Opening.Tracker.Visible,"exploration tracking visible");
        var camera=Host.ActiveCamera.Camera;
        var front=camera.GlobalPosition-camera.GlobalBasis.Z*12;
        Opening.Tracker.ShowTarget(camera,Host.ActivePlayer.GlobalPosition,front,"测试","目标在前方","目标",1);
        Check(!Opening.Tracker.Offscreen,"front marker projected on screen");
        Opening.Tracker.ShowTarget(camera,Host.ActivePlayer.GlobalPosition,camera.GlobalPosition+camera.GlobalBasis.Z*12,"测试","目标在后方","目标",1);
        Check(Opening.Tracker.Offscreen && new Rect2(0,0,640,360).HasPoint(Opening.Tracker.PinPosition),"behind marker clamped to screen edge");
        Opening.OpenJournal(); await Frames(); Check(!Opening.Tracker.Visible,"modal hides tracker");
        await Pad(JoyButton.RightShoulder); Check(Opening.JournalPanel.SelectedJob==OpeningJob.Orders,"choose other task");
        await Click("跟踪这件事"); Check(s.State.Tracking!.Job==OpeningJob.Orders,"track button changes job");
        await Click("取消跟踪"); await Key(Godot.Key.Escape); await Frames();
        Check(s.State.Tracking.Job is null && !Opening.Tracker.Visible,"cancel stays untracked");
        await Talk("opening:board","查看已记事项"); await Frames();
        Check(Opening.JournalPanel.Visible && Session.Modal.IsBlocked,"board opens shared journal");
        await Key(Godot.Key.Escape); s.Track(OpeningJob.Stool); Opening.Checkpoint();
        Check(await Host.SwitchArea(true),"tracking home transition"); await Frames();
        Check(Opening.Tracker.Visible && Opening.Tracker.Summary.Contains("正门"),"indoor tracker points to exit");
        await Load(SaveGameSerializer.Deserialize(SaveGameSerializer.Serialize(Session.State))); await Frames();
        Check(Host.IsInterior && !Opening.WorldReady && Opening.Tracker.Visible && Opening.Tracker.Summary.Contains("正门"),"indoor save shows exit before exterior actors exist");
        Check(await Host.SwitchArea(false),"tracking returns outdoors"); await Frames();
        Check(Opening.Tracker.Summary.Contains("木凳"),"outdoor target restored");
    }
    private async Task Reachability()
    {
        foreach(var npc in Opening.Residents)
        {
            var reachable=false;
            for(var i=0;i<16 && !reachable;i++)
            {
                var p=npc.Position+new Vector3(MathF.Cos(i*Mathf.Tau/16),0,MathF.Sin(i*Mathf.Tau/16))*1.3f;
                p.Y=Host.Exterior.WalkHeight(p.X,p.Z)+.04f;
                if(Opening.ClearanceDiagnostic(p,.3f)!="") continue;
                MovePlayer(p); await Frames(); reachable=npc.CanTalk();
            }
            Check(reachable,"resident physically reachable "+npc.Definition.Id);
        }
    }
    private async Task UiSizes()
    {
        foreach(var size in new[]{new Vector2I(1280,720),new Vector2I(1600,900),new Vector2I(1920,1080)})
        {
            GetWindow().Size=size; Session.State.Settings.FontScale=size.X==1920?1.25f:1;
            Session.State.Settings.SubtitleFontScale=size.X==1600?1.25f:1;
            MovePlayer(Opening.MarkerPosition("board")+new Vector3(0,0,2)); Host.ActiveCamera.SnapToTarget(); await Frames(8);
            Opening.OpenJournal(); await Frames(8);
            Check(Opening.JournalPanel.Visible && Session.Modal.IsBlocked,"journal modal "+size.X);
            await Pad(JoyButton.RightShoulder);
            Check(Opening.JournalPanel.SelectedJob==OpeningJob.Orders,"controller journal task selection "+size.X);
            await Key(Godot.Key.Q); Check(Opening.JournalPanel.SelectedJob==OpeningJob.Stool,"keyboard journal task selection "+size.X);
            using var image=GetViewport().GetTexture().GetImage(); Check(image.GetWidth()==size.X,"actual viewport "+size.X);
            image.SavePng(ProjectSettings.GlobalizePath($"res://outputs/village-opening/{size.X}-journal.png"));
            await Key(Godot.Key.Escape);
        }
    }
    private async Task NpcBlocking()
    {
        var npc=Opening.Residents.Single(n=>n.Definition.Id=="uncle_liu");
        var player=Host.ActivePlayer;
        for(var i=0;i<16;i++)
        {
            var p=npc.Position+new Vector3(MathF.Cos(i*Mathf.Tau/16),0,MathF.Sin(i*Mathf.Tau/16))*1.3f;
            p.Y=Host.Exterior.WalkHeight(p.X,p.Z)+.04f;
            if(Opening.ClearanceDiagnostic(p,.3f)!="") continue;
            MovePlayer(p); break;
        }
        await Frames();
        var vector=CameraRelativeMovement.ToScreen((npc.Position-player.Position).Normalized(),player.MovementCamera!).Normalized();
        Input.ActionPress(vector.X>=0?"move_right":"move_left",MathF.Abs(vector.X));
        Input.ActionPress(vector.Y>=0?"move_down":"move_up",MathF.Abs(vector.Y));
        await Frames(45);
        foreach(var action in new[]{"move_left","move_right","move_up","move_down"}) Input.ActionRelease(action);
        var distance=new Vector2(player.Position.X-npc.Position.X,player.Position.Z-npc.Position.Z).Length();
        Check(distance is >.48f and <1.15f,"player collision stops at resident without passing through");
    }
    private async Task BridgeChecks()
    {
        foreach(var bridge in Host.Exterior.Layout.Geometry.Bridges)
        {
            Opening.CartBody.Position=new(bridge.CenterX-bridge.HalfSpan+1,bridge.DeckY+.05f,bridge.Z); Opening.CartBody.Rotation=new(0,Mathf.Pi/2,0);
            MovePlayer(Opening.CartBody.Position-Vector3.Right*1.4f); await Frames(5); Check(Opening.BeginPush(),"bridge handles "+bridge.Id);
            Input.ActionPress("move_up");
            for(var i=0;i<1800 && Opening.CartBody.Position.X<bridge.CenterX+bridge.HalfSpan-1;i++) await Frames(1);
            Input.ActionRelease("move_up"); Check(Opening.CartBody.Position.X>bridge.CenterX+bridge.HalfSpan-1,"cart crosses "+bridge.Id); await Key(Godot.Key.F);
        }
    }
}
