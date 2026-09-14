using System.Numerics;
using System.Text.Json;

namespace TownRpg.Presentation;

/// <summary>Metres, Godot axes (+Z south). Shared offline source; no session or save state.</summary>
public static class HomeLayout3D
{
    public const string Data = """
    {
      "version":1,"width":12,"depth":9,"height":3.2,"wallThickness":0.2,
      "spawn":[-3.1,0,2.8],"lot":[-7.5,-6,15,18],
      "structure":[
        {"id":"Floor","position":[0,-0.11,0],"size":[12,0.2,9],"material":"tile"},
        {"id":"Ceiling","position":[0,3.3,0],"size":[12,0.2,9],"material":"plaster"},
        {"id":"Courtyard","position":[0,-0.13,3],"size":[15,0.22,18],"material":"concrete"},
        {"id":"Porch","position":[0,-0.014,5.25],"size":[12,0.018,1.5],"material":"tile"},
        {"id":"PorchCanopy","position":[0,2.94,5.27],"size":[12.2,0.12,1.65],"material":"concrete"},
        {"id":"PorchPillarWest","position":[-5.7,1.46,5.86],"size":[0.16,2.92,0.16],"material":"plaster"},
        {"id":"PorchPillarEast","position":[5.7,1.46,5.86],"size":[0.16,2.92,0.16],"material":"plaster"},
        {"id":"FenceWest","position":[-7.4,0.75,3],"size":[0.2,1.5,18],"material":"plaster"},
        {"id":"FenceEast","position":[7.4,0.75,3],"size":[0.2,1.5,18],"material":"plaster"},
        {"id":"FenceRear","position":[0,0.75,-5.9],"size":[14.6,1.5,0.2],"material":"plaster"},
        {"id":"FenceFrontLeft","position":[-4.4,0.75,11.9],"size":[6,1.5,0.2],"material":"plaster"},
        {"id":"FenceFrontRight","position":[4.4,0.75,11.9],"size":[6,1.5,0.2],"material":"plaster"},
        {"id":"ClosedYardGate","position":[0,0.75,11.9],"size":[2.8,1.5,0.08],"material":"green"}
      ],
      "walls":[
        {"id":"south","axis":"x","at":4.4,"from":-6,"to":6,"openings":[[0,1.6,0,2.4],[-3.9,2.1,1,2.45],[3.9,2.1,1.1,2.45]]},
        {"id":"north","axis":"x","at":-4.4,"from":-6,"to":6,"openings":[[-3.9,2,1,2.45],[0,1.8,1,2.45],[3.9,1.4,1.5,2.45]]},
        {"id":"west","axis":"z","at":-5.9,"from":-4.3,"to":4.3,"openings":[[2,1.6,1,2.45],[-2.2,1.6,1,2.45]]},
        {"id":"east","axis":"z","at":5.9,"from":-4.3,"to":4.3,"openings":[[2,1.8,1.1,2.45],[-2.7,1.1,1.5,2.45]]},
        {"id":"partition_west","axis":"z","at":-1.9,"from":-4.3,"to":4.3,"openings":[[-2.5,1.2,0,2.25],[2.1,1.2,0,2.25]]},
        {"id":"partition_east","axis":"z","at":1.9,"from":-4.3,"to":4.3,"openings":[[-2.5,1.2,0,2.25],[2,1.2,0,2.25]]},
        {"id":"bedroom_divider","axis":"x","at":0,"from":-5.8,"to":-2,"openings":[]},
        {"id":"wet_divider","axis":"x","at":-1,"from":2,"to":5.8,"openings":[]}
      ],
      "doors":[
        {"id":"entry","position":[0,0,4.4],"width":1.6,"height":2.4,"axis":"x","swing":-1,"double":true},
        {"id":"bedroom","position":[-1.9,0,2.1],"width":1.2,"height":2.25,"axis":"z","swing":-1,"double":false},
        {"id":"family","position":[-1.9,0,-2.5],"width":1.2,"height":2.25,"axis":"z","swing":-1,"double":false},
        {"id":"kitchen","position":[1.9,0,2],"width":1.2,"height":2.25,"axis":"z","swing":1,"double":false},
        {"id":"bathroom","position":[1.9,0,-2.5],"width":1.2,"height":2.25,"axis":"z","swing":1,"double":false}
      ],
      "furniture":[
        {"id":"PlayerBed","kind":"bed","position":[-4.85,0,3.1],"size":[1.45,0.7,2.25]},
        {"id":"PlayerDesk","kind":"desk","position":[-3.6,0,0.58],"size":[1.55,0.78,0.72]},
        {"id":"DeskChair","kind":"chair","position":[-3.55,0,1.28],"size":[0.48,0.9,0.48]},
        {"id":"PlayerWardrobe","kind":"wardrobe","position":[-5.35,0,0.57],"size":[0.72,2.25,0.8]},
        {"id":"Suitcase","kind":"case","position":[-2.48,0,3.92],"size":[0.65,0.56,0.38]},
        {"id":"FamilyBed","kind":"bed","position":[-4.68,0,-3.1],"size":[1.8,0.7,2.25]},
        {"id":"FamilyWardrobe","kind":"wardrobe","position":[-3.1,0,-0.52],"size":[1.55,2.2,0.65]},
        {"id":"Nightstand","kind":"nightstand","position":[-3.35,0,-3.9],"size":[0.6,0.6,0.6]},
        {"id":"Sofa","kind":"sofa","position":[-1.31,0,-0.05],"size":[0.78,0.85,1.85]},
        {"id":"CoffeeTable","kind":"table","position":[-0.48,0,-0.1],"size":[0.42,0.43,0.9]},
        {"id":"TvConsole","kind":"tv","position":[1.56,0,-0.1],"size":[0.42,1.75,1.8]},
        {"id":"ShoeCabinet","kind":"cabinet","position":[-1.49,0,3.7],"size":[0.45,0.95,0.85]},
        {"id":"DiningTable","kind":"table","position":[0,0,-3.55],"size":[1.3,0.76,0.65]},
        {"id":"DiningChair1","kind":"chair","position":[-1,0,-3.55],"size":[0.48,0.9,0.48]},
        {"id":"DiningChair2","kind":"chair","position":[1,0,-3.55],"size":[0.48,0.9,0.48]},
        {"id":"KitchenSink","kind":"sink","position":[5.38,0,2.1],"size":[0.72,0.9,1.6]},
        {"id":"Stove","kind":"stove","position":[5.36,0,0.35],"size":[0.76,0.9,1.3]},
        {"id":"PrepCabinet","kind":"counter","position":[3.95,0,-0.52],"size":[1.8,0.9,0.65]},
        {"id":"Fridge","kind":"fridge","position":[2.46,0,-0.43],"size":[0.72,1.85,0.8]},
        {"id":"Washbasin","kind":"basin","position":[3.1,0,-3.94],"size":[1,0.85,0.65]},
        {"id":"Toilet","kind":"toilet","position":[5.19,0,-3.64],"size":[0.62,0.9,0.88]},
        {"id":"Washer","kind":"washer","position":[2.62,0,-1.53],"size":[0.72,0.88,0.68]},
        {"id":"PorchBench","kind":"bench","position":[-4,0,5.27],"size":[1.6,0.48,0.5]},
        {"id":"YardTap","kind":"yardtap","position":[6.97,0,8],"size":[0.52,0.85,0.8]},
        {"id":"Plant1","kind":"plant","position":[-5.6,0,6.75],"size":[0.52,0.85,0.52]},
        {"id":"Plant2","kind":"plant","position":[5.6,0,6.75],"size":[0.52,0.85,0.52]},
        {"id":"Plant3","kind":"plant","position":[-5.6,0,7.65],"size":[0.52,0.85,0.52]},
        {"id":"ClothesRack","kind":"rack","position":[-5.3,0,9],"size":[1.9,1.85,0.75]}
      ],
      "lights":[
        {"id":"bedroom","position":[-3.9,2.9,2.1],"switch":[-2.03,1.2,3.15]},
        {"id":"family","position":[-3.9,2.9,-2.1],"switch":[-2.03,1.2,-1.42]},
        {"id":"living","position":[0,2.9,-0.3],"switch":[1.1,1.2,4.28]},
        {"id":"kitchen","position":[3.9,2.9,1.8],"switch":[2.03,1.2,3.04]},
        {"id":"bathroom","position":[3.9,2.9,-2.7],"switch":[2.03,1.2,-3.52]},
        {"id":"porch","position":[0,2.7,5.2],"switch":[1.1,1.2,4.52]}
      ],
      "safePoints":[[-3.1,0,2.8],[-2.4,0,0.8],[-2.6,0,-3.55],[0.75,0,2],[0.75,0,-1.8],[3.8,0,3.2],[3.9,0,-2.4],[0,0,5.5],[0,0,9],[-6.7,0,0],[6.7,0,0],[0,0,-5.25]]
    }
    """;

    public static HomeLayout Layout { get; } = JsonSerializer.Deserialize<HomeLayout>(Data,
        new JsonSerializerOptions { PropertyNameCaseInsensitive = true })!;

    public sealed record Wall(string Id, string Axis, float At, float From, float To, float[][] Openings);
    public sealed record Door(string Id, float[] Position, float Width, float Height, string Axis, int Swing, bool Double)
    {
        public Bounds SweepBounds(float clearance = .34f)
        {
            var p = Position; var reach = Double ? Width / 2 : Width;
            return Axis == "x"
                ? new(p[0] - Width / 2 - clearance, p[2] - reach - clearance, Width + clearance * 2, reach + clearance * 2)
                : new(p[0] + (Swing < 0 ? -reach : 0) - clearance, p[2] - Width / 2 - clearance,
                    reach + clearance * 2, Width + clearance * 2);
        }
    }
    public sealed record Furniture(string Id, string Kind, float[] Position, float[] Size);
    public sealed record StructureBox(string Id, float[] Position, float[] Size, string Material);
    public sealed record Light(string Id, float[] Position, float[] Switch);
    public sealed record HomeLayout(int Version, float Width, float Depth, float Height, float WallThickness,
        float[] Spawn, float[] Lot, StructureBox[] Structure, Wall[] Walls, Door[] Doors, Furniture[] Furniture, Light[] Lights, float[][] SafePoints);
    public readonly record struct Bounds(float X, float Z, float Width, float Depth)
    {
        public bool Contains(Vector2 p) => p.X >= X && p.X <= X + Width && p.Y >= Z && p.Y <= Z + Depth;
    }
    public readonly record struct Box(string Id, Vector3 Center, Vector3 Size);

    public static IEnumerable<Box> WallBoxes()
    {
        foreach (var w in Layout.Walls)
        {
            var cursor = w.From; var i = 0;
            foreach (var o in w.Openings.OrderBy(o => o[0]))
            {
                if (o[0] - o[1] / 2 > cursor) yield return Segment(w, cursor, o[0] - o[1] / 2, 0, Layout.Height, i++);
                if (o[2] > 0) yield return Segment(w, o[0] - o[1] / 2, o[0] + o[1] / 2, 0, o[2], i++);
                yield return Segment(w, o[0] - o[1] / 2, o[0] + o[1] / 2, o[3], Layout.Height, i++);
                cursor = o[0] + o[1] / 2;
            }
            if (cursor < w.To) yield return Segment(w, cursor, w.To, 0, Layout.Height, i);
        }
    }
    private static Box Segment(Wall w, float a, float b, float lo, float hi, int i) => w.Axis == "x"
        ? new(w.Id + i, new((a + b) / 2, (lo + hi) / 2, w.At), new(b - a, hi - lo, Layout.WallThickness))
        : new(w.Id + i, new(w.At, (lo + hi) / 2, (a + b) / 2), new(Layout.WallThickness, hi - lo, b - a));
}
