namespace TownRpg.Presentation;

/// <summary>Named terrain review entry; shares the adopted surface with the default preview and game.</summary>
public partial class VillageTerrainPreviewController : VillagePreviewController
{
    protected override bool TerrainSample=>true;
}
