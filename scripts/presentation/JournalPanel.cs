using Godot;
using TownRpg.Application;
using TownRpg.Core.Domain;
using TownRpg.Core.Services;

namespace TownRpg.Presentation;

public partial class JournalPanel : Control
{
    public event Action? Closed;
    private ManagementMenuShell _shell = null!;
    public override void _Ready()
    {
        SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect);
        _shell = new ManagementMenuShell(); AddChild(_shell); _shell.SetTitle(UiText.Get("UI_JOURNAL"));
        _shell.CloseRequested += () => Closed?.Invoke(); Visible = false;
    }
    public void Open(GameSession session, string dialogueHistory)
    {
        var state = session.State;
        var entries = new List<ArchiveEntry>();
        Add("risk", "UI_RISK", [ExposureLabel(state.World.Exposure)]);
        var relation = new List<string>();
        foreach (var npc in new[] { CharacterIds.AbilityHolder, CharacterIds.KnowledgeFriend, CharacterIds.TokenHolder, CharacterIds.Shopkeeper })
        {
            var r = state.Relationships.FirstOrDefault(x => x.SourceId == npc && x.TargetId == CharacterIds.Protagonist);
            relation.Add(r is null ? $"{WorldSceneController.CharacterName(npc)}\n好感：陌生　信赖：戒备" :
                $"{WorldSceneController.CharacterName(npc)}\n好感：{r.AffinityLabel}　信赖：{r.TrustLabel}");
        }
        Add("people", "UI_PEOPLE", relation);
        var schedules = new List<string>();
        var service = new ScheduleService(session.Content.Schedules.Entries);
        foreach (var id in state.Investigation.KnownScheduleEntries.OrderBy(x => x, StringComparer.Ordinal))
        {
            var next = service.Current(id, state.World) ?? service.NextVisible(id, state.World);
            schedules.Add(next is null ? $"{WorldSceneController.CharacterName(id)}：还不知道接下来会去哪里。" :
                $"{WorldSceneController.CharacterName(id)}\n第 {next.Day ?? state.World.Day} 日 {FormatTime(next.StartMinute)}–{FormatTime(next.EndMinute)}，{LocationLabel(next.LocationId)}。");
        }
        Add("schedules", "UI_SCHEDULES", schedules);
        Add("promises", "UI_PROMISES", state.Promises.Select(p => $"{p.Description}\n{PromiseLabel(p.Status)}"));
        Add("facts", "UI_FACTS", state.World.Facts.Select(FactLabel));
        Add("evidence", "UI_EVIDENCE", state.World.Evidence.Select(EvidenceLabel));
        Add("leads", "UI_LEADS", state.Investigation.Leads.Select(LeadLabel));
        Add("timeline", "UI_TIMELINE", state.Investigation.Timeline.Select(e => $"第 {e.Day} 日　{TimelineLabel(e.TextKey)}"));
        Add("history", "UI_DIALOGUE_HISTORY", string.IsNullOrWhiteSpace(dialogueHistory) ? [] : [dialogueHistory]);
        _shell.SetTitle(UiText.Get("UI_JOURNAL") + $"　·　第 {state.World.Day} 日");
        _shell.SetPages([new ManagementPage("journal", UiText.Get("UI_JOURNAL"), "", () => true,
            () => new ArchiveReader { Entries = entries }, c => ((ArchiveReader)c).SelectionControl)]);
        Visible = true; _shell.FocusContent();
        void Add(string id, string titleKey, IEnumerable<string> lines)
        {
            var text = string.Join("\n\n", lines);
            entries.Add(new ArchiveEntry("journal." + id, $"第 {state.World.Day} 日 {FormatTime(state.World.MinuteOfDay)}", UiText.Get(titleKey),
                text.Length == 0 ? UiText.Get("UI_JOURNAL_EMPTY") : text));
        }
    }
    public void ClosePanel() { UiStyles.ReleaseFocus(this); Visible = false; }
    public void SetGamepadPrompts(bool useGamepad) => _shell.SetGamepad(useGamepad);

    private static string FormatTime(int minute) => $"{minute / 60:00}:{minute % 60:00}";

    private static string ExposureLabel(ExposureStage stage) => stage switch
    {
        ExposureStage.Unnoticed => "无人注意",
        ExposureStage.Watched => "疑似受到监视",
        ExposureStage.Suspected => "被重点怀疑",
        ExposureStage.Identified => "身份已被确认",
        _ => stage.ToString()
    };

    private static string PromiseLabel(PromiseStatus status) => status switch
    {
        PromiseStatus.Pending => "待履行",
        PromiseStatus.Fulfilled => "已履行",
        PromiseStatus.Explained => "已解释",
        PromiseStatus.Broken => "已违约",
        _ => status.ToString()
    };

    private static string FactLabel(string fact) => fact switch
    {
        "slice.d1.awake" => "暑假留在旧宅，并听见了不该出现的列车汽笛。",
        "slice.d1.met_lin_xiaoyu" => "重新见到了林晓雨。",
        "slice.d1.met_zhou_ming" => "和旧友周明聊过近况。",
        "slice.d1.met_shen_yan" => "认识了带着旧怀表的沈砚。",
        "slice.d1.met_aunt_qiao" => "乔姨仍在经营车站旁的杂货铺。",
        "slice.d1.shared_activity_chosen" => "这个暑假重新与一位伙伴走近。",
        "slice.promise.rail_notice" => "答应与伙伴一起查看车站的新工程告示。",
        "slice.d2.railway_notice_read" => "开发公司将重新开挖废弃铁路隧道。",
        "slice.d2.anomaly_concealed" => "尽量遮挡视线，保护了晓雨的能力秘密。",
        "slice.d2.ability_seen_from_slope" => "坡上有人看见了晓雨与异象共鸣。",
        "slice.d2.anomaly_withdrawal" => "第一次接触异象时优先组织了撤离。",
        "slice.d2.first_anomaly_resolved" => "旧隧道里的异常共鸣暂时平静。",
        _ => fact.Replace('_', ' ')
    };

    private static string LeadLabel(string lead) => lead switch
    {
        "slice.lead.visit_abandoned_railway" => "主动调查镇北的废弃隧道。",
        "slice.lead.river_recording" => "比对河岸录音中的异常低音。",
        "slice.lead.compare_old_rail_maps" => "继续比较不同时期的旧铁路地图。",
        "slice.lead.watch_points_north" => "确认铁路怀表为什么总是指向北山。",
        "slice.lead.old_tunnel_below" => "调查共鸣指向的旧隧道下层。",
        "slice.lead.return_to_old_railway_prepared" => "准备充分后重返废弃支线。",
        _ => lead.Replace('_', ' ')
    };

    private static string TimelineLabel(string key) => key switch
    {
        "timeline.new_day" => "新的一天开始了。",
        "TIMELINE_D1_WOKE_AT_OLD_HOUSE" => "在旧宅醒来，听见废线方向的异常汽笛。",
        "TIMELINE_D1_FOUR_WAY_REUNION" => "在车站与三名旧友重逢。",
        "TIMELINE_D1_RECORDED_RIVER_ECHO" => "与晓雨录下河岸不自然的回声。",
        "TIMELINE_D1_STUDIED_OLD_MAP" => "与周明研究被修改过的旧地图。",
        "TIMELINE_D1_TESTED_RAILWAY_WATCH" => "与沈砚测试会指向北山的旧怀表。",
        "TIMELINE_D1_PROMISED_RAIL_NOTICE" => "作出【承诺】：一起查看新工程告示。",
        "TIMELINE_D2_READ_RAIL_NOTICE" => "履行承诺，发现施工范围覆盖被抹去的隧道区域。",
        "TIMELINE_D2_WITNESSED_FIRST_ANOMALY" => "第一次直面并平息旧隧道的异常。",
        _ => key
    };

    private static string EvidenceLabel(string evidence) => evidence switch
    {
        "slice.evidence.erased_map_note" => "旧地图上被刮除的笔痕",
        "slice.evidence.map_route_removed" => "新版地图删除的山路",
        "slice.evidence.notice_route_mismatch" => "工程告示与旧线路图的范围矛盾",
        "slice.evidence.notice_office_mismatch" => "告示审批章与现行机构不符",
        "slice.evidence.resonance_frost_pattern" => "异常共鸣留下的白霜纹路",
        "slice.evidence.resonance_directional_echo" => "指向旧隧道下层的共鸣记录",
        _ => evidence.Replace('_', ' ')
    };

    private static string LocationLabel(string locationId) => locationId switch
    {
        "station_square" => "车站广场",
        "station_market" => "商店街",
        "riverbank" => "河岸",
        "school_archive" => "学校旧档案室",
        "residential_lane" => "住宅巷",
        "abandoned_railway" => "废弃支线",
        "old_house" => "旧宅",
        _ => locationId
    };
}
