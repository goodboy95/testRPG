using System.Text.Json;
using TownRpg.Core.Domain;

namespace TownRpg.Core.Content;

public sealed record OpeningResident(string Id,string Building,float[][] Points,float Yaw,string Model,string Prompt);
public sealed record OpeningGoods(string Id,string House,string ItemKey,int Quantity,string Owner);

public static class VillageOpeningContent
{
    public static readonly string[] SceneBuildings = ["shop","community","workshop","house02","house13","house15"];
    public static OpeningResident[] ParseResidents(string json)
    {
        var data = JsonSerializer.Deserialize<OpeningResident[]>(json,new JsonSerializerOptions { PropertyNameCaseInsensitive = true }) ?? throw new InvalidDataException("Missing opening residents.");
        if (data.Length != 7 || !data.Select(r=>r.Id).ToHashSet().SetEquals(VillageOpeningIds.Residents)) throw new InvalidDataException("Opening requires seven unique resident identities.");
        foreach (var r in data)
            if (!SceneBuildings.Contains(r.Building) || r.Points is not { Length: >0 } || r.Points.Any(p=>p is not { Length:2 } || p.Any(x=>!float.IsFinite(x))) || !float.IsFinite(r.Yaw) ||
                r.Model != "res://assets/generated/village/opening-npcs/"+r.Id+".glb" || r.Prompt != "OPENING_TALK_"+r.Id.ToUpperInvariant()) throw new InvalidDataException("Invalid opening resident resource or stage placement: "+r.Id);
        return data;
    }
    public static OpeningGoods[] ParseGoods(string json)
    {
        var data = JsonSerializer.Deserialize<OpeningGoods[]>(json,new JsonSerializerOptions { PropertyNameCaseInsensitive = true }) ?? throw new InvalidDataException("Missing opening goods.");
        if (data.Length != 3 || !data.Select(x=>x.House).ToHashSet().SetEquals(VillageOpeningIds.Houses) || data.Select(x=>x.Id).Distinct().Count()!=3 || data.Any(x=>x.Quantity<=0 || !VillageOpeningIds.Residents.Contains(x.Owner) || x.Id!="village.order."+x.House || string.IsNullOrWhiteSpace(x.ItemKey))) throw new InvalidDataException("Invalid opening order catalog.");
        return data;
    }
    public static IEnumerable<string> StoryKeys => VillageOpeningIds.StoryOperations.Concat(VillageOpeningIds.Houses.SelectMany(h=>new[]{"deliver_"+h,"unload_"+h}));
}
