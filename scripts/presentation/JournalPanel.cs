using Godot;
using TownRpg.Application;
using TownRpg.Core.Domain;
using TownRpg.Core.Services;

namespace TownRpg.Presentation;

public partial class JournalPanel : Control
{
    public event Action? Closed;

    private RichTextLabel _body = null!;
    private Button _close = null!;

    public override void _Ready()
    {
        SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect);
        var shade = new ColorRect { Color = new Color(0, 0, 0, 0.62f) };
        shade.SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect);
        AddChild(shade);

        var frame = new PanelContainer();
        frame.SetAnchorsPreset(LayoutPreset.Center);
        frame.Position = new Vector2(-250, -155);
        frame.Size = new Vector2(500, 310);
        frame.AddThemeStyleboxOverride("panel", UiStyles.Panel(new Color("222d3ef2"), 10));
        AddChild(frame);

        var layout = new VBoxContainer();
        frame.AddChild(layout);
        var titleRow = new HBoxContainer();
        layout.AddChild(titleRow);
        var title = new Label { Text = "事实手册" };
        title.AddThemeFontSizeOverride("font_size", 20);
        title.AddThemeColorOverride("font_color", new Color("f2c66d"));
        titleRow.AddChild(title);
        titleRow.AddSpacer(false);
        _close = UiStyles.Button("关闭 [J]");
        _close.Pressed += () => Closed?.Invoke();
        titleRow.AddChild(_close);

        _body = new RichTextLabel
        {
            BbcodeEnabled = true,
            CustomMinimumSize = new Vector2(470, 250),
            ScrollActive = true
        };
        layout.AddChild(_body);
        Visible = false;
    }

    public void Open(GameSession session, string dialogueHistory)
    {
        var state = session.State;
        var lines = new List<string>
        {
            $"[font_size=18][color=#b9d1c5]第 {state.World.Day} 日　{FormatTime(state.World.MinuteOfDay)}[/color][/font_size]",
            $"[b]关注风险：[/b]{ExposureLabel(state.World.Exposure)}",
            "",
            "[font_size=17][b]人物关系[/b][/font_size]"
        };

        foreach (var npcId in new[]
                 {
                     CharacterIds.AbilityHolder,
                     CharacterIds.KnowledgeFriend,
                     CharacterIds.TokenHolder,
                     CharacterIds.Shopkeeper
                 })
        {
            var relationship = state.Relationships.FirstOrDefault(item =>
                item.SourceId == npcId && item.TargetId == CharacterIds.Protagonist);
            lines.Add(relationship is null
                ? $"• {WorldSceneController.CharacterName(npcId)}　好感：陌生　信赖：戒备"
                : $"• {WorldSceneController.CharacterName(npcId)}　好感：{relationship.AffinityLabel}　信赖：{relationship.TrustLabel}");
        }

        lines.AddRange(["", "[font_size=17][b]已知作息[/b][/font_size]"]);
        var scheduleService = new ScheduleService(session.Content.Schedules.Entries);
        foreach (var npcId in state.Investigation.KnownScheduleEntries.OrderBy(id => id, StringComparer.Ordinal))
        {
            var current = scheduleService.Current(npcId, state.World);
            var next = current ?? scheduleService.NextVisible(npcId, state.World);
            lines.Add(next is null
                ? $"• {WorldSceneController.CharacterName(npcId)}：目前没有已知的下一时段。"
                : $"• {WorldSceneController.CharacterName(npcId)}：第 {next.Day ?? state.World.Day} 日 {FormatTime(next.StartMinute)}–{FormatTime(next.EndMinute)}，{LocationLabel(next.LocationId)}。");
        }
        if (state.Investigation.KnownScheduleEntries.Count == 0)
        {
            lines.Add("• 第一次交谈后，会逐步记下对方的常见去处。");
        }

        lines.AddRange(["", "[font_size=17][b]承诺[/b][/font_size]"]);
        if (state.Promises.Count == 0)
        {
            lines.Add("• 尚未作出重要承诺。");
        }
        else
        {
            lines.AddRange(state.Promises.Select(promise =>
                $"• {promise.Description}　[{PromiseLabel(promise.Status)}]"));
        }

        lines.AddRange(["", "[font_size=17][b]已确认事实与调查方向[/b][/font_size]"]);
        lines.AddRange(state.World.Facts.Select(fact => $"• {FactLabel(fact)}"));
        lines.AddRange(state.Investigation.Leads.Select(lead => $"→ {LeadLabel(lead)}"));
        lines.AddRange(state.World.Evidence.Select(evidence => $"◆ 证据：{EvidenceLabel(evidence)}"));
        if (state.World.Facts.Count == 0 && state.Investigation.Leads.Count == 0)
        {
            lines.Add("• 先在镇上走走，与熟人聊聊。");
        }

        lines.AddRange(["", "[font_size=17][b]时间线[/b][/font_size]"]);
        foreach (var entry in state.Investigation.Timeline)
        {
            lines.Add($"第 {entry.Day} 日　{TimelineLabel(entry.TextKey)}");
        }

        if (!string.IsNullOrWhiteSpace(dialogueHistory))
        {
            lines.AddRange(["", "[font_size=17][b]近期对话[/b][/font_size]", dialogueHistory]);
        }

        _body.Text = string.Join("\n", lines);
        Visible = true;
    }

    public void ClosePanel() => Visible = false;

    public void SetGamepadPrompts(bool useGamepad) =>
        _close.Text = useGamepad ? "关闭 [Y]" : "关闭 [J]";

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
