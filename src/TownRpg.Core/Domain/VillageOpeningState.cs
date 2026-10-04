namespace TownRpg.Core.Domain;

public enum OpeningJob { Stool, Orders, Cart }
public enum OpeningProgress { Available, Accepted, LaborDone, Reported, Paid }
public enum StoolLocation { Workshop, Carried, Ground, Porch, Accepted }

/// <summary>Confirmed semantic checkpoints; never animation frames or Godot nodes.</summary>
[System.Text.Json.Serialization.JsonUnmappedMemberHandling(System.Text.Json.Serialization.JsonUnmappedMemberHandling.Disallow)]
public sealed class VillageOpeningState
{
    public int Version { get; set; } = 1;
    public Dictionary<OpeningJob, OpeningProgress> Jobs { get; set; } = Enum.GetValues<OpeningJob>().ToDictionary(x => x, _ => OpeningProgress.Available);
    public StoolLocation Stool { get; set; }
    public OpeningPose? StoolGround { get; set; }
    public HashSet<string> Packed { get; set; } = [];
    public HashSet<string> Delivered { get; set; } = [];
    public bool Invited { get; set; }
    public bool CartChecked { get; set; }
    public HashSet<string> Loaded { get; set; } = [];
    public HashSet<string> Unloaded { get; set; } = [];
    public bool LoadSecured { get; set; }
    public bool CartReturned { get; set; }
    public OpeningPose? CartParking { get; set; }
    public HashSet<string> Conversations { get; set; } = [];
    public int CompanionDue { get; set; }
    public bool CompanionPaid { get; set; }
    public bool BreakfastEaten { get; set; }
    public bool Washed { get; set; }
    public OpeningTracking? Tracking { get; set; }
}

/// <summary>Null on old saves; a present object with no Job means deliberately untracked.</summary>
[System.Text.Json.Serialization.JsonUnmappedMemberHandling(System.Text.Json.Serialization.JsonUnmappedMemberHandling.Disallow)]
public sealed class OpeningTracking
{
    public OpeningJob? Job { get; set; }
    public string? Target { get; set; }
}

public sealed record OpeningPose(float X, float Y, float Z, float Yaw);

public static class VillageOpeningIds
{
    public static readonly string[] Houses = ["house02", "house13", "house15"];
    public static readonly string[] Residents = ["aunt_qiao", "worker_peng", "village_mayor", "shen_yan", "grandma_xu", "uncle_chen", "uncle_liu"];
    public static readonly IReadOnlyDictionary<OpeningJob, int> Rewards = new Dictionary<OpeningJob, int>
    { [OpeningJob.Stool] = 20, [OpeningJob.Orders] = 30, [OpeningJob.Cart] = 40 };
    public const int CompanionReward = 40;
    public static string Event(string operation) => "village.opening." + operation;
    public static string StoryKey(string operation, string target) => operation is "deliver" or "unload" ? operation + "_" + target : operation;
    public static readonly string[] StoryOperations = ["posting", "accept_stool", "accept_orders", "stool_received", "report_stool", "report_orders", "invite", "cart_checked", "cart_secured", "report_cart", "rest"];
}
