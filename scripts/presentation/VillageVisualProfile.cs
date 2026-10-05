namespace TownRpg.Presentation;

/// <summary>Immutable presentation assets; never serialized into player state.</summary>
public sealed record VillageVisualProfile(string Id, string StreamManifest, string TerrainFolder,
    string HomeModel, bool AnimeLighting)
{
    /// <summary>Optional saved-source model for an independent art review; no formal selector exposes it.</summary>
    public string? ExteriorModel { get; init; }
    public static readonly VillageVisualProfile V7 = new("v7",
        "res://assets/generated/village/stream-v7/manifest.json",
        "res://assets/generated/village/terrain-v4/",
        "res://assets/generated/home/3d/home-identity-v2.glb", false);
    public static readonly VillageVisualProfile AnimeV8 = new("v8-sample",
        "res://assets/generated/village/stream-v8/manifest.json",
        "res://assets/generated/village/terrain-v8/",
        "res://assets/generated/home/anime-v8/home-anime-v8.glb", true);

    // v8 remains a preview even when a launch argument is accidentally given to the main game.
    public static VillageVisualProfile Select(bool embedded, bool animePreview, IEnumerable<string> arguments) =>
        !embedded && (animePreview || arguments.Contains("--village-v8")) ? AnimeV8 : V7;
}
