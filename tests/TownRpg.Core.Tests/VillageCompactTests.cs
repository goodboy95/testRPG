using System.Numerics;
using System.Text.Json;
using TownRpg.Presentation;
using Xunit;

namespace TownRpg.Core.Tests;

public sealed class VillageCompactTests
{
    private static string Root()
    {
        var p=new DirectoryInfo(AppContext.BaseDirectory);
        while(p is not null && !File.Exists(Path.Combine(p.FullName,"project.godot")))p=p.Parent;
        return p?.FullName??throw new InvalidOperationException("Project root missing");
    }
    private static VillageLayoutProfile3D Layout()=>VillageLayoutProfile3D.Parse(File.ReadAllText(Path.Combine(Root(),"assets/generated/village/layout-v4.json")));
    [Fact]
    public void CompactLayoutRetainsAnchorsAndSizesWhileShorteningActualRoutes()
    {
        var v=Layout();var old=VillageLayout3D.Layout;
        Assert.Equal(old.Bounds,v.Geometry.Bounds);Assert.Equal(old.Spawn,v.Geometry.Spawn);
        Assert.Equal(VillageLayout3D.Home.Position,v.Home.Position);Assert.Equal(VillageLayout3D.Home.Yaw,v.Home.Yaw);
        Assert.Equal(old.Bridges,v.Geometry.Bridges);
        foreach(var b in v.Geometry.Buildings)Assert.Equal(old.Buildings.Single(x=>x.Id==b.Id).Size,b.Size);
        Assert.InRange(VillageLayout3D.RouteLength(v.Geometry.Routes.Single(r=>r.Id=="home_shop")),55,65);
        foreach(var (id,factor) in new[]{("creek_loop",.85f),("rail_loop",.8f)})
            Assert.True(VillageLayout3D.RouteLength(v.Geometry.Routes.Single(r=>r.Id==id))<=VillageLayout3D.RouteLength(old.Routes.Single(r=>r.Id==id))*factor);
        Assert.Equal(1,VillageLayoutProfile3D.Legacy.Geometry.Version);
        Assert.NotSame(VillageLayoutProfile3D.Legacy.Geometry,v.Geometry);
    }
    [Fact]
    public void LotsAndReservedWorkSpacesDoNotOverlapSolids()
    {
        var v=Layout();
        static float[] Rect(VillageLayout3D.Building b)
        {var r=b.LocalPad;var p=new[]{b.ToWorld(new(r.X,r.Z)),b.ToWorld(new(r.X+r.Width,r.Z)),b.ToWorld(new(r.X+r.Width,r.Z+r.Depth)),b.ToWorld(new(r.X,r.Z+r.Depth))};return [p.Min(a=>a.X),p.Min(a=>a.Y),p.Max(a=>a.X)-p.Min(a=>a.X),p.Max(a=>a.Y)-p.Min(a=>a.Y)];}
        static bool Overlap(float[] a,float[] b)=>Math.Min(a[0]+a[2],b[0]+b[2])-Math.Max(a[0],b[0])>.01f && Math.Min(a[1]+a[3],b[1]+b[3])-Math.Max(a[1],b[1])>.01f;
        for(var i=0;i<v.Geometry.Buildings.Length;i++)for(var j=i+1;j<v.Geometry.Buildings.Length;j++)
            Assert.False(Overlap(Rect(v.Geometry.Buildings[i]),Rect(v.Geometry.Buildings[j])),v.Geometry.Buildings[i].Id+" / "+v.Geometry.Buildings[j].Id);
        foreach(var zone in v.Fixtures.Zones.Concat(v.PlanningZones))
        foreach(var p in v.Fixtures.Props.Where(p=>p.Solid && p.Kind!="overhead"))
            Assert.False(Overlap(zone.Rect,[p.Position[0]-p.Size[0]/2,p.Position[2]-p.Size[2]/2,p.Size[0],p.Size[2]]),zone.Id+" / "+p.Id);
        Assert.Equal(11,v.PlanningZones.Single(z=>z.Id=="sparring_with_border").Rect[2]);
    }
    [Fact]
    public void AuthoredTerrainKeepsProtectedHeightsAndSeparateNativeRegions()
    {
        var root=Root();var folder=Path.Combine(root,"art/terrain/village-v4");
        var actual=File.ReadAllBytes(Path.Combine(folder,"height.bin"));var old=File.ReadAllBytes(Path.Combine(folder,"baseline/height.bin"));var mask=File.ReadAllBytes(Path.Combine(folder,"protected.bin"));
        Assert.Equal(512*512*4,actual.Length);var changed=0;
        for(var i=0;i<mask.Length;i++)
        {var a=BitConverter.ToSingle(actual,i*4);Assert.True(float.IsFinite(a));if(mask[i]!=0)Assert.InRange(Math.Abs(a-BitConverter.ToSingle(old,i*4)),0,.01f);else if(a!=BitConverter.ToSingle(old,i*4))changed++;}
        Assert.True(changed>0);
        Assert.Contains("terrain-v4/data",File.ReadAllText(Path.Combine(root,"assets/generated/village/terrain-v4/terrain.tscn")));
        Assert.Equal(4,Directory.GetFiles(Path.Combine(root,"assets/generated/village/terrain-v4/data"),"*.res").Length);
    }
    [Fact]
    public void ActualExportRetainsFixedHomeAndSharedBuildingMeshPayloads()
    {
        var root=Root();var p=Path.Combine(root,"assets/generated/village/3d/village-v4.glb");var raw=File.ReadAllBytes(p);
        using var doc=JsonDocument.Parse(raw.AsMemory(20,BitConverter.ToInt32(raw,12)));var d=doc.RootElement;
        var nodes=d.GetProperty("nodes").EnumerateArray().ToDictionary(n=>n.GetProperty("name").GetString()!,n=>n);
        foreach(var b in Layout().Geometry.Buildings.Where(b=>b.Kind!="home"))
        {var node=nodes["Building_"+b.Id];var t=node.GetProperty("translation").EnumerateArray().Select(x=>x.GetSingle()).ToArray();for(var i=0;i<3;i++)Assert.InRange(Math.Abs(t[i]-b.Position[i]),0,.001f);}
        using var meta=JsonDocument.Parse(File.ReadAllText(Path.Combine(root,"assets/generated/village/3d/village-v4-manifest.json")));
        Assert.True(meta.RootElement.GetProperty("preservedMeshes").GetInt32()>200);
        Assert.True(meta.RootElement.GetProperty("combinedTriangles").GetInt32()<=1200000);
        Assert.True(meta.RootElement.GetProperty("combinedTextureMiB").GetDouble()<=384);
        Assert.True(nodes.ContainsKey("HomeExterior") && nodes.ContainsKey("PlazaTree"));
    }
}
