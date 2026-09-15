using System.Security.Cryptography;
using System.Text.Json;

namespace TownRpg.Core.Tests;

public class VillageArtTests
{
    private static string Root { get { var p=new DirectoryInfo(AppContext.BaseDirectory);while(p is not null && !File.Exists(Path.Combine(p.FullName,"project.godot")))p=p.Parent;return p!.FullName; } }
    private static byte[] Read(string p)=>File.ReadAllBytes(Path.Combine(Root,p));
    private static JsonDocument Glb(byte[] raw)=>JsonDocument.Parse(raw.AsMemory(20,BitConverter.ToInt32(raw,12)));
    [Fact]
    public void ArtSamplePreservesBaselineTransformsAndUsesOnlyThreeNewConstructionObjects()
    {
        using var manifest=JsonDocument.Parse(Read("assets/generated/village/3d/village-v3-manifest.json"));var m=manifest.RootElement;
        Assert.False(m.GetProperty("runtimeDefault").GetBoolean());
        var old=Read("assets/generated/village/3d/village-v2.glb");var current=Read("assets/generated/village/3d/village-v3.glb");
        Assert.Equal(m.GetProperty("baselineGlbSha256").GetString(),Convert.ToHexString(SHA256.HashData(old)).ToLowerInvariant());
        Assert.Equal(m.GetProperty("glbSha256").GetString(),Convert.ToHexString(SHA256.HashData(current)).ToLowerInvariant());
        using var a=Glb(old);using var b=Glb(current);
        var nodes=b.RootElement.GetProperty("nodes").EnumerateArray().ToDictionary(n=>n.GetProperty("name").GetString()!,n=>n);
        foreach(var n in a.RootElement.GetProperty("nodes").EnumerateArray())
        {
            var name=n.GetProperty("name").GetString()!;Assert.Contains(name,nodes.Keys);
            foreach(var key in new[]{"translation","rotation","scale","matrix"})
                Assert.Equal(n.TryGetProperty(key,out var x)?x.GetRawText():null,nodes[name].TryGetProperty(key,out var y)?y.GetRawText():null);
        }
        Assert.Equal(3,nodes.Keys.Count(n=>n.StartsWith("V3_")));
        Assert.Equal(nodes["Tree_037"].GetProperty("mesh").GetInt32(),nodes["Tree_043"].GetProperty("mesh").GetInt32());
        Assert.NotEqual(nodes["PlazaTree"].GetProperty("mesh").GetInt32(),nodes["Tree_043"].GetProperty("mesh").GetInt32());
        Assert.InRange(m.GetProperty("combinedConservativeTriangles").GetInt32(),1,1200000);
        Assert.InRange(m.GetProperty("combinedTextureMemoryMiB").GetDouble(),1,384);
        Assert.InRange(current.Length,1,100*1024*1024);
    }
    [Fact]
    public void ExportedLeavesAreCutoutAndMaterialsStayIsolatedFromOtherTreesAndHomes()
    {
        using var doc=Glb(Read("assets/generated/village/3d/village-v3.glb"));var d=doc.RootElement;
        var materials=d.GetProperty("materials").EnumerateArray().ToArray();var meshes=d.GetProperty("meshes").EnumerateArray().ToArray();
        var leaf=materials.Single(m=>m.GetProperty("name").GetString()=="V3_Leaves");
        Assert.Equal("MASK",leaf.GetProperty("alphaMode").GetString());Assert.True(leaf.GetProperty("doubleSided").GetBoolean());
        Assert.InRange(leaf.GetProperty("alphaCutoff").GetSingle(),.4f,.5f);
        using var manifest=JsonDocument.Parse(Read("assets/generated/village/3d/village-v3-manifest.json"));
        var allowed=manifest.RootElement.GetProperty("modifiedObjects").EnumerateArray().Select(x=>x.GetString()!).ToHashSet();
        foreach(var node in d.GetProperty("nodes").EnumerateArray())
        {
            if(!node.TryGetProperty("mesh",out var index))continue;var name=node.GetProperty("name").GetString()!;
            foreach(var prim in meshes[index.GetInt32()].GetProperty("primitives").EnumerateArray())
            {
                var material=materials[prim.GetProperty("material").GetInt32()].GetProperty("name").GetString()!;
                if(material.StartsWith("V3_"))Assert.True(allowed.Contains(name)||name.StartsWith("V3_"),name);
                Assert.True(prim.GetProperty("attributes").TryGetProperty("NORMAL",out _));Assert.True(prim.GetProperty("attributes").TryGetProperty("TEXCOORD_0",out _));
            }
        }
        Assert.False(d.TryGetProperty("animations",out _));Assert.False(d.TryGetProperty("skins",out _));
    }
}
