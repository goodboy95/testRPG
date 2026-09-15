using System.Numerics;
using System.Text.Json;

namespace TownRpg.Presentation;

/// <summary>Instance-owned presentation layout. Legacy geometry is never overwritten by a newer village.</summary>
public sealed class VillageLayoutProfile3D
{
    public sealed record Tree(string Id,float X,float Z,float Height,int Variant);
    public sealed record Document(int Version,VillageLayout3D.Definition Geometry,VillageExteriorLayout3D.Definition Fixtures,Tree[] Trees,VillageExteriorLayout3D.Zone[] PlanningZones);
    public VillageLayout3D.Definition Geometry { get; }
    public VillageExteriorLayout3D.Definition Fixtures { get; }
    public IReadOnlyList<Tree> Trees { get; }
    public IReadOnlyList<VillageExteriorLayout3D.Zone> PlanningZones { get; }
    public bool Compact=>Geometry.Version==4;
    public VillageLayout3D.Building Home=>Geometry.Buildings.Single(b=>b.Id=="home");
    public static VillageLayoutProfile3D Legacy { get; }=new(VillageLayout3D.Layout,VillageExteriorLayout3D.Layout,
        VillageLayout3D.Trees.Select((t,i)=>new Tree($"Tree_{i:000}",t.X,t.Z,t.Height,t.Variant)).ToArray(),[]);
    private VillageLayoutProfile3D(VillageLayout3D.Definition geometry,VillageExteriorLayout3D.Definition fixtures,Tree[] trees,VillageExteriorLayout3D.Zone[] zones)
    { Geometry=geometry;Fixtures=fixtures;Trees=trees;PlanningZones=zones; }
    public static VillageLayoutProfile3D Parse(string json)
    {
        var d=JsonSerializer.Deserialize<Document>(json,new JsonSerializerOptions {PropertyNameCaseInsensitive=true})
            ?? throw new InvalidDataException("Missing village layout.");
        if(d.Version!=4 || d.Geometry.Version!=4 || d.Geometry.Buildings.Length!=24 || d.Geometry.Buildings.Select(b=>b.Id).Distinct().Count()!=24)
            throw new InvalidDataException("Invalid compact village layout.");
        return new(d.Geometry,d.Fixtures,d.Trees,d.PlanningZones);
    }
    public float GroundHeight(float x,float z)
    {
        if(!Compact)return VillageLayout3D.GroundHeight(x,z);
        var h=VillageLayout3D.BaseHeight(x,z);
        static float Smooth(float t){t=Math.Clamp(t,0,1);return t*t*(3-2*t);}
        h-=2.6f*(1-Smooth((Math.Abs(x-VillageLayout3D.StreamX(z))-4.5f)/4));
        foreach(var b in Geometry.Buildings)
        {var outside=b.LocalPad.OutsideDistance(b.ToLocal(new(x,z)));if(outside<3)h+=(b.Elevation-h)*(1-Smooth(outside/3));}
        foreach(var b in Geometry.Bridges)
        {var d=Math.Abs(x-b.CenterX);if(Math.Abs(z-b.Z)<=b.Width/2+1 && d>=b.HalfSpan-1 && d<=b.HalfSpan+2.5f)h+=(b.DeckY-h)*Smooth((b.HalfSpan+2.5f-d)/1.5f);}
        var a=Geometry.Bounds;var edge=Math.Min(Math.Min(x-a[0],a[0]+a[2]-x),Math.Min(z-a[1],a[1]+a[3]-z));
        return h+4.5f*(1-Smooth(edge/6));
    }
}
