using System.Security.Cryptography;
using System.Text.Json;
using TownRpg.Presentation;

namespace TownRpg.Core.Tests;

public class VillageExteriorTests
{
    [Fact]
    public void WorkAndLoadingAreasExcludeFixtureFootprints()
    {
        var layout=VillageExteriorLayout3D.Layout;
        Assert.Equal(2,layout.Version);
        Assert.Equal(layout.Props.Length,layout.Props.Select(p=>p.Id).Distinct().Count());
        foreach(var zone in layout.Zones)
        foreach(var prop in layout.Props.Where(p=>p.Solid && p.Kind!="overhead"))
        {
            var r=zone.Rect;var p=prop.Position;var s=prop.Size;
            var x=Math.Min(r[0]+r[2],p[0]+s[0]/2)-Math.Max(r[0],p[0]-s[0]/2);
            var z=Math.Min(r[1]+r[3],p[2]+s[2]/2)-Math.Max(r[1],p[2]-s[2]/2);
            Assert.True(x<=.001f || z<=.001f,$"{zone.Id} occupied by {prop.Id}");
        }
        var loading=layout.Zones.Single(z=>z.Id=="Loading").Rect;
        Assert.True(loading[2]>=3 && loading[3]>=3);
        foreach(var roof in layout.Props.Where(p=>p.Kind=="overhead"))
        {
            var building=VillageLayout3D.Layout.Buildings.Single(b=>b.Id==(roof.Id=="ShopRoof"?"shop":"workshop"));
            Assert.True(roof.Position[1]-building.Elevation>=2.6f);
        }
    }

    [Fact]
    public void ActualV2AssetMatchesSharedFixturesAndPreservedBaseline()
    {
        var dir=new DirectoryInfo(AppContext.BaseDirectory);
        while(dir is not null && !File.Exists(Path.Combine(dir.FullName,"project.godot"))) dir=dir.Parent;
        Assert.NotNull(dir);var root=dir.FullName;
        byte[] Read(string p)=>File.ReadAllBytes(Path.Combine(root,p));
        using var manifest=JsonDocument.Parse(Read("assets/generated/village/3d/village-v2-manifest.json"));
        var m=manifest.RootElement;
        foreach(var (key,path) in new[]{
            ("runtimeGlbSha256","assets/generated/village/3d/village-v2.glb"),
            ("baseSourceSha256","art/blender/village/village-v1.blend"),
            ("baseGlbSha256","assets/generated/village/3d/village-v1.glb"),
            ("baseLayoutSha256","scripts/presentation/VillageLayout3D.cs"),
            ("exteriorLayoutSha256","scripts/presentation/VillageExteriorLayout3D.cs")})
            Assert.Equal(Convert.ToHexString(SHA256.HashData(Read(path))).ToLowerInvariant(),m.GetProperty(key).GetString());
        Assert.False(m.GetProperty("runtimeDefault").GetBoolean());Assert.False(m.GetProperty("graybox").GetBoolean());
        var raw=Read("assets/generated/village/3d/village-v2.glb");
        using var glb=JsonDocument.Parse(raw.AsMemory(20,BitConverter.ToInt32(raw,12)));
        var data=glb.RootElement;var nodes=data.GetProperty("nodes").EnumerateArray().Where(n=>n.TryGetProperty("name",out _))
            .ToDictionary(n=>n.GetProperty("name").GetString()!,n=>n);
        foreach(var prop in VillageExteriorLayout3D.Layout.Props)
        {
            var n=nodes["V2_"+prop.Id];var pos=n.GetProperty("translation").EnumerateArray().Select(e=>e.GetSingle()).ToArray();
            for(var i=0;i<3;i++) Assert.InRange(Math.Abs(pos[i]-prop.Position[i]),0,.002f);
        }
        foreach(var name in new[]{"V2_NoticePaper_0","V2_DeliveryParcel_0","V2_WorkshopCanopy","V2_ShopAwning","V2_RegistrationBook"}) Assert.Contains(name,nodes.Keys);
        foreach(var name in new[]{"Landmark_noticeboard","Landmark_shop_bench","Landmark_shop_crates"}) Assert.DoesNotContain(name,nodes.Keys);
        Assert.False(data.TryGetProperty("skins",out _));Assert.False(data.TryGetProperty("animations",out _));
        Assert.True(m.GetProperty("preservedObjects").EnumerateObject().Count()>200);
        foreach(var mesh in data.GetProperty("meshes").EnumerateArray())
        foreach(var primitive in mesh.GetProperty("primitives").EnumerateArray())
        {
            Assert.True(primitive.GetProperty("attributes").TryGetProperty("TEXCOORD_0",out _));
            Assert.True(primitive.GetProperty("attributes").TryGetProperty("NORMAL",out _));
        }
    }
}
