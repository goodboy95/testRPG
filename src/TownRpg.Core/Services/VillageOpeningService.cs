using System.Text.Json;
using TownRpg.Core.Domain;

namespace TownRpg.Core.Services;

/// <summary>All task eligibility and idempotent state changes live here. Presentation requests operations.</summary>
public sealed partial class VillageOpeningService(GameState game)
{
    public VillageOpeningState State => game.VillageOpening ?? throw new InvalidOperationException("尚未进入村庄。");
    public bool Initialize()
    {
        if (game.VillageExploration is null) throw new InvalidOperationException("需要先醒来。");
        if (game.VillageOpening is not null) { ReconcileTracking(); return false; }
        game.VillageOpening = new() { Tracking = new() }; return true;
    }
    public bool CanInvite => State.Jobs[OpeningJob.Stool] >= OpeningProgress.Reported && State.Jobs[OpeningJob.Orders] >= OpeningProgress.Reported;
    public int Payable => State.Jobs.Where(x => x.Value == OpeningProgress.Reported).Sum(x => VillageOpeningIds.Rewards[x.Key]);
    public bool Finished => State.Jobs[OpeningJob.Cart] >= OpeningProgress.Reported;

    public bool Can(string operation, string target = "") => operation switch
    {
        "breakfast" => !State.BreakfastEaten,
        "wash" => !State.Washed,
        "posting" => !State.Conversations.Contains("posting"),
        "accept_stool" => State.Jobs[OpeningJob.Stool] == OpeningProgress.Available,
        "accept_orders" => State.Jobs[OpeningJob.Orders] == OpeningProgress.Available,
        "take_stool" => State.Jobs[OpeningJob.Stool] == OpeningProgress.Accepted && State.Stool is StoolLocation.Workshop or StoolLocation.Ground,
        "put_stool" => State.Stool == StoolLocation.Carried,
        "porch_stool" => State.Stool == StoolLocation.Carried,
        "stool_received" => State.Stool == StoolLocation.Porch,
        "report_stool" => State.Jobs[OpeningJob.Stool] == OpeningProgress.LaborDone,
        "pack" => State.Jobs[OpeningJob.Orders] == OpeningProgress.Accepted && ValidHouse(target) && !State.Packed.Contains(target),
        "deliver" => State.Jobs[OpeningJob.Orders] == OpeningProgress.Accepted && State.Packed.Contains(target) && !State.Delivered.Contains(target),
        "report_orders" => State.Jobs[OpeningJob.Orders] == OpeningProgress.LaborDone,
        "invite" => CanInvite && !State.Invited,
        "cart_checked" => State.Invited && !State.CartChecked,
        "load" => State.CartChecked && !State.LoadSecured && ValidHouse(target) && !State.Loaded.Contains(target),
        "cart_secured" => State.Loaded.Count == 3 && !State.LoadSecured,
        "unload" => State.LoadSecured && State.Jobs[OpeningJob.Cart] == OpeningProgress.Accepted && ValidHouse(target) && !State.Unloaded.Contains(target),
        "return_cart" => State.Unloaded.Count == 3 && !State.CartReturned,
        "report_cart" => State.CartReturned && State.Jobs[OpeningJob.Cart] == OpeningProgress.LaborDone,
        "claim" => Payable > 0,
        "rest" => Finished && !State.Conversations.Contains("rest"),
        _ => false
    };

    // Dialogue is played with mutations skipped. Only its successful end calls this atomic command.
    public bool Execute(string operation, string target = "", OpeningPose? pose = null)
    {
        if (!Can(operation, target)) return false;
        var previous = game.VillageOpening!;
        var money = game.Inventory.Money;
        var events = new HashSet<string>(game.CompletedEvents);
        game.VillageOpening = JsonSerializer.Deserialize<VillageOpeningState>(JsonSerializer.Serialize(previous))!;
        try
        {
            var s = State;
            switch (operation)
            {
                case "breakfast": s.BreakfastEaten = true; break;
                case "wash": s.Washed = true; break;
                case "posting": case "rest": s.Conversations.Add(operation); break;
                case "accept_stool": s.Jobs[OpeningJob.Stool] = OpeningProgress.Accepted; break;
                case "accept_orders": s.Jobs[OpeningJob.Orders] = OpeningProgress.Accepted; break;
                case "take_stool": s.Stool = StoolLocation.Carried; break;
                case "put_stool": RequirePose(pose); s.StoolGround = pose; s.Stool = StoolLocation.Ground; break;
                case "porch_stool": if (pose is not null) { RequirePose(pose); s.StoolGround = pose; } s.Stool = StoolLocation.Porch; break;
                case "stool_received": s.Stool = StoolLocation.Accepted; s.Jobs[OpeningJob.Stool] = OpeningProgress.LaborDone; break;
                case "pack": s.Packed.Add(target); break;
                case "deliver": s.Delivered.Add(target); if (s.Delivered.Count == 3) s.Jobs[OpeningJob.Orders] = OpeningProgress.LaborDone; break;
                case "invite": s.Invited = true; s.Jobs[OpeningJob.Cart] = OpeningProgress.Accepted; break;
                case "cart_checked": s.CartChecked = true; break;
                case "load": s.Loaded.Add(target); break;
                case "cart_secured": s.LoadSecured = true; break;
                case "unload": s.Unloaded.Add(target); break;
                case "return_cart": s.CartReturned = true; s.Jobs[OpeningJob.Cart] = OpeningProgress.LaborDone; break;
                case "report_stool": s.Jobs[OpeningJob.Stool] = OpeningProgress.Reported; break;
                case "report_orders": s.Jobs[OpeningJob.Orders] = OpeningProgress.Reported; break;
                case "report_cart": s.Jobs[OpeningJob.Cart] = OpeningProgress.Reported; s.CompanionDue = VillageOpeningIds.CompanionReward; break;
                case "claim":
                    game.Inventory.Money = checked(money + Payable);
                    foreach (var job in s.Jobs.Keys.ToArray()) if (s.Jobs[job] == OpeningProgress.Reported) s.Jobs[job] = OpeningProgress.Paid;
                    if (s.CompanionDue > 0) s.CompanionPaid = true;
                    break;
                default: throw new InvalidOperationException("Unknown opening operation.");
            }
            if (VillageOpeningIds.StoryOperations.Contains(operation) || operation is "deliver" or "unload") game.CompletedEvents.Add(VillageOpeningIds.Event(VillageOpeningIds.StoryKey(operation, target)));
            ReconcileTracking();
            if (s.Tracking!.Job is null && operation is "accept_stool" or "accept_orders" or "invite")
                Track(operation == "accept_stool" ? OpeningJob.Stool : operation == "accept_orders" ? OpeningJob.Orders : OpeningJob.Cart);
            Validate(game); return true;
        }
        catch { game.VillageOpening = previous; game.Inventory.Money = money; game.CompletedEvents = events; throw; }
    }

    public void ParkCart(OpeningPose pose) { RequirePose(pose); if (!State.Invited) throw new InvalidOperationException("尚未接取配送。"); State.CartParking = pose; }
    public void RecoverCarriedStool(OpeningPose pose)
    { if (State.Stool == StoolLocation.Carried) Execute("put_stool", pose: pose); }
    public string NextStep(OpeningJob job)
    {
        var s = State; var p = s.Jobs[job];
        if (p == OpeningProgress.Paid) return "已交差，酬金已领取。";
        if (p == OpeningProgress.Reported) return $"已交差，村长处可领 {VillageOpeningIds.Rewards[job]} 元。";
        if (p == OpeningProgress.LaborDone) return job == OpeningJob.Stool ? "回修理铺向彭师傅交差。" : "回杂货铺向乔姨交差。";
        return job switch
        {
            OpeningJob.Stool => p == OpeningProgress.Available ? "到广场登记送凳，再去修理铺。报酬 20 元。" : s.Stool == StoolLocation.Porch ? "请许婆婆试坐。" : "把修好的木凳送到住宅巷许婆婆家门廊。可暂放，无限时。",
            OpeningJob.Orders => p == OpeningProgress.Available ? "到杂货铺领取三户订单，30 元。" : $"到交货台核对货包，再送许婆婆、陈叔、刘伯。已交付：{HouseList(s.Delivered)}。",
            _ => !s.Invited ? "去活动室门前邀请沈砚。三户米面配送，每人 40 元。" : !s.CartChecked ? "两人到杂货铺检查推车。" : !s.LoadSecured ? "核对三户米面，逐批装车，再检查绑带。" : s.Unloaded.Count < 3 ? $"推车送往三户；院口停靠后卸货。已交付：{HouseList(s.Unloaded)}。" : "将空车归还杂货铺。"
        };
    }
    public string Journal() => string.Join("\n\n", Enum.GetValues<OpeningJob>().Where(j => j != OpeningJob.Cart || CanInvite).Select(j => Title(j) + "\n" + NextStep(j))) +
        (State.Jobs[OpeningJob.Orders] >= OpeningProgress.Accepted ? "\n\n订单与路向\n许婆婆：住宅巷门廊，洗衣皂两块。\n陈叔：溪西岸晾晒院，棉绳一卷。\n刘伯：溪东岸菜畦外，毛巾两条。\n提袋未交付：" + HouseList(State.Packed.Except(State.Delivered)) + "。" : "") +
        $"\n\n村长代管：待领 {Payable} 元。当前余额 {game.Inventory.Money} 元。";
    public static string Title(OpeningJob job) => job switch { OpeningJob.Stool => "送还木凳", OpeningJob.Orders => "三户订货", _ => "一起送米面" };
    public static string HouseName(string house) => house switch { "house02" => "许婆婆", "house13" => "陈叔", "house15" => "刘伯", _ => throw new ArgumentException("Unknown household.") };
    private static string HouseList(IEnumerable<string> values) => values.Any() ? string.Join("、", values.Select(HouseName)) : "尚未交付";
    private static bool ValidHouse(string house) => VillageOpeningIds.Houses.Contains(house);
    private static void RequirePose(OpeningPose? p)
    { if (p is null || !float.IsFinite(p.X) || !float.IsFinite(p.Y) || !float.IsFinite(p.Z) || !float.IsFinite(p.Yaw) || MathF.Abs(p.X) > 500 || MathF.Abs(p.Z) > 500 || p.Y is < -20 or > 100) throw new InvalidDataException("Invalid opening safe pose."); }
    public static void Validate(GameState game)
    {
        if (game.VillageOpening is not { } s) return;
        void Check([System.Diagnostics.CodeAnalysis.DoesNotReturnIf(false)] bool condition) { if (!condition) throw new InvalidDataException("Invalid village opening checkpoint."); }
        Check(game.VillageExploration is not null && s.Version == 1 && s.Jobs is not null && s.Packed is not null && s.Delivered is not null && s.Loaded is not null && s.Unloaded is not null && s.Conversations is not null);
        Check(s.Jobs!.Count == 3 && Enum.GetValues<OpeningJob>().All(s.Jobs.ContainsKey) && s.Jobs.Values.All(Enum.IsDefined) && Enum.IsDefined(s.Stool));
        Check(s.Packed!.All(ValidHouse) && s.Delivered!.IsSubsetOf(s.Packed) && s.Loaded!.All(ValidHouse) && s.Unloaded!.IsSubsetOf(s.Loaded));
        Check(s.Tracking is null || (s.Tracking.Job is null || Enum.IsDefined(s.Tracking.Job.Value)) &&
            (s.Tracking.Target is null || OpeningTargets.All.Contains(s.Tracking.Target)));
        Check(s.Conversations!.All(x => x is "posting" or "rest"));
        Check((s.Stool == StoolLocation.Accepted) == (s.Jobs[OpeningJob.Stool] >= OpeningProgress.LaborDone));
        Check(s.Stool == StoolLocation.Workshop || s.Jobs[OpeningJob.Stool] >= OpeningProgress.Accepted);
        Check((s.Delivered.Count == 3) == (s.Jobs[OpeningJob.Orders] >= OpeningProgress.LaborDone));
        Check(s.Packed.Count == 0 || s.Jobs[OpeningJob.Orders] >= OpeningProgress.Accepted);
        Check(!s.Invited || s.Jobs[OpeningJob.Stool] >= OpeningProgress.Reported && s.Jobs[OpeningJob.Orders] >= OpeningProgress.Reported);
        Check(s.Invited == (s.Jobs[OpeningJob.Cart] >= OpeningProgress.Accepted));
        Check(!s.CartChecked || s.Invited); Check(s.Loaded.Count == 0 || s.CartChecked);
        Check(!s.LoadSecured || s.Loaded.Count == 3); Check(s.Unloaded.Count == 0 || s.LoadSecured);
        Check(!s.CartReturned || s.Unloaded.Count == 3); Check(s.CartReturned == (s.Jobs[OpeningJob.Cart] >= OpeningProgress.LaborDone));
        Check(s.CompanionDue == (s.Jobs[OpeningJob.Cart] >= OpeningProgress.Reported ? VillageOpeningIds.CompanionReward : 0));
        Check(s.CompanionPaid == (s.Jobs[OpeningJob.Cart] == OpeningProgress.Paid));
        Check(!s.Conversations.Contains("rest") || s.Jobs[OpeningJob.Cart] >= OpeningProgress.Reported);
        Check(s.Stool != StoolLocation.Ground || s.StoolGround is not null);
        if (s.StoolGround is not null) RequirePose(s.StoolGround);
        if (s.CartParking is not null) { RequirePose(s.CartParking); Check(s.Invited); }
        foreach (var h in VillageOpeningIds.Houses)
        {
            Check(s.Delivered.Contains(h) == game.CompletedEvents.Contains(VillageOpeningIds.Event("deliver_" + h)));
            Check(s.Unloaded.Contains(h) == game.CompletedEvents.Contains(VillageOpeningIds.Event("unload_" + h)));
        }
        foreach (var op in VillageOpeningIds.StoryOperations)
        {
            var happened = op switch {
                "posting" or "rest" => s.Conversations.Contains(op), "accept_stool" => s.Jobs[OpeningJob.Stool] >= OpeningProgress.Accepted,
                "accept_orders" => s.Jobs[OpeningJob.Orders] >= OpeningProgress.Accepted, "stool_received" => s.Stool == StoolLocation.Accepted,
                "report_stool" => s.Jobs[OpeningJob.Stool] >= OpeningProgress.Reported, "report_orders" => s.Jobs[OpeningJob.Orders] >= OpeningProgress.Reported,
                "invite" => s.Invited, "cart_checked" => s.CartChecked, "cart_secured" => s.LoadSecured, "report_cart" => s.Jobs[OpeningJob.Cart] >= OpeningProgress.Reported, _ => false };
            Check(happened == game.CompletedEvents.Contains(VillageOpeningIds.Event(op)));
        }
    }
}
