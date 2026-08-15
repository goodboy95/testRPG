using TownRpg.Core.Domain;
using TownRpg.Core.Services;

namespace TownRpg.Core.Tests;

public sealed class ResonanceBattleTests
{
    [Fact]
    public void StoryMode_PreventsResolveLoss()
    {
        var battle = new ResonanceBattle();

        var result = battle.Act(BattleAction.Observe, hasAbilityHolderSupport: false, storyMode: true);

        Assert.Equal(6, result.Resolve);
        Assert.False(result.Defeated);
    }

    [Fact]
    public void DisruptOnly_IsAZeroSupportWinningPath()
    {
        var battle = new ResonanceBattle();
        BattleTurnResult? result = null;

        for (var turn = 0; turn < 6; turn++)
        {
            result = battle.Act(BattleAction.Disrupt, hasAbilityHolderSupport: false);
        }

        Assert.NotNull(result);
        Assert.True(result.Won);
        Assert.False(result.Defeated);
        Assert.Equal(0, result.Resonance);
        Assert.Equal(1, result.Resolve);
        Assert.True(battle.IsComplete);
    }

    [Fact]
    public void ObserveThenSupportedSoothe_ProvidesFasterWinningPath()
    {
        var battle = new ResonanceBattle();

        battle.Act(BattleAction.Observe, hasAbilityHolderSupport: true);
        battle.Act(BattleAction.Observe, hasAbilityHolderSupport: true);
        battle.Act(BattleAction.Soothe, hasAbilityHolderSupport: true);
        var result = battle.Act(BattleAction.Soothe, hasAbilityHolderSupport: true);

        Assert.True(result.Won);
        Assert.False(result.Defeated);
        Assert.Equal(0, result.Resonance);
    }

    [Fact]
    public void Retreat_EndsBattleWithoutVictoryOrDefeat()
    {
        var battle = new ResonanceBattle();

        var result = battle.Act(BattleAction.Retreat, hasAbilityHolderSupport: false);

        Assert.True(result.Retreated);
        Assert.False(result.Won);
        Assert.False(result.Defeated);
        Assert.True(battle.HasRetreated);
        Assert.Throws<InvalidOperationException>(() =>
            battle.Act(BattleAction.Disrupt, hasAbilityHolderSupport: false));
    }
}
