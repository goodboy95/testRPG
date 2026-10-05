using Godot;
using TownRpg.Presentation;

namespace TownRpg.Scripts.Testing;

public partial class RefinementVillage : VillageAreaHost
{
    public bool Baseline { get; init; }
    private HomePreviewController3D? _finishedHome;
    protected override VillageVisualProfile? IndependentVisualProfile => Baseline ? null :
        new VillageVisualProfile("refinement-v1", "", "res://assets/generated/village/terrain-v4/",
            RefinementArt.Folder+"home.glb",true) { ExteriorModel=RefinementArt.Folder+"village.glb" };
    public override void _Ready()
    {
        base._Ready();
        if(!Baseline)RefinementArt.Village(Exterior);
    }
    public override void _Process(double delta)
    {
        if(!Baseline && Interior is not null && Interior.IsNodeReady() && _finishedHome!=Interior)
        { RefinementArt.Home(Interior); _finishedHome=Interior; }
    }
}
