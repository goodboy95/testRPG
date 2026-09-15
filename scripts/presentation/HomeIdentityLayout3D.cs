using System.Text.Json;

namespace TownRpg.Presentation;

/// <summary>Exterior-only sample fixtures. Metres in home-local Godot coordinates; positions are bottoms.</summary>
public static class HomeIdentityLayout3D
{
    public const string Data = """
    {
      "version": 1,
      "props": [
        {"id":"Bicycle","position":[4.15,0,5.05],"size":[1.85,1.08,0.5]},
        {"id":"PorchStorage","position":[-5.25,0,4.95],"size":[0.65,0.65,0.55]}
      ],
      "clearance":[-1.4,4.5,2.8,8.0],
      "treeTriangleLimit":1900,
      "treeVariants":["broad","upright","open"]
    }
    """;
    public static Definition Layout { get; } = JsonSerializer.Deserialize<Definition>(Data,new JsonSerializerOptions{PropertyNameCaseInsensitive=true})!;
    public sealed record Prop(string Id,float[] Position,float[] Size);
    public sealed record Definition(int Version,Prop[] Props,float[] Clearance,int TreeTriangleLimit,string[] TreeVariants);
}
