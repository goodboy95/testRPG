using System.Security.Cryptography;
using System.Text.Json;
using TownRpg.Presentation;

namespace TownRpg.Core.Tests;

public class VillageAssetTests
{
    [Fact]
    public void ActualVillageGlbMatchesTerrainPlacementsAndSharedExteriorContract()
    {
        var directory=new DirectoryInfo(AppContext.BaseDirectory);
        while(directory is not null && !File.Exists(Path.Combine(directory.FullName,"project.godot"))) directory=directory.Parent;
        Assert.NotNull(directory);var root=directory.FullName;
        byte[] Read(string path)=>File.ReadAllBytes(Path.Combine(root,path));
        var raw=Read("assets/generated/village/3d/village-v1.glb");
        Assert.Equal("glTF",System.Text.Encoding.ASCII.GetString(raw,0,4));
        var length=BitConverter.ToInt32(raw,12);
        using var glb=JsonDocument.Parse(raw.AsMemory(20,length));var data=glb.RootElement;
        using var manifest=JsonDocument.Parse(Read("assets/generated/village/3d/village-v1-manifest.json"));var meta=manifest.RootElement;
        Assert.Equal("independent-preview",meta.GetProperty("status").GetString());Assert.False(meta.GetProperty("runtimeDefault").GetBoolean());
        Assert.False(meta.GetProperty("graybox").GetBoolean());
        Assert.Equal(Convert.ToHexString(SHA256.HashData(raw)).ToLowerInvariant(),meta.GetProperty("runtimeGlbSha256").GetString());
        Assert.Equal(Convert.ToHexString(SHA256.HashData(Read("scripts/presentation/VillageLayout3D.cs"))).ToLowerInvariant(),meta.GetProperty("layoutSha256").GetString());
        Assert.Equal(Convert.ToHexString(SHA256.HashData(Read("art/blender/home/home-v1.blend"))).ToLowerInvariant(),meta.GetProperty("homeSourceSha256").GetString());
        Assert.False(data.TryGetProperty("skins",out _));Assert.False(data.TryGetProperty("animations",out _));Assert.False(data.TryGetProperty("cameras",out _));
        var nodes=data.GetProperty("nodes").EnumerateArray().Where(n=>n.TryGetProperty("name",out _)).ToDictionary(n=>n.GetProperty("name").GetString()!,n=>n);
        foreach(var name in new[]{"Terrain","StreamWater","HomeExterior","HomeOpenYardGate","Bridge_south","Bridge_north","OldRailway","PlazaTree"}) Assert.True(nodes.ContainsKey(name),name);
        foreach(var name in new[]{"PlayerBed","FamilyBed","KitchenSink","Toilet","ClosedYardGate"}) Assert.False(nodes.ContainsKey(name),name);
        foreach(var b in VillageLayout3D.Layout.Buildings)
        {
            var name=b.Id=="home"?"HomeExterior":"Building_"+b.Id;Assert.True(nodes.ContainsKey(name),name);
            var p=nodes[name].GetProperty("translation").EnumerateArray().Select(e=>e.GetSingle()).ToArray();
            for(var axis=0;axis<3;axis++) Assert.InRange(Math.Abs(p[axis]-b.Position[axis]),0,.002f);
        }
        foreach(var (a,b) in new[]{("house02","house05"),("house04","house07"),("house03","house10")})
            Assert.Equal(nodes["HouseShell_"+a].GetProperty("mesh").GetInt32(),nodes["HouseShell_"+b].GetProperty("mesh").GetInt32());
        var trees=nodes.Where(n=>n.Key.StartsWith("Tree_",StringComparison.Ordinal)&&n.Value.TryGetProperty("mesh",out _)).ToArray();
        Assert.Equal(VillageLayout3D.Trees.Count,trees.Length);
        Assert.InRange(trees.Select(n=>n.Value.GetProperty("mesh").GetInt32()).Distinct().Count(),1,3);
        var terrain=data.GetProperty("meshes")[nodes["Terrain"].GetProperty("mesh").GetInt32()];var checkedSamples=0;
        foreach(var primitive in terrain.GetProperty("primitives").EnumerateArray())
        {
            var accessor=data.GetProperty("accessors")[primitive.GetProperty("attributes").GetProperty("POSITION").GetInt32()];
            Assert.Equal(5126,accessor.GetProperty("componentType").GetInt32());
            var view=data.GetProperty("bufferViews")[accessor.GetProperty("bufferView").GetInt32()];
            var offset=28+length+(view.TryGetProperty("byteOffset",out var vo)?vo.GetInt32():0)+(accessor.TryGetProperty("byteOffset",out var ao)?ao.GetInt32():0);
            var stride=view.TryGetProperty("byteStride",out var st)?st.GetInt32():12;
            for(var i=0;i<accessor.GetProperty("count").GetInt32();i+=97)
            {
                var x=BitConverter.ToSingle(raw,offset+i*stride);var y=BitConverter.ToSingle(raw,offset+i*stride+4);var z=BitConverter.ToSingle(raw,offset+i*stride+8);
                Assert.InRange(Math.Abs(y-VillageLayout3D.GroundHeight(x,z)),0,.004f);checkedSamples++;
            }
        }
        Assert.True(checkedSamples>400);
        foreach(var mesh in data.GetProperty("meshes").EnumerateArray())
        foreach(var primitive in mesh.GetProperty("primitives").EnumerateArray()) Assert.True(primitive.GetProperty("attributes").TryGetProperty("TEXCOORD_0",out _));
        Assert.All(data.GetProperty("images").EnumerateArray(),im=>Assert.True(im.TryGetProperty("bufferView",out _)));
        var keys=File.ReadAllLines(Path.Combine(root,"localization/zh_CN.csv")).Select(l=>l.Split(',')[0]).ToHashSet(StringComparer.Ordinal);
        foreach(var key in new[]{"VILLAGE_TITLE","VILLAGE_HOME_TITLE","VILLAGE_ENTER_HOME","VILLAGE_EXIT_HOME","VILLAGE_CONTROLS_KEYBOARD","VILLAGE_CONTROLS_PAD","VILLAGE_PAUSE","VILLAGE_RESET"}) Assert.Contains(key,keys);
    }
}
