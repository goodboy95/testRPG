using System.Security.Cryptography;
using System.Text.Json;
using TownRpg.Presentation;

namespace TownRpg.Core.Tests;

public class VillageTerrainTests
{
    private static string Root { get { var p=new DirectoryInfo(AppContext.BaseDirectory);while(p is not null && !File.Exists(Path.Combine(p.FullName,"project.godot")))p=p.Parent;return p!.FullName; } }
    private static byte[] Read(string path)=>File.ReadAllBytes(Path.Combine(Root,path));
    [Fact]
    public void MigratedSamplesMatchBaselineAndProtectedGround()
    {
        var b=Read("art/terrain/village-sample-v1/baseline.bin");var h=Read("art/terrain/village-sample-v1/height.bin");var protect=Read("art/terrain/village-sample-v1/protected.bin");
        Assert.Equal(512*512*4,h.Length);var changes=0;
        for(var j=32;j<=464;j++) for(var i=48;i<=464;i++)
        {
            var index=j*512+i;var old=BitConverter.ToSingle(b,index*4);var current=BitConverter.ToSingle(h,index*4);
            Assert.True(float.IsFinite(current));
            Assert.InRange(Math.Abs(old-VillageLayout3D.TriangulatedHeight(i*.5f-128,j*.5f-128)),0,.004f);
            if(protect[index]!=0) Assert.InRange(Math.Abs(old-current),0,.01f);
            if(old!=current)changes++;
        }
        Assert.InRange(changes,100,6000);
    }
    [Fact]
    public void InputsAndRuntimeSampleAreVersionedWithoutChangingV2()
    {
        using var manifest=JsonDocument.Parse(Read("assets/generated/village/terrain-sample-v1/manifest.json"));var m=manifest.RootElement;
        Assert.Equal(Convert.ToHexString(SHA256.HashData(Read("assets/generated/village/3d/village-v2.glb"))).ToLowerInvariant(),m.GetProperty("baselineGlbSha256").GetString());
        foreach(var input in m.GetProperty("inputs").EnumerateObject())
            Assert.Equal(input.Value.GetString(),Convert.ToHexString(SHA256.HashData(Read("art/terrain/village-sample-v1/"+input.Name))).ToLowerInvariant());
        Assert.Equal(4,Directory.GetFiles(Path.Combine(Root,"assets/generated/village/terrain-sample-v1/data"),"terrain3d*.res").Length);
        Assert.Equal(4,m.GetProperty("textureIds").GetArrayLength());
        var scene=System.Text.Encoding.UTF8.GetString(Read("assets/generated/village/terrain-sample-v1/terrain.tscn"));
        Assert.Contains("vertex_spacing = 0.5",scene);Assert.Contains("collision_mode = 3",scene);
    }
}
