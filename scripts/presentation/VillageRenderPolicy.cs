using System.Text.Json;

namespace TownRpg.Presentation;

// Presentation-only, deterministic and engine independent. Nothing here is persisted.
public sealed record VillageRenderManifest
{
    public int Version { get; init; }
    public string[] Districts { get; init; } = [];
    public VillageRenderBundle[] Bundles { get; init; } = [];
    public VillageRenderUnit[] Units { get; init; } = [];
    public static VillageRenderManifest Parse(string json)
    {
        var m=JsonSerializer.Deserialize<VillageRenderManifest>(json,new JsonSerializerOptions { PropertyNameCaseInsensitive=true })
            ?? throw new InvalidDataException("Missing streaming manifest");
        if(m.Version is not (7 or 8) || m.Districts.Length!=7 || m.Districts.Distinct().Count()!=7 || m.Units.Length==0 ||
           m.Units.Select(u=>u.Id).Distinct().Count()!=m.Units.Length || m.Bundles.Select(b=>b.Id).Distinct().Count()!=m.Bundles.Length)
            throw new InvalidDataException("Invalid village streaming manifest");
        foreach(var b in m.Bundles)
            if(!b.Path.StartsWith($"res://assets/generated/village/stream-v{m.Version}/",StringComparison.Ordinal) || b.Path.Contains(".."))
                throw new InvalidDataException("Invalid village bundle path");
        foreach(var u in m.Units)
            if(!m.Districts.Contains(u.District) || u.Bounds.Length!=6 || u.Bounds.Any(v=>!float.IsFinite(v)) || u.Bounds.Skip(3).Any(v=>v<0) ||
               u.Lods.Length!=4 || u.Lods.Any(l=>l.Triangles<=0 || l.Surfaces<=0 || l.Materials.Length==0 || !m.Bundles.Any(b=>b.Id==l.Bundle)) ||
               u.Lods.Skip(2).Any(l=>l.Surfaces>2) || u.Lods.Select(l=>l.Node).Distinct().Count()!=4)
                throw new InvalidDataException("Invalid village render unit: "+u.Id);
        return m;
    }
}
public sealed record VillageRenderBundle
{
    public string Id { get; init; }="";
    public string Path { get; init; }="";
}
public sealed record VillageRenderUnit
{
    public string Id { get; init; }="";
    public string District { get; init; }="";
    public string Category { get; init; }="";
    public bool Important { get; init; }
    public float[] Bounds { get; init; }=[];
    public VillageRenderLod[] Lods { get; init; }=[];
}
public sealed record VillageRenderLod
{
    public string Node { get; init; }="";
    public string Bundle { get; init; }="";
    public int Triangles { get; init; }
    public int Surfaces { get; init; }
    public string[] Materials { get; init; }=[];
    public float ScreenFraction { get; init; }
}
public readonly record struct VillageRenderBudget(int Triangles,int Meshes,int Batches)
{
    public static readonly VillageRenderBudget Near=new(220000,120,24);
    public static readonly VillageRenderBudget Wide=new(300000,160,32);
    public static readonly VillageRenderBudget Transition=new(360000,190,36);
}
// Development profiles only; never persisted or inferred from the current adapter name.
public sealed record VillageRenderProfile
{
    public string Id { get; init; }="integrated";
    public VillageRenderBudget SceneryNear { get; init; }=new(100000,100,24);
    public VillageRenderBudget SceneryWide { get; init; }=new(180000,140,32);
    public VillageRenderBudget FrameNear { get; init; }=VillageRenderBudget.Near;
    public VillageRenderBudget FrameWide { get; init; }=VillageRenderBudget.Wide;
    public VillageRenderBudget FrameTransition { get; init; }=VillageRenderBudget.Transition;
    public float Lod0Distance { get; init; }=16;
    public float Lod1Distance { get; init; }=35;
    public float Lod2Distance { get; init; }=85;
    public float ShadowDistance { get; init; }=40;
    public float ShadowCasterDistance { get; init; }=25;
    public int ShadowCasterMaxLod { get; init; }
    public int ShadowProxyLod { get; init; }=3;
    public float DetailDistance { get; init; }=38;
    public bool PreserveNearbyTrees { get; init; }
    public bool EnforceFrameDrawTarget { get; init; }
    public static readonly VillageRenderProfile Integrated=new();
    public static readonly VillageRenderProfile Laptop1060=new()
    {
        Id="laptop-gtx1060",SceneryNear=new(320000,140,120),SceneryWide=new(450000,180,180),
        FrameNear=new(600000,180,500),FrameWide=new(800000,240,700),FrameTransition=new(1000000,280,900),
        Lod0Distance=20,Lod1Distance=50,Lod2Distance=100,ShadowDistance=60,ShadowCasterDistance=40,
        ShadowCasterMaxLod=1,ShadowProxyLod=2,DetailDistance=55,PreserveNearbyTrees=true,EnforceFrameDrawTarget=true
    };
    public static VillageRenderProfile FromArguments(IEnumerable<string> args)=>
        args.Contains("--village-budget-integrated")?Integrated:Laptop1060;
}
public readonly record struct VillageRenderCost(int Triangles,int Meshes,int Batches,int Materials)
{
    public bool Fits(VillageRenderBudget budget)=>Triangles<=budget.Triangles && Meshes<=budget.Meshes && Batches<=budget.Batches;
}
public sealed record VillageRenderCandidate(VillageRenderUnit Unit,float Distance,float ScreenFraction,bool DetailReady,int PreviousLod);
public sealed record VillageRenderSelection(int[] Lods,VillageRenderCost Cost,bool Fits);

public static class VillageRenderPolicy
{
    public const float PreloadDistance=58,UnloadDistance=78,UnloadDelaySeconds=4;
    public static int DesiredLod(float distance,float screenFraction,int previous,VillageRenderProfile? profile=null)
    {
        profile??=VillageRenderProfile.Integrated;
        // Hysteresis around both screen size and distance. A real near object wins on distance.
        var lod=distance<profile.Lod0Distance || screenFraction>.30f ? 0 : distance<profile.Lod1Distance || screenFraction>.12f ? 1 : distance<profile.Lod2Distance || screenFraction>.035f ? 2 : 3;
        if(previous>=0 && previous<4 && Math.Abs(previous-lod)==1)
        {
            var distances=new[]{profile.Lod0Distance,profile.Lod1Distance,profile.Lod2Distance};
            var boundary=distances[Math.Min(lod,previous)];
            if(distance>boundary*.9f && distance<boundary*1.1f) return previous;
        }
        return lod;
    }
    public static VillageRenderCost Cost(IReadOnlyList<VillageRenderCandidate> items,IReadOnlyList<int> lods)
    {
        var triangles=0;var meshes=0;var batches=0;
        var proxyGroups=new Dictionary<string,HashSet<string>>();var materials=new HashSet<string>();
        for(var i=0;i<items.Count;i++)
        {
            var u=items[i].Unit;var l=u.Lods[lods[i]];triangles+=l.Triangles;materials.UnionWith(l.Materials);
            if(lods[i]<2){meshes++;batches+=l.Surfaces;}
            else
            {
                // Resident distant silhouettes share one HLOD mesh. The selection already
                // budgets every orientation, so batching them globally cannot exceed that bound.
                if(!proxyGroups.TryGetValue("resident",out var set))proxyGroups["resident"]=set=[];
                set.UnionWith(l.Materials);
            }
        }
        meshes+=proxyGroups.Count;batches+=proxyGroups.Values.Sum(s=>s.Count);
        return new(triangles,meshes,batches,materials.Count);
    }
    public static VillageRenderSelection Select(IReadOnlyList<VillageRenderCandidate> items,VillageRenderBudget budget,int pressure=0,VillageRenderProfile? profile=null)
    {
        profile??=VillageRenderProfile.Integrated;
        var lods=items.Select(c=>Math.Max(c.DetailReady?0:2,Math.Min(3,DesiredLod(c.Distance,c.ScreenFraction,c.PreviousLod,profile)+(c.Unit.Important && c.Distance<18?0:pressure)))).ToArray();
        var cost=Cost(items,lods);
        // Demote distant details first, then trees, then ordinary buildings. Nearby landmarks last.
        var order=Enumerable.Range(0,items.Count)
            .OrderBy(i=>items[i].Unit.Important && items[i].Distance<18 ? 4 : profile.PreserveNearbyTrees && items[i].Unit.Category=="tree" && items[i].Distance<28 ? 3 : items[i].Unit.Category is "detail" or "ground" ? 0 : items[i].Unit.Category=="tree" ? 1 : 2)
            .ThenByDescending(i=>items[i].Distance).ThenBy(i=>items[i].Unit.Id,StringComparer.Ordinal).ToArray();
        foreach(var i in order)
        {
            while(!cost.Fits(budget) && lods[i]<3)
            {
                lods[i]++;cost=Cost(items,lods);
            }
            if(cost.Fits(budget))break;
        }
        // Never hide a building to fabricate a pass. An infeasible minimum is explicit.
        return new(lods,cost,cost.Fits(budget));
    }
}
