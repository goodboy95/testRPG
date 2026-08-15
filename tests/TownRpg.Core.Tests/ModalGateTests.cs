using TownRpg.Application;

namespace TownRpg.Core.Tests;

public sealed class ModalGateTests
{
    [Fact]
    public void NestedLeasesWithSameOwner_KeepGateBlockedUntilBothClose()
    {
        var gate = new ModalGate();
        using var first = gate.Acquire("dialogue");
        var second = gate.Acquire("dialogue");

        first.Dispose();
        Assert.True(gate.IsBlocked);

        second.Dispose();
        Assert.False(gate.IsBlocked);
    }

    [Fact]
    public void LeaseDisposal_IsIdempotent()
    {
        var gate = new ModalGate();
        var lease = gate.Acquire("journal");

        lease.Dispose();
        lease.Dispose();

        Assert.False(gate.IsBlocked);
    }
}
