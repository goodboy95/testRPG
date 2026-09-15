namespace TownRpg.Presentation;

/// <summary>Independent art review; no formal session persistence or default-route change.</summary>
public partial class VillageArtPreviewController : VillagePreviewController
{
    protected override bool ArtSample=>true;
}
