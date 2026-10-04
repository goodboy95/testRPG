using System.Text.Json.Nodes;
using TownRpg.Core.Domain;
using TownRpg.Core.Services;

namespace TownRpg.Core.Tests;

public class OpeningTrackingTests
{
    private static GameState Awake()
    {
        var g = GameState.CreateDreamPrologue(); g.DreamEscape!.Status = DreamEscapeStatus.CompletedPin;
        g.DreamEscape.Items.Clear(); g.VillageExploration = new() { WakeIntroductionComplete = true };
        g.Player.LocationId = VillageExplorationState.VillageLocation; new VillageOpeningService(g).Initialize(); return g;
    }
    [Fact]
    public void TrackingFollowsStoolCheckpointsWithoutCompletingWorkOnSelection()
    {
        var g = Awake(); var s = new VillageOpeningService(g);
        Assert.False(s.Track(OpeningJob.Stool)); s.Execute("accept_stool");
        Assert.Equal("stool", s.State.Tracking!.Target);
        s.Execute("take_stool"); Assert.Equal("porch", s.State.Tracking.Target);
        s.Execute("put_stool", pose: new(1, 0, 2, 0)); Assert.Equal("stool", s.State.Tracking.Target);
        s.Execute("take_stool"); s.Execute("porch_stool"); Assert.Equal("npc:grandma_xu", s.State.Tracking.Target);
        s.Execute("stool_received"); Assert.Equal("npc:worker_peng", s.State.Tracking.Target);
        s.Execute("report_stool"); Assert.Equal("npc:village_mayor", s.State.Tracking.Target);
        s.Execute("claim"); Assert.Null(s.State.Tracking.Job);
        Assert.False(s.Track(OpeningJob.Stool)); Assert.False(s.Execute("claim"));
    }
    [Fact]
    public void PartialPackingAndManualChoicePreserveAnyDeliveryOrder()
    {
        var s = new VillageOpeningService(Awake()); s.Execute("accept_orders");
        Assert.Equal("table", s.State.Tracking!.Target);
        s.Execute("pack", "house15"); Assert.Equal("receive_house15", s.State.Tracking.Target);
        s.Execute("pack", "house02"); Assert.Equal("receive_house15", s.State.Tracking.Target);
        Assert.True(s.Track(OpeningJob.Orders, "receive_house02"));
        Assert.False(s.Track(OpeningJob.Orders, "receive_house13"));
        Assert.Empty(s.State.Delivered);
        s.Execute("deliver", "house15"); Assert.Equal("receive_house02", s.State.Tracking.Target);
        s.Execute("deliver", "house02"); Assert.Equal("table", s.State.Tracking.Target);
        s.Execute("pack", "house13"); s.Execute("deliver", "house13");
        Assert.Equal("npc:aunt_qiao", s.State.Tracking.Target);
    }
    [Fact]
    public void AcceptDoesNotStealFocusAndCancellationSurvivesSave()
    {
        var g = Awake(); var s = new VillageOpeningService(g);
        s.Execute("accept_orders"); s.Execute("accept_stool"); Assert.Equal(OpeningJob.Orders, s.State.Tracking!.Job);
        s.StopTracking(); s.Execute("pack", "house02");
        var restored = SaveGameSerializer.Deserialize(SaveGameSerializer.Serialize(g));
        Assert.Null(restored.VillageOpening!.Tracking!.Job);
        Assert.False(s.Execute("accept_orders")); Assert.Null(s.State.Tracking.Job);
    }
    [Fact]
    public void NewAcceptanceAfterCancellationTracksAndManualDestinationSurvivesSave()
    {
        var g = Awake(); var s = new VillageOpeningService(g);
        s.Execute("accept_stool"); s.StopTracking(); s.Execute("accept_orders");
        Assert.Equal(OpeningJob.Orders, s.State.Tracking!.Job);
        foreach (var h in VillageOpeningIds.Houses) s.Execute("pack", h);
        Assert.True(s.Track(OpeningJob.Orders, "receive_house13"));
        var restored = SaveGameSerializer.Deserialize(SaveGameSerializer.Serialize(g));
        Assert.Equal("receive_house13", restored.VillageOpening!.Tracking!.Target);
        Assert.Equal(OpeningJob.Orders, restored.VillageOpening.Tracking.Job);
        s.Execute("deliver", "house02"); Assert.Equal("receive_house13", s.State.Tracking.Target);
        Assert.False(s.Execute("deliver", "house02")); Assert.Equal("receive_house13", s.State.Tracking.Target);
    }
    [Fact]
    public void OldSavesMigrateAndStaleKnownTargetsAreReconciled()
    {
        var g = Awake(); var s = new VillageOpeningService(g); s.Execute("accept_stool"); s.Execute("take_stool");
        var json = JsonNode.Parse(SaveGameSerializer.Serialize(g))!; json["game"]!["villageOpening"]!.AsObject().Remove("tracking");
        var old = SaveGameSerializer.Deserialize(json.ToJsonString());
        Assert.Equal("porch", old.VillageOpening!.Tracking!.Target);
        s.State.Tracking!.Target = "table";
        Assert.Equal("porch", SaveGameSerializer.Deserialize(SaveGameSerializer.Serialize(g)).VillageOpening!.Tracking!.Target);
        s.State.Tracking.Target = "unknown";
        Assert.Throws<InvalidDataException>(() => SaveGameSerializer.Deserialize(SaveGameSerializer.Serialize(g)));
    }
    [Fact]
    public void InvitationAndCartStagesIncludeReportingAndPayment()
    {
        var s = new VillageOpeningService(Awake());
        s.Execute("accept_stool"); s.Execute("take_stool"); s.Execute("porch_stool"); s.Execute("stool_received"); s.Execute("report_stool");
        s.Execute("accept_orders"); foreach (var h in VillageOpeningIds.Houses) { s.Execute("pack", h); s.Execute("deliver", h); } s.Execute("report_orders");
        Assert.True(s.Track(OpeningJob.Cart)); Assert.Equal("npc:shen_yan", s.State.Tracking!.Target);
        s.Execute("invite"); Assert.Equal("table", s.State.Tracking.Target);
        s.Execute("cart_checked"); foreach (var h in VillageOpeningIds.Houses) s.Execute("load", h);
        s.Execute("cart_secured"); Assert.Equal(3, s.Objective(OpeningJob.Cart).Targets.Count);
        s.Track(OpeningJob.Cart, "receive_house13"); s.Execute("unload", "house02"); Assert.Equal("receive_house13", s.State.Tracking.Target);
        s.Execute("unload", "house13"); Assert.Equal("receive_house15", s.State.Tracking.Target);
        s.Execute("unload", "house15"); Assert.Equal("cart_home", s.State.Tracking.Target);
        s.Execute("return_cart"); Assert.Equal("npc:aunt_qiao", s.State.Tracking.Target);
        s.Execute("report_cart"); Assert.Equal("npc:village_mayor", s.State.Tracking.Target);
        s.Execute("claim"); Assert.Null(s.State.Tracking.Job);
    }
}
