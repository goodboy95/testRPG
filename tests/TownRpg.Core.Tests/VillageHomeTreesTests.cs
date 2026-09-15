using System.Text.Json;
using System.Security.Cryptography;
using TownRpg.Presentation;

namespace TownRpg.Core.Tests;

public class VillageHomeTreesTests
{
    private static string Root {get{var d=new DirectoryInfo(AppContext.BaseDirectory);while(d is not null&&!File.Exists(Path.Combine(d.FullName,"project.godot")))d=d.Parent;return d!.FullName;}}
    private static byte[] Read(string p)=>File.ReadAllBytes(Path.Combine(Root,p));
    private static string Sha(byte[] b)=>Convert.ToHexString(SHA256.HashData(b)).ToLowerInvariant();
    [Fact]
    public void HomeFixturesLeaveOriginalGateAndDoorRoutesClear()
    {
        var d=HomeIdentityLayout3D.Layout;Assert.Equal(2,d.Props.Length);Assert.Equal(2.8f,d.Clearance[2]);
        foreach(var p in d.Props)
        {
            Assert.True(Math.Abs(p.Position[0])-p.Size[0]/2>1.4f,p.Id);
            Assert.True(p.Position[2]-p.Size[2]/2>4.5f,p.Id);
            Assert.True(Math.Abs(p.Position[0])+p.Size[0]/2<5.8f,p.Id);
            Assert.True(p.Position[2]+p.Size[2]/2<5.8f,p.Id);
        }
    }
    [Fact]
    public void ActualV6PreservesV5BuildingsTerrainAndTreeAnchorsWithinBudget()
    {
        using var manifest=JsonDocument.Parse(Read("assets/generated/village/3d/village-v6-manifest.json"));var m=manifest.RootElement;
        var old=Read("assets/generated/village/3d/village-v5.glb");var raw=Read("assets/generated/village/3d/village-v6.glb");
        Assert.Equal(Sha(old),m.GetProperty("baselineGlbSha256").GetString());Assert.Equal(Sha(raw),m.GetProperty("glbSha256").GetString());Assert.True(m.GetProperty("runtimeDefault").GetBoolean());
        Assert.Equal(Sha(Read("scripts/presentation/HomeIdentityLayout3D.cs")),m.GetProperty("layoutSha256").GetString());
        foreach(var p in m.GetProperty("terrainHashes").EnumerateObject())Assert.Equal(p.Value.GetString(),Sha(Read(p.Name)));
        Compare(old,raw,m);
        using var d=JsonDocument.Parse(raw.AsMemory(20,BitConverter.ToInt32(raw,12)));var root=d.RootElement;var nodes=root.GetProperty("nodes").EnumerateArray().ToDictionary(n=>n.GetProperty("name").GetString()!);
        var trees=nodes.Where(n=>n.Key.StartsWith("Tree_")).ToArray();Assert.Equal(57,trees.Length);Assert.True(nodes.ContainsKey("PlazaTree"));
        var changedTrees=trees.Where(n=>n.Key is not "Tree_037" and not "Tree_043").ToArray();Assert.Equal(55,changedTrees.Length);Assert.Equal(3,changedTrees.Select(n=>n.Value.GetProperty("mesh").GetInt32()).Distinct().Count());
        foreach(var n in changedTrees)
        {
            var mesh=root.GetProperty("meshes")[n.Value.GetProperty("mesh").GetInt32()];var triangles=mesh.GetProperty("primitives").EnumerateArray().Sum(p=>root.GetProperty("accessors")[p.GetProperty("indices").GetInt32()].GetProperty("count").GetInt32()/3);
            Assert.InRange(triangles,1,1900);
            foreach(var p in mesh.GetProperty("primitives").EnumerateArray())Assert.StartsWith("V3_",root.GetProperty("materials")[p.GetProperty("material").GetInt32()].GetProperty("name").GetString());
        }
        foreach(var p in HomeIdentityLayout3D.Layout.Props)
        {var n=nodes["V6_"+p.Id];var xyz=n.GetProperty("translation").EnumerateArray().Select(x=>x.GetSingle()).ToArray();for(var i=0;i<3;i++)Assert.InRange(Math.Abs(xyz[i]-p.Position[i]),0,.001);}
        Assert.InRange(m.GetProperty("combinedTriangles").GetInt32(),1,1200000);Assert.InRange(m.GetProperty("combinedTextureMiB").GetDouble(),1,384);
        Assert.Contains("meshes/generate_lods=false",File.ReadAllText(Path.Combine(Root,"assets/generated/village/3d/village-v6.glb.import")));
    }
    [Fact]
    public void HomeDerivativeRetainsInteriorDoorPivotsAndUsesSameExteriorTrim()
    {
        using var manifest=JsonDocument.Parse(Read("assets/generated/home/3d/home-identity-v2.json"));var m=manifest.RootElement;var old=Read("assets/generated/home/3d/home-v1.glb");var raw=Read("assets/generated/home/3d/home-identity-v2.glb");
        Compare(old,raw,m);Assert.Equal(Sha(raw),m.GetProperty("glbSha256").GetString());
        using var h=JsonDocument.Parse(raw.AsMemory(20,BitConverter.ToInt32(raw,12)));var vraw=Read("assets/generated/village/3d/village-v6.glb");using var v=JsonDocument.Parse(vraw.AsMemory(20,BitConverter.ToInt32(vraw,12)));
        foreach(var name in new[]{"V6_HomeTrim","V6_Bicycle_Mesh","V6_PorchStorage_Mesh"})
        {
            var a=h.RootElement.GetProperty("nodes").EnumerateArray().Single(n=>n.GetProperty("name").GetString()==name);var b=v.RootElement.GetProperty("nodes").EnumerateArray().Single(n=>n.GetProperty("name").GetString()==name);
            Assert.Equal(VillageHouseArtTests.Signature(h.RootElement,raw,a.GetProperty("mesh").GetInt32()),VillageHouseArtTests.Signature(v.RootElement,vraw,b.GetProperty("mesh").GetInt32()));
        }
    }
    private static void Compare(byte[] old,byte[] raw,JsonElement m)
    {
        using var a=JsonDocument.Parse(old.AsMemory(20,BitConverter.ToInt32(old,12)));using var b=JsonDocument.Parse(raw.AsMemory(20,BitConverter.ToInt32(raw,12)));
        var changes=m.GetProperty("modifiedObjects").EnumerateArray().Select(x=>x.GetString()).ToHashSet();var nodes=b.RootElement.GetProperty("nodes").EnumerateArray().ToDictionary(n=>n.GetProperty("name").GetString()!);
        foreach(var n in a.RootElement.GetProperty("nodes").EnumerateArray())
        {
            var name=n.GetProperty("name").GetString()!;var current=nodes[name];
            foreach(var key in new[]{"translation","rotation","scale","matrix"})Assert.Equal(n.TryGetProperty(key,out var x)?x.GetRawText():null,current.TryGetProperty(key,out var y)?y.GetRawText():null);
            if(n.TryGetProperty("mesh",out var mesh)&&!changes.Contains(name))Assert.Equal(VillageHouseArtTests.Signature(a.RootElement,old,mesh.GetInt32()),VillageHouseArtTests.Signature(b.RootElement,raw,current.GetProperty("mesh").GetInt32()));
        }
    }
}
