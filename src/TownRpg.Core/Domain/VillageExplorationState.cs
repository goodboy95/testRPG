namespace TownRpg.Core.Domain;

/// <summary>Reality pose uses metres, separately from legacy logical coordinates.</summary>
public sealed class VillageExplorationState
{
    public const string HomeLocation = "village.home";
    public const string VillageLocation = "village.exterior";
    public float X { get; set; } = -3.1f;
    public float Y { get; set; }
    public float Z { get; set; } = 2.8f;
    public float FacingRadians { get; set; } = MathF.PI / 2;
    public float CameraYaw { get; set; } = -90;
    public bool WakeIntroductionComplete { get; set; }
    public Dictionary<string, bool> Doors { get; set; } = new(StringComparer.Ordinal);
    // Optional for older saves: -1/+1 names the opening side of the door's local plane.
    public Dictionary<string, int> DoorOpenSides { get; set; } = new(StringComparer.Ordinal);
    public Dictionary<string, bool> Lights { get; set; } = new(StringComparer.Ordinal);
    public static readonly string[] DoorIds = ["entry", "bedroom", "family", "kitchen", "bathroom"];
    public static readonly string[] LightIds = ["bedroom", "family", "living", "kitchen", "bathroom", "porch"];
}
