using System.Numerics;
using TownRpg.Presentation;

namespace TownRpg.Core.Tests;

public class VillageLayoutTests
{
    [Fact]
    public void VillageContainsTwentyFourDistinctBuildingsAndKeepsHomeDimensions()
    {
        var layout=VillageLayout3D.Layout;
        Assert.Equal(24,layout.Buildings.Length);
        Assert.Equal(18,layout.Buildings.Count(b=>b.Kind is "home" or "house"));
        Assert.Equal(24,layout.Buildings.Select(b=>b.Id).Distinct().Count());
        var home=VillageLayout3D.Home;
        Assert.Equal(HomeLayout3D.Layout.Width,home.Size[0]);
        Assert.Equal(HomeLayout3D.Layout.Depth,home.Size[2]);
        Assert.Equal(new VillageLayout3D.Rectangle(-7.5f,-6,15,18),home.LocalPad);
        foreach(var b in layout.Buildings)
        foreach(var other in layout.Buildings.Where(a=>a.Id!=b.Id))
        {
            var c=b.ToLocal(other.Center);
            var halfOtherX=(other.Yaw-b.Yaw)%180==0 ? other.Size[0]/2 : other.Size[2]/2;
            var halfOtherZ=(other.Yaw-b.Yaw)%180==0 ? other.Size[2]/2 : other.Size[0]/2;
            Assert.True(Math.Abs(c.X)>b.Size[0]/2+halfOtherX+1 || Math.Abs(c.Y)>b.Size[2]/2+halfOtherZ+1,$"Building overlap: {b.Id}/{other.Id}");
        }
    }

    [Fact]
    public void CourtyardsAreFlatAndApproachesHaveWalkableSlopes()
    {
        foreach(var b in VillageLayout3D.Layout.Buildings)
        {
            var pad=b.LocalPad;
            foreach(var p in new[]{new Vector2(0,0),new Vector2(0,b.Size[2]/2+2),new Vector2(pad.X+.5f,pad.Z+.5f)})
            {
                var w=b.ToWorld(p);
                Assert.InRange(Math.Abs(VillageLayout3D.GroundHeight(w.X,w.Y)-b.Elevation),0,.001f);
            }
        }
        foreach(var route in VillageLayout3D.Layout.Routes)
        for(var i=1;i<route.Points.Length;i++)
        {
            var a=new Vector2(route.Points[i-1][0],route.Points[i-1][1]);var b=new Vector2(route.Points[i][0],route.Points[i][1]);
            var steps=(int)Math.Ceiling(Vector2.Distance(a,b)/.2f);
            for(var n=1;n<=steps;n++)
            {
                var p=Vector2.Lerp(a,b,(float)(n-1)/steps);var q=Vector2.Lerp(a,b,(float)n/steps);
                var dy=Math.Abs(VillageLayout3D.WalkHeight(p.X,p.Y)-VillageLayout3D.WalkHeight(q.X,q.Y));
                Assert.True(dy/Vector2.Distance(p,q)<.5f,$"Steep route {route.Id} at {p} ({dy})");
            }
        }
    }

    [Fact]
    public void BridgesCrossTheStreamAndTheDailyRouteFitsTheApprovedScale()
    {
        Assert.Equal(2,VillageLayout3D.Layout.Bridges.Length);
        foreach(var b in VillageLayout3D.Layout.Bridges)
        {
            Assert.True(b.Contains(new(b.CenterX,b.Z)));
            Assert.InRange(b.DeckY-VillageLayout3D.TriangulatedHeight(b.CenterX,b.Z),2.5f,2.8f);
            Assert.InRange(Math.Abs(b.DeckY-VillageLayout3D.TriangulatedHeight(b.CenterX+b.HalfSpan,b.Z)),0,.1f);
        }
        var daily=VillageLayout3D.Layout.Routes.Single(r=>r.Id=="home_shop");
        Assert.InRange(VillageLayout3D.RouteLength(daily)/3.1f,20,30);
        var roads=VillageLayout3D.Layout.Roads;
        Assert.All(roads,r=>Assert.InRange(r.Width,2.5f,4));
    }

    [Fact]
    public void AllDeclaredWalkingRoutesAvoidSolidBuildings()
    {
        foreach(var route in VillageLayout3D.Layout.Routes)
        for(var i=1;i<route.Points.Length;i++)
        {
            var a=new Vector2(route.Points[i-1][0],route.Points[i-1][1]);var b=new Vector2(route.Points[i][0],route.Points[i][1]);
            var steps=(int)Math.Ceiling(Vector2.Distance(a,b)/.25f);
            for(var n=0;n<=steps;n++)
            foreach(var building in VillageLayout3D.Layout.Buildings)
            {
                var p=building.ToLocal(Vector2.Lerp(a,b,(float)n/steps));
                Assert.True(Math.Abs(p.X)>building.Size[0]/2+.4f || Math.Abs(p.Y)>building.Size[2]/2+.4f,$"{route.Id} intersects {building.Id}");
            }
        }
    }

    [Fact]
    public void TerrainGravityIsBoundedAndReattachesToGroundWithoutJumping()
    {
        Assert.Equal(-.4f,TerrainMotionRules.VerticalVelocity(-20,true,.016f));
        Assert.Equal(-9,TerrainMotionRules.VerticalVelocity(0,false,.5f));
        Assert.Equal(-30,TerrainMotionRules.VerticalVelocity(-29,false,1));
        Assert.Equal(-1,TerrainMotionRules.VerticalVelocity(-1,false,-1));
        Assert.True(TerrainMotionRules.HeightAllowsInteraction(3,3.5f));
        Assert.False(TerrainMotionRules.HeightAllowsInteraction(3,5));
        Assert.InRange(TerrainMotionRules.FloorSnap,.2f,.4f);
    }

    [Fact]
    public void WalkingRoutesKeepClearOfCourtyardWallsAndUtilityPoles()
    {
        var layout=VillageLayout3D.Layout;
        foreach(var pole in layout.UtilityPoles)
        foreach(var road in layout.Roads)
        for(var i=1;i<road.Points.Length;i++)
            Assert.True(VillageLayout3D.SegmentDistance(new(pole[0],pole[1]),new(road.Points[i-1][0],road.Points[i-1][1]),new(road.Points[i][0],road.Points[i][1]))>road.Width/2+.3f,$"Pole obstructs {road.Id}: {pole[0]},{pole[1]}");
        foreach(var route in layout.Routes)
        for(var i=1;i<route.Points.Length;i++)
        {
            var a=new Vector2(route.Points[i-1][0],route.Points[i-1][1]);var b=new Vector2(route.Points[i][0],route.Points[i][1]);
            var steps=(int)Math.Ceiling(Vector2.Distance(a,b)/.2f);
            for(var n=0;n<=steps;n++)
            foreach(var house in layout.Buildings.Where(h=>h.Kind=="house"))
            {
                var p=house.ToLocal(Vector2.Lerp(a,b,(float)n/steps));var pad=house.LocalPad;var end=pad.Z+pad.Depth;
                var distance=Math.Min(VillageLayout3D.SegmentDistance(p,new(pad.X,pad.Z),new(pad.X,end)),
                    VillageLayout3D.SegmentDistance(p,new(pad.X+pad.Width,pad.Z),new(pad.X+pad.Width,end)));
                distance=Math.Min(distance,VillageLayout3D.SegmentDistance(p,new(pad.X,end),new(-1.4f,end)));
                distance=Math.Min(distance,VillageLayout3D.SegmentDistance(p,new(1.4f,end),new(pad.X+pad.Width,end)));
                Assert.True(distance>.38f,$"{route.Id} intersects yard wall of {house.Id} at {p}");
            }
        }
    }
}
