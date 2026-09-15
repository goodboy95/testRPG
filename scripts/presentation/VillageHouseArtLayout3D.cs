using System.Text.Json;

namespace TownRpg.Presentation;

/// <summary>Offline/preview-only fixtures, in building-local metres; positions are object bottoms.</summary>
public static class VillageHouseArtLayout3D
{
    public sealed record Prop(string Id,string House,string Kind,float[] Position,float[] Size,bool Solid);
    public sealed record House(string Id,string Wall,float[] Loading);
    public sealed record Document(House[] Houses,Prop[] Props);
    public static Document Layout { get; }=JsonSerializer.Deserialize<Document>(Data,new JsonSerializerOptions{PropertyNameCaseInsensitive=true})!;
    public const string Data = """
    {
      "houses":[
        {"id":"house02","wall":"Ivory","loading":[1.55,5.7,3,3]},
        {"id":"house03","wall":"Silver","loading":[1.55,5.7,3,3]},
        {"id":"house13","wall":"Warm","loading":[1.55,5.7,3,3]},
        {"id":"house15","wall":"Lime","loading":[1.55,5.7,3,3]}
      ],
      "props":[
        {"id":"OldBench","house":"house02","kind":"bench","position":[-2.65,0.08,4.45],"size":[1.5,0.55,0.52],"solid":true},
        {"id":"Storage","house":"house02","kind":"shelf","position":[-4.55,0.08,6.45],"size":[0.7,1.3,1.35],"solid":true},
        {"id":"Laundry","house":"house03","kind":"laundry","position":[-4.9,0.08,6.5],"size":[0.58,1.9,2.4],"solid":true},
        {"id":"Storage","house":"house03","kind":"cabinet","position":[3.4,0.08,4.35],"size":[1.0,1.05,0.65],"solid":true},
        {"id":"BalconyCloth","house":"house03","kind":"cloth","position":[-1.65,3.44,4.73],"size":[0.75,0.65,0.06],"solid":false},
        {"id":"Laundry","house":"house13","kind":"laundry","position":[-5.25,0.08,6.5],"size":[0.55,1.85,2.5],"solid":true},
        {"id":"Baskets","house":"house13","kind":"baskets","position":[-3.7,0.08,5.8],"size":[1.0,0.6,1.0],"solid":true},
        {"id":"Storage","house":"house13","kind":"shelf","position":[3.65,0.08,4.45],"size":[1.15,1.25,0.6],"solid":true},
        {"id":"Vegetables","house":"house15","kind":"garden","position":[-4.45,0.08,6.55],"size":[1.3,1.55,2.55],"solid":true},
        {"id":"RainBarrel","house":"house15","kind":"barrel","position":[3.75,0.08,4.7],"size":[0.75,0.92,0.75],"solid":true},
        {"id":"DeliveryMat","house":"house15","kind":"mat","position":[3.8,0.08,9.15],"size":[1.2,0.018,0.5],"solid":false}
      ]
    }
    """;
}
