namespace TownRpg.Core.Domain;

public sealed class RelationshipState
{
    public required string SourceId { get; init; }
    public required string TargetId { get; init; }
    public int Affinity { get; set; }
    public int Trust { get; set; }
    public RomanceStatus Romance { get; set; } = RomanceStatus.Friendship;
    public HashSet<string> Memories { get; set; } = new(StringComparer.Ordinal);
    public HashSet<string> GiftedAffinityStages { get; set; } = new(StringComparer.Ordinal);

    public string AffinityLabel => StageLabel(Affinity, "陌生", "熟悉", "亲近", "深厚");
    public string TrustLabel => StageLabel(Trust, "戒备", "接纳", "认可", "信赖");

    public void ChangeAffinity(int delta) => Affinity = Math.Clamp(Affinity + delta, 0, 100);

    public void ChangeTrust(int delta) => Trust = Math.Clamp(Trust + delta, 0, 100);

    public static int StageIndex(int value) => Math.Clamp(value, 0, 100) switch
    {
        < 25 => 0,
        < 50 => 1,
        < 75 => 2,
        _ => 3
    };

    private static string StageLabel(int value, string first, string second, string third, string fourth) =>
        StageIndex(value) switch
        {
            0 => first,
            1 => second,
            2 => third,
            _ => fourth
        };
}
