namespace TownRpg.Presentation;

/// <summary>Shared, engine-free limits for authored modular resident appearances.</summary>
public sealed record VillageNpcVisualProfile(
    string Id,
    int VisibleLodCount,
    int Lod0Triangles,
    int Lod1Triangles,
    int Lod2Triangles,
    int MaxSurfaces,
    float Lod0Distance,
    float Lod1Distance,
    float HideDistance,
    bool UsesImportedAnimations);

public static class VillageNpcArtPolicy
{
    public static readonly VillageNpcVisualProfile Pilot = new("pilot", 2, 8_000, 2_000, 0, 2, 25, 55, 55, false);
    public static readonly VillageNpcVisualProfile Art = new("art", 3, 32_000, 10_000, 2_500, 3, 12, 28, 55, true);

    public static VillageNpcVisualProfile Profile(string? id) => id == "art" ? Art : Pilot;

    public static int DesiredLod(float distance, int previous, bool talking, VillageNpcVisualProfile profile)
    {
        if (talking) return 0;
        if (distance > profile.HideDistance || previous == profile.VisibleLodCount && distance >= profile.HideDistance - 5)
            return profile.VisibleLodCount;
        if (profile.VisibleLodCount == 2)
            return distance > profile.Lod0Distance ? 1 : 0;
        if (distance > profile.Lod1Distance || previous == 2 && distance >= profile.Lod1Distance - 3) return 2;
        return distance > profile.Lod0Distance || previous == 1 && distance >= profile.Lod0Distance - 2 ? 1 : 0;
    }

    /// <summary>Assigns a detail floor by proximity. The current speaker remains eligible for LOD0.</summary>
    public static int[] AllocateCaps(IReadOnlyList<float> distances, IReadOnlyList<bool> talking)
    {
        if (distances.Count != talking.Count) throw new ArgumentException("Resident priority arrays differ.");
        var caps = Enumerable.Repeat(2, distances.Count).ToArray();
        var ordered = Enumerable.Range(0, distances.Count)
            .OrderBy(i => talking[i] ? 0 : 1)
            .ThenBy(i => distances[i])
            .ToArray();
        for (var rank = 0; rank < ordered.Length; rank++) caps[ordered[rank]] = rank < 4 ? 0 : rank < 8 ? 1 : 2;
        return caps;
    }

    public static int TriangleLimit(VillageNpcVisualProfile profile, int lod) => lod switch
    {
        0 => profile.Lod0Triangles,
        1 => profile.Lod1Triangles,
        2 => profile.Lod2Triangles,
        _ => 0
    };
}
