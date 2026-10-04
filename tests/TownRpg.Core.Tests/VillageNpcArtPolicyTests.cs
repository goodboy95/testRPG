using TownRpg.Presentation;

namespace TownRpg.Core.Tests;

public sealed class VillageNpcArtPolicyTests
{
    [Fact]
    public void ArtProfileUsesThreeVisibleLodsAndAHiddenDistance()
    {
        Assert.Equal(0, VillageNpcArtPolicy.DesiredLod(2, 0, false, VillageNpcArtPolicy.Art));
        Assert.Equal(1, VillageNpcArtPolicy.DesiredLod(16, 0, false, VillageNpcArtPolicy.Art));
        Assert.Equal(2, VillageNpcArtPolicy.DesiredLod(35, 1, false, VillageNpcArtPolicy.Art));
        Assert.Equal(3, VillageNpcArtPolicy.DesiredLod(56, 2, false, VillageNpcArtPolicy.Art));
        Assert.Equal(0, VillageNpcArtPolicy.DesiredLod(80, 3, true, VillageNpcArtPolicy.Art));
        Assert.Equal(32_000, VillageNpcArtPolicy.TriangleLimit(VillageNpcArtPolicy.Art, 0));
        Assert.Equal(2_500, VillageNpcArtPolicy.TriangleLimit(VillageNpcArtPolicy.Art, 2));
    }

    [Fact]
    public void TwelveResidentAllocationCapsFourHighAndFourMedium()
    {
        var caps = VillageNpcArtPolicy.AllocateCaps(Enumerable.Range(0, 12).Select(i => (float)i).ToArray(),
            Enumerable.Repeat(false, 12).ToArray());
        Assert.Equal(4, caps.Count(cap => cap == 0));
        Assert.Equal(4, caps.Count(cap => cap == 1));
        Assert.Equal(4, caps.Count(cap => cap == 2));
    }

    [Fact]
    public void TalkingResidentWinsTheHighDetailSlot()
    {
        var caps = VillageNpcArtPolicy.AllocateCaps(new[] { 2f, 9f, 1f }, new[] { false, true, false });
        Assert.Equal(0, caps[1]);
    }
}
