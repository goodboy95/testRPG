using TownRpg.Core.Domain;
using TownRpg.Core.Services;

namespace TownRpg.Core.Tests;

public class VillageOpeningTests
{
    private static GameState Awake(DreamEscapeStatus ending = DreamEscapeStatus.CompletedEscape)
    {
        var g = GameState.CreateDreamPrologue(); g.DreamEscape!.Status = ending;
        g.DreamEscape.Items.Clear(); g.VillageExploration = new() { WakeIntroductionComplete = true };
        g.Player.LocationId = VillageExplorationState.VillageLocation;
        new VillageOpeningService(g).Initialize(); return g;
    }
    private static void Stool(VillageOpeningService s)
    { Assert.True(s.Execute("accept_stool")); Assert.True(s.Execute("take_stool")); Assert.True(s.Execute("porch_stool")); Assert.True(s.Execute("stool_received")); Assert.True(s.Execute("report_stool")); }
    private static void Orders(VillageOpeningService s, string[] order)
    { Assert.True(s.Execute("accept_orders")); foreach (var h in order) Assert.True(s.Execute("pack", h)); foreach (var h in order) Assert.True(s.Execute("deliver", h)); Assert.True(s.Execute("report_orders")); }
    public static IEnumerable<object[]> Routes()
    {
        var h = VillageOpeningIds.Houses;
        var routes = (from a in h from b in h from c in h where a != b && b != c && a != c select new[] { a, b, c }).ToArray();
        foreach (var first in new[] { true, false }) foreach (var a in routes) foreach (var b in routes) yield return [first, a, b];
    }
    [Theory, MemberData(nameof(Routes))]
    public void BothJobsAndEveryDeliveryOrderFinishWithoutCollectingMoney(bool stoolFirst, string[] small, string[] bulk)
    {
        var g = Awake(); var s = new VillageOpeningService(g); var initial = g.Inventory.Money;
        if (stoolFirst) { Stool(s); Orders(s, small); } else { Orders(s, small); Stool(s); }
        Assert.True(s.CanInvite); Assert.Equal(initial, g.Inventory.Money);
        Assert.True(s.Execute("invite")); Assert.False(s.Execute("invite"));
        s.Execute("cart_checked"); foreach (var h in bulk) Assert.True(s.Execute("load", h)); s.Execute("cart_secured");
        foreach (var h in bulk) { Assert.True(s.Execute("unload", h)); Assert.False(s.Execute("unload", h)); }
        Assert.False(s.Execute("report_cart")); s.Execute("return_cart"); s.Execute("report_cart");
        Assert.True(s.Finished); Assert.Equal(90, s.Payable); Assert.Equal(40, s.State.CompanionDue);
        s.Execute("rest"); Assert.True(s.Execute("claim")); Assert.False(s.Execute("claim"));
        Assert.Equal(initial + 90, g.Inventory.Money); Assert.True(s.State.CompanionPaid);
        Assert.False(s.Execute("report_stool")); Assert.False(s.Execute("report_orders")); Assert.False(s.Execute("report_cart"));
        Assert.Equal(OpeningProgress.Paid, SaveGameSerializer.Deserialize(SaveGameSerializer.Serialize(g)).VillageOpening!.Jobs[OpeningJob.Cart]);
    }
    [Theory]
    [InlineData(DreamEscapeStatus.CompletedEscape)] [InlineData(DreamEscapeStatus.CompletedPin)]
    public void OldAwakeSavePreservesPoseMoneyDoorsAndEnding(DreamEscapeStatus ending)
    {
        var g = Awake(ending); g.VillageOpening = null; g.Inventory.Money = 729;
        g.VillageExploration!.X = 13; g.VillageExploration.Doors["entry"] = true;
        var old = SaveGameSerializer.Deserialize(SaveGameSerializer.Serialize(g)); var s = new VillageOpeningService(old);
        Assert.True(s.Initialize()); Assert.False(s.Initialize()); Assert.Equal(729, old.Inventory.Money);
        Assert.Equal(13, old.VillageExploration!.X); Assert.True(old.VillageExploration.Doors["entry"]); Assert.True(old.VillageExploration.WakeIntroductionComplete);
        Assert.Equal(ending, old.DreamEscape!.Status);
    }
    [Fact]
    public void CheckpointsSurviveInterleavingAndSeventeenInterruptions()
    {
        var g = Awake(); var s = new VillageOpeningService(g);
        void Roundtrip() { g = SaveGameSerializer.Deserialize(SaveGameSerializer.Serialize(g)); s = new(g); }
        s.Execute("posting"); Roundtrip(); // 1: board cancellation after normal posting
        s.Execute("accept_stool"); Roundtrip(); // 2: accepted, not picked
        s.Execute("take_stool"); Roundtrip(); // 3: carry reload restores to safe ground
        s.RecoverCarriedStool(new(0, 1, 0, 0)); Roundtrip(); // 4: temporary placement
        s.Execute("accept_orders"); s.Execute("pack", "house13"); Roundtrip(); // 5: partial preparation
        Assert.False(s.Execute("deliver", "house15")); Roundtrip(); // 6: wrong/unpacked package
        s.Execute("deliver", "house13"); Roundtrip(); // 7: delivered, interrupted optional conversation
        s.Execute("take_stool"); s.Execute("porch_stool"); Roundtrip(); // 8: not yet tried
        s.Execute("stool_received"); Roundtrip(); // 9: delivered, not reported
        s.Execute("report_stool"); Roundtrip(); // 10: first reported
        foreach (var h in new[] { "house02", "house15" }) { s.Execute("pack", h); s.Execute("deliver", h); }
        s.Execute("report_orders"); Roundtrip(); // 11: declined/deferred invitation makes no mutation
        Assert.True(s.CanInvite); Assert.False(s.State.Invited);
        s.Execute("invite"); Roundtrip(); // 12: companion across homes
        s.Execute("cart_checked"); s.Execute("load", "house02"); Roundtrip(); // 13: partially loaded
        foreach (var h in new[] { "house13", "house15" }) s.Execute("load", h);
        s.Execute("cart_secured"); s.ParkCart(new(1, 2, 3, 1)); Roundtrip(); // 14: cart parked
        s.Execute("unload", "house15"); Roundtrip(); // 15: partial unloading
        s.Execute("unload", "house13"); s.Execute("unload", "house02"); s.Execute("return_cart"); Roundtrip(); // 16: empty returned
        s.Execute("report_cart"); Roundtrip(); // 17: reported, unpaid
        Assert.True(s.Finished); Assert.Equal(90, s.Payable); Assert.Single(s.State.Packed, x => x == "house13");
    }
    [Fact]
    public void InvalidOperationsAndFailedAtomicMutationLeaveCheckpointUntouched()
    {
        var g = Awake(); var s = new VillageOpeningService(g);
        Assert.False(s.Execute("invite")); Assert.False(s.Execute("unload", "house02")); Assert.False(s.Execute("unknown"));
        s.Execute("accept_stool"); s.Execute("take_stool"); var snapshot = SaveGameSerializer.Serialize(g);
        Assert.Throws<InvalidDataException>(() => s.Execute("put_stool", pose: new(float.NaN, 0, 0, 0)));
        Assert.Equal(snapshot, SaveGameSerializer.Serialize(g));
        s.Execute("porch_stool"); s.Execute("stool_received"); s.Execute("report_stool"); g.Inventory.Money = int.MaxValue;
        snapshot = SaveGameSerializer.Serialize(g); Assert.Throws<OverflowException>(() => s.Execute("claim")); Assert.Equal(snapshot, SaveGameSerializer.Serialize(g));
        g.VillageOpening!.Unloaded.Add("house02"); Assert.Throws<InvalidDataException>(() => SaveGameSerializer.Deserialize(SaveGameSerializer.Serialize(g)));
    }
    [Fact]
    public void ExplorationCaptureDoesNotOverwriteOpeningAndLegacyRemainsLegacy()
    {
        var g = Awake(); var s = new VillageOpeningService(g); s.Execute("accept_orders"); s.Execute("pack", "house02");
        new VillageExplorationService(g).Capture(VillageExplorationState.HomeLocation, new()); Assert.Contains("house02", s.State.Packed);
        var legacy = SaveGameSerializer.Deserialize(SaveGameSerializer.Serialize(GameState.CreateVerticalSlice()));
        Assert.Null(legacy.VillageOpening); Assert.Throws<InvalidOperationException>(() => new VillageOpeningService(legacy).Initialize());
    }
}
