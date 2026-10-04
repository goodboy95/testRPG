using Godot;
using DialogueManagerRuntime;
using System.Text.Json;
using TownRpg.Application;
using TownRpg.Core.Domain;
using TownRpg.Core.Services;

namespace TownRpg.Presentation;

/// <summary>Formal opening adapter. Rules belong to Core; the independent pilot never accesses this controller.</summary>
public partial class VillageOpeningController : Node
{
    public VillageAreaHost Host { get; init; } = null!;
    private GameSession Session => GetNode<GameSession>("/root/GameSession");
    public VillageOpeningService Service => Session.VillageOpening;
    private VillageOpeningState State => Service.State;
    public DialoguePanel Panel { get; private set; } = null!;
    public OpeningJournalPanel JournalPanel { get; private set; } = null!;
    public List<VillageNpcActor3D> Residents { get; } = [];
    public bool Busy { get; private set; }
    public string FeedbackText => _status.Text;
    private VillageExterior3D Area => Host.Exterior;
    private PlayerController3D Player => Area.PlayerActor;
    private readonly Dictionary<string, Resource> _stories = [];
    private readonly Dictionary<string, InteractionMarker3D> _markers = [];
    private readonly List<PackedScene> _models = [];
    private CancellationTokenSource? _conversation;
    private Label _status = null!;
    private double _toastTime;
    private bool _focus = true;
    private bool _leaving;
    private bool _worldReady;
    private HomePreviewController3D? _homeInteractions;
    public bool WorldReady => _worldReady;
    public Vector3 MarkerPosition(string id) => _markers[id].Position;
    private string _inputHint = "F 确认／交互　Esc 取消　J 事项";
    private readonly Dictionary<string, string> _houseNpc = new() { ["house02"] = "grandma_xu", ["house13"] = "uncle_chen", ["house15"] = "uncle_liu" };
    private string[] Goods => VillageOpeningIds.Houses.Select(h => Session.Content.OpeningGoods.Single(g=>g.House==h)).Select(g=>UiText.Get(g.ItemKey)+" ×"+g.Quantity).ToArray();

    public override void _Ready()
    {
        Service.Initialize();
        var ui = new CanvasLayer { Name = "OpeningUi", Layer = 35 }; AddChild(ui);
        var root = new Control { MouseFilter = Control.MouseFilterEnum.Ignore }; root.SetAnchorsAndOffsetsPreset(Control.LayoutPreset.FullRect); ui.AddChild(root);
        Panel = new DialoguePanel { Name = "OpeningDialogue" }; root.AddChild(Panel);
        Tracker = new ObjectiveTrackerView { Name = "ObjectiveTracker" }; root.AddChild(Tracker);
        JournalPanel = new OpeningJournalPanel { Name = "OpeningJournal", Service = Service, TrackingChanged = TrackingChanged }; root.AddChild(JournalPanel);
        _status = new Label { Position = new(12,48), Size = new(600,60), AutowrapMode = TextServer.AutowrapMode.WordSmart, MouseFilter = Control.MouseFilterEnum.Ignore };
        UiStyles.Font(_status,12); root.AddChild(_status);
        Host.LeavingExterior += PrepareHome;
        Host.CheckpointReached += AfterAreaChange;
        if (Area.IsInsideTree()) InitializeExterior();
        InitializeHome();
    }
    private void InitializeExterior()
    {
        if (_worldReady) return;
        var definitions = Session.Content.OpeningResidents.Select(r=>new VillageNpcDefinition(r.Id,r.Building,r.Points,r.Yaw,r.Model,"","start",r.Prompt)).ToArray();
        if (!definitions.Select(d => d.Id).ToHashSet().SetEquals(VillageOpeningIds.Residents)) throw new InvalidDataException("Opening needs exactly seven residents.");
        foreach (var definition in definitions)
        {
            var model = ResourceLoader.Load<PackedScene>(definition.Model) ?? throw new InvalidDataException("Missing opening model: " + definition.Id);
            _models.Add(model); var npc = new VillageNpcActor3D(); npc.Initialize(definition,Area,model); Area.AddChild(npc); Residents.Add(npc); Area.RegisterInteractionMarker(npc.Marker);
            npc.AddChild(new Label3D { Text = NpcName(definition.Id), Position = new(0,1.95f,0), PixelSize = .0035f, FontSize = 28, Billboard = BaseMaterial3D.BillboardModeEnum.Enabled });
        }
        foreach (var evt in Session.Content.Events.Events.Where(e => e.Id.StartsWith("village.opening.")))
            _stories.Add(evt.Id["village.opening.".Length..], ResourceLoader.Load<Resource>(evt.DialoguePath) ?? throw new InvalidDataException("Missing opening dialogue."));
        BuildLabor();
        var board = Area.Layout.Fixtures.Props.Single(p => p.Id == "Noticeboard");
        AddMarker("board", Area.SurfacePoint(board.Position[0],board.Position[2]+1.4f,.05f), "OPENING_BOARD");
        var desk = Area.Layout.Fixtures.Props.Single(p => p.Id == "VillageDesk");
        var mayor = Residents.Single(n => n.Definition.Id == "village_mayor");
        mayor.Position = Area.SurfacePoint(desk.Position[0]+1.3f,desk.Position[2],.05f);
        Area.AdditionalInteractionActive = marker => !Busy && !_placing && !_pushing && (Residents.Any(n => n.Marker == marker && n.CanTalk()) || _markers.Values.Contains(marker) && MarkerReachable(marker));
        Area.InteractionRequested += Interact;
        _worldReady = true;
        RecoverProps(); RefreshProps(); SelectNearestTarget();
    }
    private InteractionMarker3D AddMarker(string id, Vector3 position, string prompt, float range = 2)
    {
        var marker = new InteractionMarker3D { Name = "Opening_" + id, TargetId = "opening:"+id, Prompt = prompt, Position = position, InteractionRange = range };
        Area.AddChild(marker); Area.RegisterInteractionMarker(marker); _markers.Add(id,marker); return marker;
    }
    private bool MarkerReachable(InteractionMarker3D marker)
    {
        if (marker == _markers["stool"] && State.Stool is StoolLocation.Carried or StoolLocation.Porch or StoolLocation.Accepted) return false;
        if (marker == _markers["cart"] && !State.Invited) return false;
        var d = marker.GlobalPosition - Player.GlobalPosition;
        if (MathF.Abs(d.Y) > 1.2f) return false;
        using var ray = PhysicsRayQueryParameters3D.Create(Player.GlobalPosition + Vector3.Up, marker.GlobalPosition + Vector3.Up,1);
        ray.Exclude = new Godot.Collections.Array<Rid> { Player.GetRid(), _cart.GetRid() };
        return Area.GetWorld3D().DirectSpaceState.IntersectRay(ray).Count == 0;
    }
    private void Interact(string id)
    {
        if (Busy || Host.HostLocked || Host.IsTransitioning || !_focus) return;
        if (id.StartsWith("opening:") || id.StartsWith("npc:")) _ = Conversation(id);
    }
    public async Task Conversation(string id)
    {
        if (Busy || Session.Modal.IsBlocked || _placing || _pushing || _leaving) return;
        Busy = true;
        using var lease = Session.Modal.Acquire("village-opening");
        using var cts = new CancellationTokenSource(); _conversation = cts;
        var npc = Residents.FirstOrDefault(n => "npc:"+n.Definition.Id == id);
        npc?.BeginTalk(Player.GlobalPosition);
        try
        {
            await ReleaseButtons();
            if (id == "opening:breakfast")
            {
                if (!Service.Can("breakfast")) await Say("主角","已经吃过了，碗也收好了。");
                else if (await Choose("主角","还有馒头，热一下就能吃。",["吃点再出门","回来再吃"]) == 0) { await Say("主角","碗放好了，走吧。"); Commit("breakfast"); }
            }
            else if (id == "opening:wash")
            { await Say("主角",State.Washed ? "洗好了，水龙头也关上了。" : "洗把脸，清醒一点。嗯，这下好多了。"); Commit("wash"); }
            else if (id == "opening:board") await Board();
            else if (id == "opening:table") await Table();
            else if (id == "opening:stool")
            {
                if (Service.Can("take_stool"))
                {
                    await Say("主角", State.Stool == StoolLocation.Workshop ? "就是靠外这张。得拎住座面两边，别提凳脚。送到后让许婆婆试坐一下，再回来告诉彭师傅。" : "木凳还好好放着，拿起来接着送吧。");
                    if (await Choose("木凳", "送往许婆婆家，住宅巷的门廊。", ["双手拿起", "先放着"]) == 0) TakeStool();
                }
                else await Say("彭师傅","许婆婆的凳子修好了。事项在广场牌上，你要接的话先去记一下。");
            }
            else if (id == "opening:cart") await CartMenu();
            else if (id.StartsWith("opening:receive_")) await Receive(id["opening:receive_".Length..]);
            else if (npc is not null) await TalkNpc(npc.Definition.Id);
        }
        catch (OperationCanceledException) { }
        catch (Exception e) { GD.PushError(e.ToString()); Toast("操作未完成："+e.Message); }
        finally
        {
            if (!_leaving) { Panel.CloseDialogue(); npc?.EndTalk(); if (_worldReady && Area.IsInsideTree()) { Residents.Single(n=>n.Definition.Id=="shen_yan").LaborPose = _pushing; RefreshProps(); } }
            _conversation = null; Busy = false;
            if (_journalAfterConversation && !_leaving)
            {
                _journalAfterConversation = false;
                // Release the conversation modal before handing focus to the journal.
                lease.Dispose(); JournalPanel.Open();
            }
        }
    }
    private async Task Board()
    {
        if (Service.Can("posting"))
        {
            var qiao = Residents.Single(n => n.Definition.Id == "aunt_qiao"); var original = qiao.Position;
            qiao.Position = Residents.Single(n => n.Definition.Id == "village_mayor").Position + new Vector3(0,0,1.2f);
            try { await Story("posting"); } finally { if (!_leaving) qiao.Position = original; }
        }
        var options = new List<string>(); var operations = new List<string>();
        foreach (var pair in new[] { ("accept_stool","送还木凳 · 20 元"), ("accept_orders","三户订货 · 30 元") })
            if (Service.Can(pair.Item1)) { operations.Add(pair.Item1); options.Add(pair.Item2); }
        if (Service.CanInvite && !State.Invited) { operations.Add("invitation_info"); options.Add("三户米面 · 两人各 40 元"); }
        operations.Add("journal"); options.Add("查看已记事项"); options.Add("晚点再来");
        var choice = await Choose("任务板","选择当前要帮忙的事项；做完先向委托人交差，村长代管酬金。",options);
        if (choice >= operations.Count) return;
        if (operations[choice] == "journal") _journalAfterConversation = true;
        else if (operations[choice] == "invitation_info") await Say("任务板","许婆婆、陈叔、刘伯，每户米面各一袋。推车从杂货铺借还。你和同伴各 40 元，分别登记。去活动室门前找沈砚，把目的地和分酬谈清。");
        else await Story(operations[choice]);
    }
    private async Task TalkNpc(string id)
    {
        var options = new List<string>(); var actions = new List<string>();
        void Add(string action,string text) { if (Service.Can(action)) { actions.Add(action); options.Add(text); } }
        if (id == "worker_peng") Add("report_stool","告诉彭师傅试坐的结果");
        if (id == "aunt_qiao") { Add("report_orders","交回订单和空提袋"); Add("report_cart","确认米面送达、归还推车"); actions.Add("table"); options.Add("核对交货台"); }
        if (id == "village_mayor") { Add("claim",$"领取已登记酬金 · {Service.Payable} 元"); actions.Add("board"); options.Add("看看当前委托"); }
        if (id == "shen_yan") { Add("invite","邀请一起送米面 · 各 40 元"); Add("rest","一起歇会儿"); }
        if (id == "grandma_xu") Add("stool_received","请许婆婆试坐木凳");
        var house = _houseNpc.FirstOrDefault(p => p.Value == id).Key;
        if (house is not null) { actions.Add("receive"); options.Add("核对送来的东西"); }
        actions.Add("chat"); options.Add("聊聊近况"); options.Add("先忙去了");
        var answer = await Choose(NpcName(id),Greeting(id),options);
        if (answer >= actions.Count) return;
        var op = actions[answer];
        if (op == "table") await Table();
        else if (op == "board") await Board();
        else if (op == "receive") await Receive(house!);
        else if (op == "claim")
        {
            var amount = Service.Payable;
            var wageNote = State.Jobs[OpeningJob.Cart] >= OpeningProgress.Reported ? "沈砚那份单独记着。" : "";
            if (await Choose("村长",$"登记好的工钱一共是 {amount} 元。{wageNote}你现在要领吗？",["领取","先存着"]) == 0)
            { Commit("claim"); await Say("村长",$"你的 {amount} 元已经结清。做过的事都有记录，不会少你的。"); }
        }
        else if (op == "chat")
        {
            var variant = id switch { "grandma_xu" => State.Stool == StoolLocation.Accepted ? 1 : 0, "uncle_chen" => State.Delivered.Contains("house13") ? 1 : 0, "uncle_liu" => State.Delivered.Contains("house15") ? 1 : 0, "shen_yan" => State.Invited ? 1 : 0, _ => 0 };
            await Say(NpcName(id),UiText.Get($"OPENING_CHAT_{id.ToUpperInvariant()}_{variant}"));
            if (id == "aunt_qiao" && State.Jobs[OpeningJob.Orders] >= OpeningProgress.Reported && await Choose("乔姨","我留了点饭，你要吃就坐一会儿。碗在边上，吃完放盆里。",["吃一点","先不吃了"]) == 0)
                await Say("主角","碗收好了，谢谢乔姨。");
        }
        else
        {
            if (op == "invite" && await Choose("沈砚","送三家的米面，推车用完还店里，各 40 元。我把工具收好就能走。",["现在一起去","想添什么工具？","我先忙，待会再来"]) is var choice && choice != 0)
            { if (choice == 1) await Say("沈砚",UiText.Get("OPENING_CHAT_SHEN_YAN_1")); return; }
            await Story(op);
            if (op is "report_stool" or "report_orders" && Service.CanInvite && !State.Invited) await Say(NpcName(id),
                id == "aunt_qiao" ? "米面也备齐了，双人送货的事项在广场。你可以去看看，再问问沈砚有没有空搭手。" : "乔姨那边还有一批米面要送，事项在广场。你可以去看看，再问问沈砚有没有空搭手。");
            if (op == "report_cart") await Story("rest");
        }
    }
    private string Greeting(string id) => id switch
    {
        "worker_peng" => State.Jobs[OpeningJob.Stool] >= OpeningProgress.Reported ? "许婆婆用着稳就好。上午这件办妥了，你忙你的。" : State.Jobs[OpeningJob.Stool] == OpeningProgress.LaborDone ? "她试过了？用着怎么样？" : State.Jobs[OpeningJob.Stool] == OpeningProgress.Accepted ? State.Stool == StoolLocation.Workshop ? "靠外这张就是。松的横档已经换好了，你拎座面两边，别提凳脚。" : "凳子已经拿走了吧？送过去让许婆婆试坐一下，再回来告诉我。" : "许婆婆的凳子修好了。事项在广场牌上，你要接的话先去记一下。",
        "aunt_qiao" => Service.Finished ? "米面都到家了，台子也腾出来了。歇歇再忙。" : State.Jobs[OpeningJob.Orders] == OpeningProgress.Available ? "三家的小件都分好了，纸条在广场登记，接好了来交货台核对。" : State.Jobs[OpeningJob.Orders] == OpeningProgress.Accepted ? "许婆婆的洗衣皂、陈叔的棉绳、刘伯的毛巾。包上写着，先和单子对一遍。" : State.Jobs[OpeningJob.Orders] == OpeningProgress.LaborDone ? "都送到了吧？把单子给我看看，空袋放台边就好。" : "刚才那趟辛苦了。你要是累了，就坐一会儿。",
        "village_mayor" => Service.Payable > 0 ? "确认过的酬金都记上了。你现在领，还是先存着？" : "做完先跟托你办事的人说一声，我这里再结钱。",
        "shen_yan" => Service.Finished ? "你推得还行，下次转弯先说，我好换边扶。" : State.Invited ? State.CartReturned ? "车还回去了，咱们去跟乔姨说一声吧。" : State.LoadSecured ? "装卸的时候咱们互相喊一声，别一声不吭就松手啊。" : "走，咱们去杂货铺看看车。装卸的时候互相喊一声再松手。" : Service.CanInvite ? "手头的活办完了？我有空，去哪儿、怎么分，咱们先说清。" : "钳子快调顺了，我再试几下。你先忙，回来再聊。",
        "grandma_xu" => State.Stool == StoolLocation.Accepted ? "修好的凳子稳当，坐着择菜正合适。" : State.Stool == StoolLocation.Porch ? "放好了？我来试试晃不晃。" : State.Stool == StoolLocation.Carried ? "来，把凳子放在门廊靠墙这边吧，留出进门的地方。" : "来啦？我正准备择菜呢，你先歇一会儿。",
        "uncle_chen" => State.Delivered.Contains("house13") ? "棉绳已经收好了，正好够换晾晒那头的绑绳。辛苦你跑这一趟。" : "我把台上这点东西挪开。要放什么，先对一下。",
        _ => State.Delivered.Contains("house15") ? "毛巾我已经收好了。等把院子收拾完，我也歇一会儿。" : "放菜畦外边，别踩进泥里。我洗个手就来。"
    };
    private async Task Table()
    {
        var options = new List<string>(); var actions = new List<(string,string)>();
        for (var i = 0; i < 3; i++)
        {
            var h = VillageOpeningIds.Houses[i];
            if (Service.Can("pack",h)) { options.Add(VillageOpeningService.HouseName(h)+" · "+Goods[i]); actions.Add(("pack",h)); }
            if (Service.Can("load",h)) { options.Add(VillageOpeningService.HouseName(h)+" · 米一袋、面一袋"); actions.Add(("load",h)); }
        }
        if (Service.Can("cart_checked")) { options.Add("两人检查轮轴和车把"); actions.Add(("cart_checked","")); }
        if (Service.Can("cart_secured")) { options.Add("复核三户，收紧绑带"); actions.Add(("cart_secured","")); }
        options.Add("先离开");
        var pick = await Choose("乔姨的交货台","许婆婆：住宅巷；陈叔：溪西岸；刘伯：溪东岸。每包看名字和数量，可一次带齐，先送哪户自己定。",options);
        if (pick >= actions.Count) return;
        var (op,target) = actions[pick];
        if (op is "cart_checked" or "cart_secured") await Story(op);
        else
        {
            var expected = Array.IndexOf(VillageOpeningIds.Houses,target);
            var selection = await Choose("核对标签","把这包对应到订单上的哪一户？",["许婆婆","陈叔","刘伯","先放回台面"]);
            if (selection == expected)
            {
                if (op == "load" && _cart.Position.DistanceTo(_cartHome) > 3) { await Say("乔姨","先把车停回交货台旁边，再装这份。"); return; }
                Commit(op,target); LaborPose(true);
                await Say(op == "load" ? "沈砚" : "乔姨",op == "load" ? "这一份托住，好，放下。名字朝外，卸货时好找。" : "名字和数量对上了，放进提袋。其余几份也可以一起带走。"); LaborPose(false);
            }
            else if (selection < 3) await Say("乔姨","看看上面的名字，再和这一行对一对。货还在台上，没有拿走。");
        }
    }
    private async Task Receive(string house)
    {
        if (State.Stool == StoolLocation.Carried) { await Say("主角","先把凳子安全放下，腾出手再交货。"); return; }
        var actions = new List<string>(); var options = new List<string>();
        if (Service.Can("deliver",house)) { actions.Add("deliver"); options.Add("核对小件货包"); }
        if (Service.Can("unload",house)) { actions.Add("unload"); options.Add("两人卸下米面"); }
        if (house == "house02" && Service.Can("stool_received")) { actions.Add("stool_received"); options.Add("请许婆婆试坐"); }
        options.Add("先离开"); var pick = await Choose(VillageOpeningService.HouseName(house),Greeting(_houseNpc[house]),options);
        if (pick >= actions.Count) return;
        var op = actions[pick];
        if (op == "deliver")
        {
            var packs = State.Packed.Except(State.Delivered).Order().ToArray();
            var labels = packs.Select(h => VillageOpeningService.HouseName(h)+" · "+Goods[Array.IndexOf(VillageOpeningIds.Houses,h)]).Append("再对一下单子").ToArray();
            var selected = await Choose("提袋","取出写着收件人姓名的货包。",labels);
            if (selected >= packs.Length) return;
            if (packs[selected] != house) { await Say(VillageOpeningService.HouseName(house),house switch { "house02" => "我订的是洗衣皂，你看看是不是拿成旁人的了。", "house13" => "这名字不是我。要不把单子摊开对一下？", _ => "我的那份是毛巾，别把人家的落我这儿。" }); return; }
        }
        if (op == "unload" && _cart.GlobalPosition.DistanceTo(_receiving[house]) > 3.2f)
        { await Say("沈砚","先把车推到院口的收货处，停稳再卸。"); return; }
        LaborPose(op == "unload");
        try { await Story(op,op == "stool_received" ? "" : house); }
        finally { LaborPose(false); }
    }
    private async Task Story(string op,string target = "")
    {
        if (!Service.Can(op,target)) return;
        var key = VillageOpeningIds.StoryKey(op,target); var next = "start";
        if (op is "cart_checked" or "cart_secured") LaborPose(true);
        while (true)
        {
            await ReleaseButtons();
            using var line = await DialogueManager.GetNextDialogueLine(_stories[key],next,mutation_behaviour: MutationBehaviour.Skip).WaitAsync(_conversation!.Token);
            if (line is null) break;
            await Panel.ShowLineAsync(line.TranslationKey,line.Character == "主角" ? Session.PlayerName : line.Character,line.Text).WaitAsync(_conversation!.Token);
            next = line.NextId;
        }
        Commit(op,target);
        if (op is "cart_checked" or "cart_secured") LaborPose(false);
    }
    private async Task ReleaseButtons()
    {
        do { await ToSignal(GetTree(),SceneTree.SignalName.ProcessFrame); _conversation?.Token.ThrowIfCancellationRequested(); }
        while (Input.IsActionPressed("interact") || Input.IsActionPressed("ui_accept"));
    }
    private async Task Say(string speaker,string text)
    { await ReleaseButtons(); await Panel.ShowLineAsync("",speaker == "主角" ? Session.PlayerName : speaker,text).WaitAsync(_conversation!.Token); }
    private async Task<int> Choose(string speaker,string text,IReadOnlyList<string> options)
    { await ReleaseButtons(); return await Panel.ShowChoiceAsync("",speaker,text,options).WaitAsync(_conversation!.Token); }
    private void Commit(string op,string target = "",OpeningPose? pose = null)
    { if (Service.Execute(op,target,pose)) { _toastTime = 0; if (_worldReady && Area.IsInsideTree()) RefreshProps(); SelectNearestTarget(); Checkpoint(); } }
    public void Checkpoint()
    {
        try
        {
            new VillageExplorationService(Session.State).Capture(Host.IsInterior ? VillageExplorationState.HomeLocation : VillageExplorationState.VillageLocation,Host.Capture());
            Session.SaveCheckpoint(); Session.NotifyChanged();
        }
        catch (Exception e) { Toast("保存失败："+e.Message+"。进度仍在，请从暂停菜单重试保存。"); }
    }
    public void OpenJournal()
    {
        if (Busy || _placing || Session.Modal.IsBlocked) return;
        if (_pushing) ParkCart();
        JournalPanel.Open();
    }
    private static string NpcName(string id) => id switch { "aunt_qiao" => "乔姨", "shen_yan" => "沈砚", "worker_peng" => "彭师傅", "village_mayor" => "村长", "grandma_xu" => "许婆婆", "uncle_chen" => "陈叔", _ => "刘伯" };
    private void Toast(string text) { _status.Text = text; _toastTime = 8; }
    private void LaborPose(bool active)
    {
        var shen = Residents.Single(n => n.Definition.Id == "shen_yan");
        shen.LaborPose = active;
        if (active) { shen.Position = FindSafeGround(_cart.ToGlobal(new(1.3f,0,.1f)),.35f); shen.BeginTalk(_cart.Position); }
        else shen.EndTalk();
        Player.PlayInteractionPose(active ? "operate" : "observe",_cart.GlobalPosition);
    }
    private void InitializeHome()
    {
        if (Host.Interior is not { } home || _homeInteractions == home) return;
        _homeInteractions = home;
        foreach (var (id,prompt,p) in new[] { ("breakfast","厨房早餐",new Vector3(3.7f,0,1.1f)),("wash","洗手台",new Vector3(3.1f,0,-3.1f)) })
        {
            var marker = new InteractionMarker3D { Name="Opening_"+id,TargetId="opening:"+id,Prompt=prompt,Position=p,InteractionRange=1.4f };
            home.AddChild(marker); home.RegisterInteractionMarker(marker);
        }
        home.AdditionalInteractionActive = marker =>
        {
            using var query = PhysicsRayQueryParameters3D.Create(home.PlayerActor.GlobalPosition+Vector3.Up,marker.GlobalPosition+Vector3.Up,1);
            query.Exclude = new Godot.Collections.Array<Rid> { home.PlayerActor.GetRid() };
            return home.GetWorld3D().DirectSpaceState.IntersectRay(query).Count == 0;
        };
        home.InteractionRequested += Interact;
    }
    public override void _Notification(int what)
    { if (what == NotificationWMWindowFocusOut) _focus = false; else if (what == NotificationWMWindowFocusIn) _focus = true; }
    public override void _Input(InputEvent e)
    {
        if (e is InputEventKey { Echo: true }) return;
        if (e is InputEventJoypadButton or InputEventJoypadMotion) _inputHint = "A 确认／交互　B 取消　Y 事项";
        else if (e is InputEventKey or InputEventMouseButton) _inputHint = "F 确认／交互　Esc 取消　J 事项";
        if (Busy && e.IsActionPressed("ui_cancel")) { _conversation?.Cancel(); Panel.CloseDialogue(); GetViewport().SetInputAsHandled(); return; }
        if (!_focus || Host.IsInterior || Host.IsTransitioning || Host.HostLocked || Busy) return;
        if (_placing)
        {
            if (e.IsActionPressed("ui_cancel")) { _placing = false; Player.ExternalMotion = false; RefreshProps(); GetViewport().SetInputAsHandled(); }
            else if (e.IsActionPressed("interact")) { ConfirmPlacement(); GetViewport().SetInputAsHandled(); }
        }
        else if (_pushing && e.IsActionPressed("interact")) { ParkCart(); GetViewport().SetInputAsHandled(); }
        else if (State.Stool == StoolLocation.Carried && e.IsActionPressed("interact")) { _placing = true; _placeYaw = Mathf.Atan2(Player.FacingWorld.X,Player.FacingWorld.Z); Player.ExternalMotion = true; GetViewport().SetInputAsHandled(); }
    }
    public override void _ExitTree()
    {
        _leaving = true; _conversation?.Cancel();
        Host.LeavingExterior -= PrepareHome; Host.CheckpointReached -= AfterAreaChange;
        if (IsInstanceValid(_homeInteractions)) { _homeInteractions!.InteractionRequested -= Interact; _homeInteractions.AdditionalInteractionActive = null; }
        if (IsInstanceValid(Area)) { Area.InteractionRequested -= Interact; Area.AdditionalInteractionActive = null; }
        GC.KeepAlive(_models);
    }
}
