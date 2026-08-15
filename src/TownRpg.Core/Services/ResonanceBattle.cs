using TownRpg.Core.Domain;

namespace TownRpg.Core.Services;

public sealed class ResonanceBattle
{
    public int Resonance { get; private set; } = 6;
    public int Resolve { get; private set; } = 6;
    public int Insight { get; private set; }
    public int Turn { get; private set; } = 1;
    public bool IsComplete => Resonance <= 0;
    public bool IsDefeated => Resolve <= 0;
    public bool HasRetreated { get; private set; }

    public BattleTurnResult Act(BattleAction action, bool hasAbilityHolderSupport, bool storyMode = false)
    {
        if (IsComplete || IsDefeated || HasRetreated)
        {
            throw new InvalidOperationException("The battle is already over.");
        }

        var message = action switch
        {
            BattleAction.Observe => Observe(),
            BattleAction.Guard => "你护住同伴，稳住呼吸。",
            BattleAction.Soothe => Soothe(hasAbilityHolderSupport),
            BattleAction.Disrupt => Disrupt(),
            BattleAction.Retreat => Retreat(),
            _ => throw new ArgumentOutOfRangeException(nameof(action))
        };

        if (!HasRetreated && !IsComplete)
        {
            var incoming = action == BattleAction.Guard || storyMode ? 0 : 1;
            Resolve = Math.Max(0, Resolve - incoming);
            if (incoming > 0)
            {
                message += " 异响穿过胸口，你失去了一点意志。";
            }
        }

        var result = new BattleTurnResult
        {
            Message = message,
            Turn = Turn,
            Resonance = Resonance,
            Resolve = Resolve,
            Won = IsComplete,
            Defeated = IsDefeated,
            Retreated = HasRetreated
        };
        Turn++;
        return result;
    }

    private string Observe()
    {
        Insight = Math.Min(2, Insight + 1);
        return Insight == 1
            ? "你分辨出异响中重复的节拍。"
            : "你找到了共鸣最脆弱的间隙。";
    }

    private string Soothe(bool hasAbilityHolderSupport)
    {
        if (!hasAbilityHolderSupport)
        {
            return "没有人能回应那道频率，安抚没有奏效。";
        }

        var reduction = Insight > 0 ? 3 : 1;
        Resonance = Math.Max(0, Resonance - reduction);
        return reduction == 3
            ? "晓雨循着你找出的节拍轻声哼唱，异响迅速退去。"
            : "晓雨勉强压低了异响，但她还找不到正确的节拍。";
    }

    private string Disrupt()
    {
        var reduction = Insight >= 2 ? 2 : 1;
        Resonance = Math.Max(0, Resonance - reduction);
        return reduction == 2
            ? "你在节拍断点敲响铁轨，打散了一层共鸣。"
            : "你敲击生锈的铁轨，异响短暂停顿。";
    }

    private string Retreat()
    {
        HasRetreated = true;
        return "你记下位置，决定先回镇上准备。";
    }
}

public sealed class BattleTurnResult
{
    public required string Message { get; init; }
    public int Turn { get; init; }
    public int Resonance { get; init; }
    public int Resolve { get; init; }
    public bool Won { get; init; }
    public bool Defeated { get; init; }
    public bool Retreated { get; init; }
}
