using System.Numerics;
using System.Security.Cryptography;
using System.Text.Json;
using TownRpg.Presentation;
using Xunit;

namespace TownRpg.Core.Tests;

public sealed class HomePreviewTests
{
    [Fact]
    public void HomeLayoutHasCompleteIndependentRoomDoorsAndLights()
    {
        var l = HomeLayout3D.Layout;
        Assert.Equal((12f, 9f, 3.2f), (l.Width, l.Depth, l.Height));
        Assert.Equal(5, l.Doors.Length); Assert.Equal(6, l.Lights.Length);
        Assert.Equal(5, l.Doors.Select(d => d.Id).Distinct().Count());
        Assert.All(l.Doors, d => Assert.True(d.Width >= 1.2));
        Assert.All(HomeLayout3D.WallBoxes(), b => Assert.True(b.Size.X > 0 && b.Size.Y > 0 && b.Size.Z > 0));
        Assert.All(l.SafePoints, p => Assert.DoesNotContain(HomeLayout3D.WallBoxes(), b =>
            b.Center.Y - b.Size.Y / 2 < 1.6 && Math.Abs(p[0] - b.Center.X) < b.Size.X / 2 + .25 &&
            Math.Abs(p[2] - b.Center.Z) < b.Size.Z / 2 + .25));
    }

    [Theory]
    [InlineData(30)] [InlineData(60)] [InlineData(120)]
    public void DoorPushesAwayAndClosesAlongItsOpeningSide(int fps)
    {
        foreach(var d in HomeLayout3D.Layout.Doors)
        foreach(var side in new[]{-1,1})
        {
            var p=new Vector2(d.Position[0],d.Position[2])+(d.Axis=="x"?Vector2.UnitY:Vector2.UnitX)*(.55f*side);
            var m=new HomeDoorMotion(d);Assert.True(m.Request(p));Assert.Equal(-side,m.OpenSide);
            Assert.False(m.Request(p));
            for(var i=0;i<fps;i++)m.Step(1f/fps,p);
            Assert.True(m.IsOpen);Assert.False(m.IsBlocked);Assert.Equal(-side,m.StableOpenSide);
            Assert.True(m.Request(p));
            for(var i=0;i<fps;i++)m.Step(1f/fps,p);
            Assert.False(m.IsOpen);Assert.False(m.IsMoving);Assert.Equal(0,m.Progress);
            Assert.False(m.Request(new(d.Position[0],d.Position[2])));
        }
    }

    [Fact]
    public void DoorStopsBeforeCapsuleAndResumesWithoutFlippingOrSavingIntermediatePose()
    {
        var d=HomeLayout3D.Layout.Doors[1];var m=new HomeDoorMotion(d);
        Assert.True(m.Request(new(-2.55f,2.1f)));
        // Capsule moves into the opening side, ahead of the moving tip.
        m.Step(1,new Vector2(-1.05f,1.65f));
        Assert.True(m.IsBlocked);Assert.True(m.IsMoving);Assert.False(m.StableOpen);
        Assert.False(m.OverlapsPlayer(m.Progress,m.OpenSide,new(-1.05f,1.65f)));
        var angle=m.AngleRadians(0);m.Step(.1f,new(-1.05f,1.65f));Assert.Equal(angle,m.AngleRadians(0));
        m.Step(1,new(-3,2));Assert.True(m.IsOpen);Assert.Equal(1,m.StableOpenSide);
        Assert.True(m.Request(new(-3,2)));m.Step(.1f,new(-3,2));Assert.True(m.StableOpen);Assert.Equal(1,m.StableOpenSide);
        m.Restore(m.StableOpen,m.StableOpenSide);Assert.True(m.IsOpen);Assert.Equal(1,m.OpenSide);
    }

    [Fact]
    public void DoorResetCancelsPartialMotionWithoutChangingAnotherInstance()
    {
        var d = HomeLayout3D.Layout.Doors[0];
        var a = new HomeDoorMotion(d); var b = new HomeDoorMotion(d);
        a.Request(new Vector2(20)); a.Step(.2f);
        b.Request(new Vector2(20)); b.Step(1);
        a.Reset(); a.Step(1);
        Assert.False(a.IsMoving); Assert.Equal(0, a.Progress); Assert.True(b.IsOpen);
    }

    [Fact]
    public void ExportedHomeHasActualUvSharedFurnitureAndNoStoryOrCharacterPayload()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory is not null && !File.Exists(Path.Combine(directory.FullName, "project.godot"))) directory = directory.Parent;
        Assert.NotNull(directory);
        var root = directory.FullName;
        byte[] Read(string path) => File.ReadAllBytes(Path.Combine(root, path));
        var bytes = Read("assets/generated/home/3d/home-v1.glb");
        Assert.Equal("glTF", System.Text.Encoding.ASCII.GetString(bytes, 0, 4));
        using var glb = JsonDocument.Parse(bytes.AsMemory(20, BitConverter.ToInt32(bytes, 12)));
        using var manifest = JsonDocument.Parse(Read("assets/generated/home/3d/home-v1-manifest.json"));
        var data = glb.RootElement; var meta = manifest.RootElement;
        Assert.Equal("independent-preview", meta.GetProperty("status").GetString());
        Assert.False(meta.GetProperty("runtimeDefault").GetBoolean());
        Assert.Equal(Convert.ToHexString(SHA256.HashData(bytes)).ToLowerInvariant(), meta.GetProperty("runtimeGlbSha256").GetString());
        Assert.Equal(Convert.ToHexString(SHA256.HashData(Read("scripts/presentation/HomeLayout3D.cs"))).ToLowerInvariant(), meta.GetProperty("layoutSha256").GetString());
        Assert.False(data.TryGetProperty("skins", out _)); Assert.False(data.TryGetProperty("animations", out _));
        var nodes = data.GetProperty("nodes").EnumerateArray().Where(n => n.TryGetProperty("name", out _))
            .ToDictionary(n => n.GetProperty("name").GetString()!, n => n);
        foreach (var name in new[] { "Floor", "Ceiling", "Courtyard", "ClosedYardGate", "PlayerBed", "FamilyBed", "Shower", "KitchenSink" })
            Assert.True(nodes.ContainsKey(name), name);
        foreach (var name in new[] { "DiningChair1", "DiningChair2" })
            Assert.Equal(nodes["DeskChair"].GetProperty("mesh").GetInt32(), nodes[name].GetProperty("mesh").GetInt32());
        Assert.Equal(nodes["RoofTilesSouth"].GetProperty("mesh").GetInt32(), nodes["RoofTilesNorth"].GetProperty("mesh").GetInt32());
        // Winding is visible data: an inward roof can pass node/budget checks but render incorrectly.
        var roof = data.GetProperty("meshes")[nodes["RoofTilesSouth"].GetProperty("mesh").GetInt32()];
        foreach (var primitive in roof.GetProperty("primitives").EnumerateArray())
        {
            var accessor = data.GetProperty("accessors")[primitive.GetProperty("attributes").GetProperty("NORMAL").GetInt32()];
            var view = data.GetProperty("bufferViews")[accessor.GetProperty("bufferView").GetInt32()];
            var offset = 28 + BitConverter.ToInt32(bytes, 12) + (view.TryGetProperty("byteOffset", out var vo) ? vo.GetInt32() : 0)
                + (accessor.TryGetProperty("byteOffset", out var ao) ? ao.GetInt32() : 0);
            var stride = view.TryGetProperty("byteStride", out var s) ? s.GetInt32() : 12;
            for (var i = 0; i < accessor.GetProperty("count").GetInt32(); i++) Assert.True(BitConverter.ToSingle(bytes, offset + i * stride + 4) > .8f);
        }
        foreach (var mesh in data.GetProperty("meshes").EnumerateArray())
        foreach (var primitive in mesh.GetProperty("primitives").EnumerateArray())
            Assert.True(primitive.GetProperty("attributes").TryGetProperty("TEXCOORD_0", out _));
        Assert.All(data.GetProperty("images").EnumerateArray(), i => Assert.True(i.TryGetProperty("bufferView", out _)));
        var csv = File.ReadAllLines(Path.Combine(root, "localization/zh_CN.csv"));
        var keys = csv.Select(l => l.Split(',')[0]).ToHashSet(StringComparer.Ordinal);
        foreach (var door in HomeLayout3D.Layout.Doors)
        {
            Assert.Contains("HOME_ROOM_" + door.Id.ToUpperInvariant(), keys);
            Assert.True(nodes.ContainsKey("Door_" + door.Id + "_Hinge0"));
        }
        foreach (var light in HomeLayout3D.Layout.Lights) Assert.Contains("HOME_LIGHT_" + light.Id.ToUpperInvariant(), keys);
    }
}
