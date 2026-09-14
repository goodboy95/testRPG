using System.Numerics;
using System.Text.Json;

namespace TownRpg.Presentation;

/// <summary>Metres, Godot axes (+Z south). Authoritative offline/runtime village geometry; no story state.</summary>
public static class VillageLayout3D
{
    public const string Data = """
    {
      "version":1,"bounds":[-104,-112,208,216],"grid":1,
      "spawn":[-56,0,49],
      "buildings":[
        {"id":"home","kind":"home","position":[-56,0,40],"size":[12,3.2,9],"yaw":0,"variant":0},
        {"id":"house02","kind":"house","position":[-79,-0.09,42],"size":[9,3.5,7],"yaw":0,"variant":0},
        {"id":"house03","kind":"house","position":[-34,0,40],"size":[10,6.1,7],"yaw":0,"variant":1},
        {"id":"house04","kind":"house","position":[-76,-1.26,68],"size":[8,3.3,7],"yaw":180,"variant":2},
        {"id":"house05","kind":"house","position":[-51,-1.44,72],"size":[10,3.5,7],"yaw":180,"variant":0},
        {"id":"house06","kind":"house","position":[-26,-1.26,68],"size":[9,6.1,7],"yaw":180,"variant":3},
        {"id":"house07","kind":"house","position":[-66,1.44,8],"size":[10,3.5,8],"yaw":0,"variant":2},
        {"id":"house08","kind":"house","position":[-42,1.44,8],"size":[9,3.3,7],"yaw":0,"variant":3},
        {"id":"house09","kind":"house","position":[-69,2.61,-18],"size":[10,3.5,7],"yaw":90,"variant":0},
        {"id":"house10","kind":"house","position":[-66,3.915,-47],"size":[9,6.1,7],"yaw":90,"variant":1},
        {"id":"house11","kind":"house","position":[-54,5.04,-72],"size":[8,3.3,7],"yaw":90,"variant":2},
        {"id":"house12","kind":"house","position":[-7,3.645,-41],"size":[10,3.5,8],"yaw":90,"variant":3},
        {"id":"house13","kind":"house","position":[20,0.045,39],"size":[10,3.5,7],"yaw":180,"variant":0},
        {"id":"house14","kind":"house","position":[27,2.16,-8],"size":[9,3.3,7],"yaw":180,"variant":2},
        {"id":"house15","kind":"house","position":[76,0,40],"size":[9,3.5,7],"yaw":180,"variant":3},
        {"id":"house16","kind":"house","position":[85,1.485,7],"size":[10,6.1,7],"yaw":270,"variant":1},
        {"id":"house17","kind":"house","position":[83,3.105,-29],"size":[9,3.5,7],"yaw":270,"variant":0},
        {"id":"house18","kind":"house","position":[80,4.5,-60],"size":[10,3.5,8],"yaw":180,"variant":2},
        {"id":"shop","kind":"shop","position":[-4,1.935,-3],"size":[12,3.8,9],"yaw":0,"variant":0},
        {"id":"clinic","kind":"clinic","position":[18,1.215,13],"size":[10,3.5,8],"yaw":0,"variant":1},
        {"id":"workshop","kind":"workshop","position":[-23,2.7,-20],"size":[12,3.6,8],"yaw":0,"variant":2},
        {"id":"community","kind":"community","position":[-28,1.125,15],"size":[10,4.0,8],"yaw":90,"variant":3},
        {"id":"station","kind":"station","position":[-29,5.895,-91],"size":[20,4.3,9],"yaw":0,"variant":0},
        {"id":"rail_shed","kind":"rail_shed","position":[0,5.67,-86],"size":[8,3.2,6],"yaw":0,"variant":2}
      ],
      "roads":[
        {"id":"home_lane","width":4,"surface":"concrete","points":[[-89,55],[-56,55],[-27,55],[-20,37],[-12,15],[0,8]]},
        {"id":"home_gate","width":2.8,"surface":"concrete","points":[[-56,51.8],[-56,55]]},
        {"id":"south_loop","width":2.5,"surface":"stone","points":[[-89,55],[-88,26],[-78,21],[-62,22],[-41,23],[-20,37]]},
        {"id":"north_spine","width":4,"surface":"concrete","points":[[0,8],[8,-8],[6,-24],[14,-44],[7,-65],[-14,-70],[-28,-78],[-28,-84]]},
        {"id":"west_return","width":3.5,"surface":"stone","points":[[0,8],[-17,4],[-33,-8],[-43,-28],[-42,-51],[-28,-78]]},
        {"id":"creek_south","width":3.5,"surface":"concrete","points":[[0,8],[4,24],[16,28],[38,28],[66,28],[72,26]]},
        {"id":"creek_north","width":3.5,"surface":"stone","points":[[6,-24],[28,-30],[38,-31],[65,-31],[72,-31]]},
        {"id":"west_bank","width":2.5,"surface":"stone","points":[[38,28],[36,6],[35,-13],[36,-31]]},
        {"id":"east_bank","width":2.5,"surface":"stone","points":[[72,26],[72,2],[72,-31],[70,-69],[80,-70]]},
        {"id":"south_return","width":2.5,"surface":"stone","points":[[16,28],[9,28],[7,48],[-6,58],[-27,55]]},
        {"id":"workshop_path","width":2.5,"surface":"concrete","points":[[-23,-14],[-29,-5]]},
        {"id":"station_court","width":4,"surface":"concrete","points":[[-41,-82],[-28,-82],[-12,-79],[0,-80]]},
        {"id":"west_houses","width":2.5,"surface":"stone","points":[[-60,-18],[-48,-19],[-43,-28],[-51,-45],[-57,-47]]},
        {"id":"north_house","width":2.5,"surface":"stone","points":[[-45,-72],[-34,-68]]}
      ],
      "bridges":[{"id":"south","z":28,"width":3.6,"halfSpan":13},{"id":"north","z":-31,"width":3.6,"halfSpan":13}],
      "utilityPoles":[[-92,28],[-83,58],[-38,59],[-23,29],[11,6],[-37,-30],[-45,-58],[-17,-83]],
      "landmarks":[
        {"id":"plaza_tree","position":[-12,1.35,10],"size":[2.6,1,2.6]},
        {"id":"shop_bench","position":[-9,1.935,4.1],"size":[2.2,0.85,0.6]},
        {"id":"shop_crates","position":[0.5,1.935,3.5],"size":[1.5,1.0,1]},
        {"id":"noticeboard","position":[-15.5,1.7,7],"size":[2.1,2,0.35]},
        {"id":"station_bench","position":[-35,5.895,-83.5],"size":[2.4,0.85,0.6]},
        {"id":"station_luggage","position":[-21,5.895,-84.5],"size":[1.8,0.6,0.8]}
      ],
      "routes":[
        {"id":"home_shop","points":[[-56,49],[-56,55],[-27,55],[-20,37],[-12,15],[-4,5]]},
        {"id":"creek_loop","points":[[0,8],[4,24],[16,28],[38,28],[66,28],[72,26],[72,2],[72,-31],[65,-31],[38,-31],[28,-30],[6,-24],[8,-8],[0,8],[4,24],[16,28],[9,28],[7,48],[-6,58],[-27,55],[-56,55],[-56,49]]},
        {"id":"rail_loop","points":[[0,8],[-17,4],[-33,-8],[-43,-28],[-42,-51],[-28,-78],[-28,-82],[-12,-79],[0,-80],[-12,-79],[-14,-70],[7,-65],[14,-44],[6,-24],[8,-8],[0,8]]}
      ]
    }
    """;

    public sealed record Building(string Id, string Kind, float[] Position, float[] Size, float Yaw, int Variant)
    {
        public Vector2 Center => new(Position[0], Position[2]);
        public float Elevation => Position[1];
        public Vector2 ToLocal(Vector2 point)
        {
            var p = point - Center; var angle = Yaw * MathF.PI / 180;
            return new(p.X * MathF.Cos(angle) - p.Y * MathF.Sin(angle), p.X * MathF.Sin(angle) + p.Y * MathF.Cos(angle));
        }
        public Vector2 ToWorld(Vector2 point)
        {
            var angle = Yaw * MathF.PI / 180;
            return Center + new Vector2(point.X * MathF.Cos(angle) + point.Y * MathF.Sin(angle), -point.X * MathF.Sin(angle) + point.Y * MathF.Cos(angle));
        }
        public Rectangle LocalPad => Kind == "home" ? new(-7.5f, -6, 15, 18) : new(-Size[0]/2-1.5f, -Size[2]/2-1, Size[0]+3, Size[2]+7);
    }
    public sealed record Road(string Id, float Width, string Surface, float[][] Points);
    public sealed record Bridge(string Id, float Z, float Width, float HalfSpan)
    {
        public float CenterX => StreamX(Z);
        public float DeckY => BaseHeight(0, Z) + .06f;
        public bool Contains(Vector2 point, float margin = 0) => Math.Abs(point.X-CenterX) <= HalfSpan-margin && Math.Abs(point.Y-Z) <= Width/2-margin;
    }
    public sealed record Landmark(string Id, float[] Position, float[] Size);
    public sealed record Route(string Id, float[][] Points);
    public sealed record Definition(int Version, float[] Bounds, float Grid, float[] Spawn,
        Building[] Buildings, Road[] Roads, Bridge[] Bridges, Landmark[] Landmarks, Route[] Routes, float[][] UtilityPoles);
    public readonly record struct Rectangle(float X, float Z, float Width, float Depth)
    {
        public float OutsideDistance(Vector2 p) => Math.Max(Math.Max(X-p.X,p.X-X-Width), Math.Max(Z-p.Y,p.Y-Z-Depth));
    }
    public static Definition Layout { get; } = JsonSerializer.Deserialize<Definition>(Data, new JsonSerializerOptions { PropertyNameCaseInsensitive=true })!;
    public static Building Home => Layout.Buildings.Single(b => b.Id == "home");
    public static float BaseHeight(float x, float z) => (40-z)*.045f;
    public static float StreamX(float z) => 49+4*MathF.Sin(z/38);
    private static float Smooth(float t) { t=Math.Clamp(t,0,1); return t*t*(3-2*t); }

    public static float GroundHeight(float x, float z)
    {
        var height=BaseHeight(x,z);
        var creekDistance=Math.Abs(x-StreamX(z));
        height-=2.6f*(1-Smooth((creekDistance-4.5f)/4));
        foreach(var b in Layout.Buildings)
        {
            var outside=b.LocalPad.OutsideDistance(b.ToLocal(new(x,z)));
            if(outside>=3) continue;
            var weight=1-Smooth(outside/3);
            height+=(b.Elevation-height)*weight;
        }
        // A shallow earth/concrete approach meets the raised deck exactly. A small
        // vertical lip can still stop a capsule, even when it looks visually harmless.
        foreach(var bridge in Layout.Bridges)
        {
            var distance=Math.Abs(x-bridge.CenterX);
            if(Math.Abs(z-bridge.Z)>bridge.Width/2+1 || distance<bridge.HalfSpan-1 || distance>bridge.HalfSpan+2.5f) continue;
            height+=(bridge.DeckY-height)*Smooth((bridge.HalfSpan+2.5f-distance)/1.5f);
        }
        var edge=Math.Min(Math.Min(x-Layout.Bounds[0],Layout.Bounds[0]+Layout.Bounds[2]-x),
            Math.Min(z-Layout.Bounds[1],Layout.Bounds[1]+Layout.Bounds[3]-z));
        height+=4.5f*(1-Smooth(edge/6));
        return height;
    }

    /// <summary>Exactly interpolate the same two triangles used by Blender and the physics ground.</summary>
    public static float TriangulatedHeight(float x, float z)
    {
        var g=Layout.Grid; var x0=MathF.Floor((x-Layout.Bounds[0])/g)*g+Layout.Bounds[0];
        var z0=MathF.Floor((z-Layout.Bounds[1])/g)*g+Layout.Bounds[1];
        var u=(x-x0)/g; var v=(z-z0)/g;
        var a=GroundHeight(x0,z0); var b=GroundHeight(x0+g,z0); var c=GroundHeight(x0,z0+g); var d=GroundHeight(x0+g,z0+g);
        return u+v<=1 ? a+(b-a)*u+(c-a)*v : d+(c-d)*(1-u)+(b-d)*(1-v);
    }
    public static float WalkHeight(float x, float z) => Layout.Bridges.FirstOrDefault(b=>b.Contains(new(x,z))) is { } bridge ? bridge.DeckY : TriangulatedHeight(x,z);
    public static float RouteLength(Route route) => route.Points.Zip(route.Points.Skip(1), (a,b)=>Vector2.Distance(new(a[0],a[1]),new(b[0],b[1]))).Sum();

    public readonly record struct TreeSite(float X, float Z, float Height, int Variant);
    public static IReadOnlyList<TreeSite> Trees { get; } = MakeTrees();
    private static IReadOnlyList<TreeSite> MakeTrees()
    {
        var trees=new List<TreeSite>();
        for(var ix=0;ix<16;ix++)
        for(var iz=0;iz<15;iz++)
        {
            if((ix+iz)%3==0) continue;
            var hash=(ix*7387+iz*1933+617)%997;
            var x=-94+ix*12+(hash%7-3)*.6f; var z=-101+iz*13+(hash%11-5)*.5f;
            var p=new Vector2(x,z);
            if(Math.Abs(x-StreamX(z))<14 || (x>5 && z>56) || (x>5 && z< -78)) continue;
            if(Layout.Buildings.Any(b=>b.LocalPad.OutsideDistance(b.ToLocal(p))<5)) continue;
            if(Layout.Roads.Any(r=>Enumerable.Range(1,r.Points.Length-1).Any(i=>
                SegmentDistance(p,new(r.Points[i-1][0],r.Points[i-1][1]),new(r.Points[i][0],r.Points[i][1]))<r.Width/2+3.5f))) continue;
            if(Layout.Landmarks.Any(l=>Vector2.Distance(p,new(l.Position[0],l.Position[2]))<7)) continue;
            trees.Add(new(x,z,5.5f+hash%5*.55f,hash%3));
        }
        return trees;
    }
    public static float SegmentDistance(Vector2 p, Vector2 a, Vector2 b)
    {
        var d=b-a; var t=d.LengthSquared()==0?0:Math.Clamp(Vector2.Dot(p-a,d)/d.LengthSquared(),0,1);
        return Vector2.Distance(p,a+d*t);
    }
}
