using System.Security.Cryptography;
using System.Text.Json;
using TownRpg.Presentation;

namespace TownRpg.Core.Tests;

public class VillageHouseArtTests
{
    private static string Root { get { var p=new DirectoryInfo(AppContext.BaseDirectory);while(p is not null&&!File.Exists(Path.Combine(p.FullName,"project.godot")))p=p.Parent;return p!.FullName; } }
    private static byte[] Read(string path)=>File.ReadAllBytes(Path.Combine(Root,path));
    private static string Sha(byte[] bytes)=>Convert.ToHexString(SHA256.HashData(bytes)).ToLowerInvariant();
    [Fact]
    public void FourHouseFixturesKeepDoorwayAndLoadingAreasClear()
    {
        var d=VillageHouseArtLayout3D.Layout;var v=VillageLayoutProfile3D.Parse(File.ReadAllText(Path.Combine(Root,"assets/generated/village/layout-v4.json")));
        Assert.Equal(new[]{"house02","house03","house13","house15"},d.Houses.Select(h=>h.Id));
        foreach(var h in d.Houses)
        {
            var b=v.Geometry.Buildings.Single(b=>b.Id==h.Id);var pad=b.LocalPad;var r=h.Loading;Assert.True(r[2]>=3&&r[3]>=3);
            foreach(var p in d.Props.Where(p=>p.House==h.Id))
            {
                var x=p.Position[0];var z=p.Position[2];var w=p.Size[0];var depth=p.Size[2];
                Assert.True(x-w/2>pad.X+.1f&&x+w/2<pad.X+pad.Width-.1f,p.Id);
                Assert.True(z-depth/2>b.Size[2]/2&&z+depth/2<=pad.Z+pad.Depth-.1f+.001f,p.Id);
                Assert.True(p.Position[1]>=2.6f||x+w/2<=-1.4f||x-w/2>=1.4f,p.Id+" central passage");
                Assert.True(x+w/2<=r[0]||x-w/2>=r[0]+r[2]||z+depth/2<=r[1]||z-depth/2>=r[1]+r[3],p.Id+" loading");
            }
        }
    }
    [Fact]
    public void ActualV5PreservesNonSamplePayloadsAndMatchesCollisionTransforms()
    {
        var old=Read("assets/generated/village/3d/village-v4.glb");var raw=Read("assets/generated/village/3d/village-v5.glb");
        using var a=JsonDocument.Parse(old.AsMemory(20,BitConverter.ToInt32(old,12)));using var b=JsonDocument.Parse(raw.AsMemory(20,BitConverter.ToInt32(raw,12)));
        using var manifest=JsonDocument.Parse(Read("assets/generated/village/3d/village-v5-manifest.json"));var m=manifest.RootElement;
        Assert.Equal(Sha(old),m.GetProperty("baselineGlbSha256").GetString());Assert.Equal(Sha(raw),m.GetProperty("glbSha256").GetString());
        Assert.Equal(Sha(Read("scripts/presentation/VillageHouseArtLayout3D.cs")),m.GetProperty("fixtureLayoutSha256").GetString());Assert.False(m.GetProperty("runtimeDefault").GetBoolean());
        foreach(var p in m.GetProperty("terrainHashes").EnumerateObject())Assert.Equal(p.Value.GetString(),Sha(Read(p.Name)));
        var changed=m.GetProperty("modifiedObjects").EnumerateArray().Select(p=>p.GetString()!).ToHashSet();Assert.Equal(8,changed.Count);
        var nodes=b.RootElement.GetProperty("nodes").EnumerateArray().ToDictionary(n=>n.GetProperty("name").GetString()!);
        foreach(var node in a.RootElement.GetProperty("nodes").EnumerateArray())
        {
            var name=node.GetProperty("name").GetString()!;var current=nodes[name];
            foreach(var k in new[]{"translation","rotation","scale","matrix"})Assert.Equal(node.TryGetProperty(k,out var x)?x.GetRawText():null,current.TryGetProperty(k,out var y)?y.GetRawText():null);
            if(node.TryGetProperty("mesh",out var mesh)&&!changed.Contains(name))
                Assert.Equal(Signature(a.RootElement,old,mesh.GetInt32()),Signature(b.RootElement,raw,current.GetProperty("mesh").GetInt32()));
            if(name.StartsWith("HouseShell_")&&changed.Contains(name))
                Assert.Equal(Positions(a.RootElement,old,node.GetProperty("mesh").GetInt32()),Positions(b.RootElement,raw,current.GetProperty("mesh").GetInt32()));
        }
        foreach(var prop in VillageHouseArtLayout3D.Layout.Props)
        {
            var n=nodes["V5_"+prop.House+"_"+prop.Id];var pos=n.GetProperty("translation").EnumerateArray().Select(x=>x.GetSingle()).ToArray();
            for(var i=0;i<3;i++)Assert.InRange(Math.Abs(pos[i]-prop.Position[i]),0,.001);
        }
        var materials=b.RootElement.GetProperty("materials");var meshes=b.RootElement.GetProperty("meshes");
        foreach(var n in nodes.Values.Where(n=>n.TryGetProperty("mesh",out _)))
        foreach(var p in meshes[n.GetProperty("mesh").GetInt32()].GetProperty("primitives").EnumerateArray())
        {
            var material=materials[p.GetProperty("material").GetInt32()].GetProperty("name").GetString()!;var name=n.GetProperty("name").GetString()!;
            if(material.StartsWith("V5_"))Assert.True(changed.Contains(name)||name.StartsWith("V5_"),name);
            Assert.True(p.GetProperty("attributes").TryGetProperty("TEXCOORD_0",out _));Assert.True(p.GetProperty("attributes").TryGetProperty("NORMAL",out _));
        }
        Assert.InRange(m.GetProperty("combinedTriangles").GetInt32(),1,1200000);Assert.InRange(m.GetProperty("combinedTextureMiB").GetDouble(),1,384);
    }
    internal static string Signature(JsonElement d,byte[] raw,int mesh)
    {
        string Accessor(int index)
        {
            var a=d.GetProperty("accessors")[index];var v=d.GetProperty("bufferViews")[a.GetProperty("bufferView").GetInt32()];
            var components=a.GetProperty("type").GetString() switch {"SCALAR"=>1,"VEC2"=>2,"VEC3"=>3,"VEC4"=>4,_=>throw new Exception("accessor type")};
            var bytes=a.GetProperty("componentType").GetInt32() switch {5120 or 5121=>1,5122 or 5123=>2,_=>4};var size=components*bytes;
            var offset=28+BitConverter.ToInt32(raw,12)+(v.TryGetProperty("byteOffset",out var vo)?vo.GetInt32():0)+(a.TryGetProperty("byteOffset",out var ao)?ao.GetInt32():0);
            var stride=v.TryGetProperty("byteStride",out var st)?st.GetInt32():size;var packed=new byte[a.GetProperty("count").GetInt32()*size];
            for(var i=0;i<packed.Length/size;i++)Array.Copy(raw,offset+i*stride,packed,i*size,size);return Sha(packed);
        }
        return string.Join("|",d.GetProperty("meshes")[mesh].GetProperty("primitives").EnumerateArray().Select(p=>d.GetProperty("materials")[p.GetProperty("material").GetInt32()].GetProperty("name").GetString()+":"+Accessor(p.GetProperty("indices").GetInt32())+":"+string.Join(",",p.GetProperty("attributes").EnumerateObject().OrderBy(a=>a.Name).Select(a=>a.Name+Accessor(a.Value.GetInt32())))));
    }
    private static string[] Positions(JsonElement d,byte[] raw,int mesh)
    {
        var points=new HashSet<string>();
        foreach(var p in d.GetProperty("meshes")[mesh].GetProperty("primitives").EnumerateArray())
        {
            var a=d.GetProperty("accessors")[p.GetProperty("attributes").GetProperty("POSITION").GetInt32()];var v=d.GetProperty("bufferViews")[a.GetProperty("bufferView").GetInt32()];
            var start=28+BitConverter.ToInt32(raw,12)+(v.TryGetProperty("byteOffset",out var vo)?vo.GetInt32():0)+(a.TryGetProperty("byteOffset",out var ao)?ao.GetInt32():0);var stride=v.TryGetProperty("byteStride",out var st)?st.GetInt32():12;
            for(var i=0;i<a.GetProperty("count").GetInt32();i++)points.Add(Convert.ToHexString(raw.AsSpan(start+i*stride,12)));
        }
        return points.Order().ToArray();
    }
}
