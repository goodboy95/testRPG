using System.Text.Json.Nodes;
using TownRpg.Presentation;
using Xunit;

namespace TownRpg.Core.Tests;

public sealed class VillageAnimeTests
{
    private static string Root()
    {
        var d=new DirectoryInfo(AppContext.BaseDirectory);
        while(d is not null && !File.Exists(Path.Combine(d.FullName,"project.godot")))d=d.Parent;
        return d?.FullName ?? throw new DirectoryNotFoundException();
    }
    [Fact]
    public void AnimePreviewCannotChangeFormalOrDefaultSelection()
    {
        Assert.Same(VillageVisualProfile.V7,VillageVisualProfile.Select(true,true,["--village-v8"]));
        Assert.Same(VillageVisualProfile.V7,VillageVisualProfile.Select(false,false,[]));
        Assert.Same(VillageVisualProfile.AnimeV8,VillageVisualProfile.Select(false,false,["--village-v8"]));
        Assert.Same(VillageVisualProfile.AnimeV8,VillageVisualProfile.Select(false,true,[]));
    }
    [Fact]
    public void ActualV8ManifestCoversBuildingsAndRejectsCrossVersionBundles()
    {
        var text=File.ReadAllText(Path.Combine(Root(),"assets/generated/village/stream-v8/manifest.json"));
        var m=VillageRenderManifest.Parse(text);
        Assert.Equal(8,m.Version);Assert.Equal(24,m.Units.Count(u=>u.Category=="building"));
        Assert.All(m.Units,u=>Assert.Equal(4,u.Lods.Length));
        Assert.All(m.Bundles,b=>Assert.True(File.Exists(Path.Combine(Root(),b.Path[6..]))));
        var node=JsonNode.Parse(text)!;
        node["bundles"]![0]!["path"]="res://assets/generated/village/stream-v7/plaza_detail.glb";
        Assert.Throws<InvalidDataException>(()=>VillageRenderManifest.Parse(node.ToJsonString()));
    }
    [Fact]
    public void HomeExportRetainsInteractiveHingeHierarchy()
    {
        var path=Path.Combine(Root(),"assets/generated/home/anime-v8/home-anime-v8.glb");
        var bytes=File.ReadAllBytes(path);var length=BitConverter.ToInt32(bytes,12);
        var data=JsonNode.Parse(System.Text.Encoding.UTF8.GetString(bytes,20,length))!;
        var nodes=data["nodes"]!.AsArray();
        foreach(var door in HomeLayout3D.Layout.Doors)
        for(var i=0;i<(door.Double?2:1);i++)
        {
            var name=$"Door_{door.Id}_Hinge{i}";
            var hinge=Assert.Single(nodes,n=>n?["name"]?.GetValue<string>()==name);
            Assert.NotEmpty(hinge!["children"]!.AsArray());
        }
    }
    [Fact]
    public void TerrainVisualDerivativeUsesSameNativeHeightData()
    {
        var path=Path.Combine(Root(),"assets/generated/village/terrain-v8/terrain.tscn");
        var text=File.ReadAllText(path);
        Assert.Contains("data_directory = \"res://assets/generated/village/terrain-v4/data\"",text);
        Assert.Contains("vertex_spacing = 0.5",text);
        Assert.Contains("collision_mode = 3",text);
        Assert.Contains("terrain-v8/terrain-assets.tres",text);
    }
}
