using System.Text.Json;
using TownRpg.Presentation;
using Xunit;

namespace TownRpg.Core.Tests;

public class VillageRenderPolicyTests
{
    private static VillageRenderUnit Unit(string id,string district="home",bool important=false)=>new()
    {
        Id=id,District=district,Category="building",Important=important,Bounds=[0,0,0,10,4,8],
        Lods=Enumerable.Range(0,4).Select(i=>new VillageRenderLod
        {
            Node=id+"_lod"+i,Bundle=district+(i<2?"_detail":"_proxy"),
            Triangles=new[]{30000,12000,3000,600}[i],Surfaces=i<2?8:1,Materials=i<2?["plaster","roof"]:["proxy"]
        }).ToArray()
    };
    [Fact] public void ResidentProxyBatchCostIsSeparateFromDetailedSurfaceDraws()
    {
        var items=new[]{new VillageRenderCandidate(Unit("a"),50,0,true,-1),new VillageRenderCandidate(Unit("b"),50,0,true,-1),new VillageRenderCandidate(Unit("c","west"),50,0,true,-1)};
        Assert.Equal(new VillageRenderCost(9000,1,1,1),VillageRenderPolicy.Cost(items,[2,2,2]));
        Assert.Equal(new VillageRenderCost(42000,2,16,2),VillageRenderPolicy.Cost(items[..2],[0,1]));
    }
    [Fact] public void BudgetKeepsNearbyLandmarkBeforeDistantOrdinaryBuildings()
    {
        var items=new[]{new VillageRenderCandidate(Unit("home",important:true),1,.6f,true,-1),new VillageRenderCandidate(Unit("ordinary"),20,.4f,true,-1)};
        var result=VillageRenderPolicy.Select(items,new(40000,120,9));
        Assert.True(result.Fits);Assert.Equal(0,result.Lods[0]);Assert.True(result.Lods[1]>=2);
    }
    [Fact] public void MissingDetailsAlwaysUsesAResidentProxy()
    {
        var result=VillageRenderPolicy.Select([new(Unit("home"),1,.8f,false,-1)],VillageRenderBudget.Near);
        Assert.Equal(2,result.Lods[0]);Assert.True(result.Fits);
    }
    [Fact] public void MinimumBudgetFailureIsReportedWithoutMakingBuildingsDisappear()
    {
        var result=VillageRenderPolicy.Select([new(Unit("home"),1,.8f,true,-1)],new(1,1,1));
        Assert.False(result.Fits);Assert.Equal(3,result.Lods[0]);Assert.Equal(600,result.Cost.Triangles);
    }
    [Fact] public void LodDistanceHysteresisPreventsBoundaryOscillation()
    {
        Assert.Equal(0,VillageRenderPolicy.DesiredLod(16.5f,.1f,0));
        Assert.Equal(1,VillageRenderPolicy.DesiredLod(15.5f,.1f,1));
        Assert.Equal(1,VillageRenderPolicy.DesiredLod(20,.1f,0));
        Assert.Equal(0,VillageRenderPolicy.DesiredLod(12,.1f,1));
    }
    [Fact] public void LaptopProfileRetainsMidrangeMaterialsWithoutBypassingMissingBundles()
    {
        var profile=VillageRenderProfile.FromArguments([]);
        var items=new[]{new VillageRenderCandidate(Unit("a"),45,.01f,true,-1),new VillageRenderCandidate(Unit("b"),45,.01f,false,-1)};
        var current=VillageRenderPolicy.Select(items,profile.SceneryNear,profile:profile);
        var legacy=VillageRenderPolicy.Select(items,VillageRenderProfile.Integrated.SceneryNear);
        Assert.True(current.Fits);Assert.Equal(1,current.Lods[0]);Assert.Equal(2,legacy.Lods[0]);Assert.Equal(2,current.Lods[1]);
        Assert.Equal(VillageRenderProfile.Integrated,VillageRenderProfile.FromArguments(["--village-budget-integrated"]));
        Assert.Equal(1,VillageRenderPolicy.DesiredLod(51,.01f,1,profile));
        Assert.Equal(2,VillageRenderPolicy.DesiredLod(49,.01f,2,profile));
    }
    [Fact] public void LaptopBudgetKeepsNearTreeCrownBeforeFarOrdinaryFacade()
    {
        var items=new[]{new VillageRenderCandidate(Unit("tree") with {Category="tree"},10,.4f,true,-1),new VillageRenderCandidate(Unit("building"),30,.4f,true,-1)};
        var result=VillageRenderPolicy.Select(items,new(40000,100,9),profile:VillageRenderProfile.Laptop1060);
        Assert.True(result.Fits);Assert.Equal(0,result.Lods[0]);Assert.True(result.Lods[1]>=2);
    }
    [Fact] public void RealManifestEveryOrientationFitsConservativeSceneryBudget()
    {
        var folder=new DirectoryInfo(AppContext.BaseDirectory);
        while(folder is not null && !File.Exists(Path.Combine(folder.FullName,"project.godot")))folder=folder.Parent;
        Assert.NotNull(folder);
        var path=Path.Combine(folder!.FullName,"assets/generated/village/stream-v7/manifest.json");
        var manifest=VillageRenderManifest.Parse(File.ReadAllText(path));
        foreach(var x in Enumerable.Range(0,23).Select(i=>-110f+i*10))
        foreach(var z in Enumerable.Range(0,25).Select(i=>-115f+i*10))
        {
            var items=manifest.Units.Select(u=>
            {
                var dx=Math.Max(0,Math.Max(u.Bounds[0]-x,x-u.Bounds[0]-u.Bounds[3]));
                var dz=Math.Max(0,Math.Max(u.Bounds[2]-z,z-u.Bounds[2]-u.Bounds[5]));
                var d=MathF.Sqrt(dx*dx+dz*dz);
                return new VillageRenderCandidate(u,d,Math.Max(u.Bounds[3],u.Bounds[4])/Math.Max(1,d),true,-1);
            }).ToArray();
            var selection=VillageRenderPolicy.Select(items,new(140000,100,24));
            Assert.True(selection.Fits,$"Budget at {x},{z}: {selection.Cost}");
            Assert.Equal(items.Length,selection.Lods.Length);
        }
        var bad=JsonSerializer.Serialize(manifest with {Version=8});
        Assert.Throws<InvalidDataException>(()=>VillageRenderManifest.Parse(bad));
    }
}
