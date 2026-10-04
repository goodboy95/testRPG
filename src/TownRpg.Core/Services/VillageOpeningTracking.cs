using TownRpg.Core.Domain;

namespace TownRpg.Core.Services;

public sealed record OpeningTarget(string Id, string Name, string Action);
public sealed record OpeningObjective(OpeningJob Job, string Title, string Stage, bool Trackable, IReadOnlyList<OpeningTarget> Targets);

public static class OpeningTargets
{
    public static readonly HashSet<string> All = ["stool", "porch", "table", "cart_home", "npc:worker_peng",
        "npc:aunt_qiao", "npc:village_mayor", "npc:grandma_xu", "npc:shen_yan", "receive_house02", "receive_house13", "receive_house15"];
}

public sealed partial class VillageOpeningService
{
    public int Balance => game.Inventory.Money;
    public IReadOnlyList<OpeningObjective> Objectives() => Enum.GetValues<OpeningJob>()
        .Where(j => j != OpeningJob.Cart || CanInvite).Select(Objective).ToArray();

    public OpeningObjective Objective(OpeningJob job)
    {
        var p = State.Jobs[job];
        OpeningObjective At(string stage, params OpeningTarget[] targets) => new(job, Title(job), stage, targets.Length > 0, targets);
        OpeningTarget T(string id, string name, string action) => new(id, name, action);
        if (p == OpeningProgress.Paid) return At("这件事已经办好，酬金也领过了。");
        if (p == OpeningProgress.Reported) return At("已经交差，可以去村长那里领取酬金。", T("npc:village_mayor", "广场 · 村长", "找村长领取酬金"));
        if (p == OpeningProgress.LaborDone) return job == OpeningJob.Stool
            ? At("许婆婆已经试坐，回去告诉彭师傅结果。", T("npc:worker_peng", "修理铺 · 彭师傅", "向彭师傅交差"))
            : At("东西已经送到，回杂货铺向乔姨交差。", T("npc:aunt_qiao", "杂货铺 · 乔姨", "向乔姨交差"));
        if (p == OpeningProgress.Available && job != OpeningJob.Cart) return At("还没有接取，可以到广场任务板查看委托。");
        if (job == OpeningJob.Stool) return State.Stool switch
        {
            StoolLocation.Workshop => At("先去修理铺拿修好的木凳。", T("stool", "修理铺 · 待领木凳", "拿起修好的木凳")),
            StoolLocation.Ground => At("木凳暂时放在路上，拿起来就能继续送。", T("stool", "暂放的木凳", "拿回暂放的木凳")),
            StoolLocation.Carried => At("把木凳送到许婆婆家的门廊，靠墙放稳。", T("porch", "许婆婆家 · 门廊", "把木凳放在门廊")),
            _ => At("木凳已经放好，请许婆婆试坐。", T("npc:grandma_xu", "许婆婆", "请许婆婆试坐"))
        };
        if (job == OpeningJob.Orders)
        {
            var ready = VillageOpeningIds.Houses.Where(h => State.Packed.Contains(h) && !State.Delivered.Contains(h)).ToArray();
            return ready.Length == 0
                ? At("到杂货铺交货台核对姓名和数量，领取还没拿走的货包。", T("table", "杂货铺 · 交货台", "核对并领取货包"))
                : At("把手中的货包交给对应住户，先送哪一家都可以。", ready.Select(h => T("receive_" + h, HouseName(h) + "家 · 收货处", "把货包送给" + HouseName(h))).ToArray());
        }
        if (!CanInvite) return At("这件委托还没有开放。");
        if (!State.Invited) return At("去活动室门前找沈砚，问问他愿不愿意一起送米面。", T("npc:shen_yan", "活动室 · 沈砚", "邀请沈砚一起送货"));
        if (!State.CartChecked) return At("和沈砚一起到杂货铺检查推车。", T("table", "杂货铺 · 交货台", "一起检查推车"));
        if (!State.LoadSecured) return At(State.Loaded.Count < 3 ? "核对三户的米面，逐份装上推车。" : "米面已经装齐，再检查一下绑带。",
            T("table", "杂货铺 · 交货台", State.Loaded.Count < 3 ? "核对米面并装车" : "检查并收紧绑带"));
        var remaining = VillageOpeningIds.Houses.Where(h => !State.Unloaded.Contains(h)).ToArray();
        return remaining.Length > 0
            ? At("把米面推到收货处，停稳后请住户收货。", remaining.Select(h => T("receive_" + h, HouseName(h) + "家 · 收货处", "把米面送给" + HouseName(h))).ToArray())
            : At("三户都收到了，把空推车还到杂货铺。", T("cart_home", "杂货铺 · 还车处", "归还空推车"));
    }

    public void ReconcileTracking()
    {
        if (State.Tracking is null)
        {
            State.Tracking = new();
            var eligible = Objectives().Where(o => o.Trackable).ToArray();
            State.Tracking.Job = eligible.FirstOrDefault(o => State.Jobs[o.Job] is OpeningProgress.Accepted or OpeningProgress.LaborDone)?.Job
                ?? eligible.FirstOrDefault()?.Job;
        }
        var track = State.Tracking;
        if (track.Job is not { } job) { track.Target = null; return; }
        var objective = Objective(job);
        if (!objective.Trackable) { track.Job = null; track.Target = null; return; }
        if (!objective.Targets.Any(t => t.Id == track.Target)) track.Target = objective.Targets.Count == 1 ? objective.Targets[0].Id : null;
    }

    public bool Track(OpeningJob job, string? target = null)
    {
        if (!Enum.IsDefined(job)) return false;
        var objective = Objective(job);
        if (!objective.Trackable || target is not null && !objective.Targets.Any(t => t.Id == target)) return false;
        var previous = State.Tracking;
        State.Tracking = new() { Job = job, Target = target ?? (previous?.Job == job ? previous.Target : null) };
        ReconcileTracking(); return true;
    }

    public void StopTracking() => State.Tracking = new();
}
