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

    [Fact]
    public void DoorBlocksOccupiedSweepAndIgnoresRepeatedInputDuringMotion()
    {
        foreach (var d in HomeLayout3D.Layout.Doors)
        {
            var motion = new HomeDoorMotion(d);
            Assert.False(motion.Request(new(d.Position[0], d.Position[2])));
            Assert.True(motion.Request(new(20, 20)));
            motion.Step(.1f); Assert.True(motion.IsMoving);
            Assert.False(motion.Request(new(20, 20)));
            motion.Step(1); Assert.True(motion.IsOpen);
            var sweep = d.SweepBounds();
            Assert.False(motion.Request(new(sweep.X + sweep.Width / 2, sweep.Z + sweep.Depth / 2)));
            Assert.True(motion.Request(new(20, 20)));
            motion.Step(1); Assert.False(motion.IsOpen); Assert.Equal(0, motion.Progress);
        }
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
